using Microsoft.Extensions.Options;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Exceptions;
using sg.gov.cpf.esvc.smpp.server.Extensions;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace sg.gov.cpf.esvc.smpp.server.Services;

public class SslSmppSession : ISmppSession, IDisposable
{
    private readonly TcpClient _client;
    private readonly ILogger _logger;
    private readonly SslConfiguration _sslConfig;
    private readonly SemaphoreSlim _sendLock;
    private readonly X509Certificate2Collection _trustedCertificates;

    private SslStream? _sslStream;
    private bool _isPaused;
    private bool _disposed;
    private bool _sslAuthenticated;

    public string Id { get; }
    public string? SystemId { get; set; }

    public Guid ProcessId { get; set; }

    public bool IsAuthenticated { get; set; }
    public bool IsSslAuthenticated => _sslAuthenticated;
    public X509Certificate2 ServerCertificate { get; }
    public X509Certificate2? ClientCertificate { get; private set; }

    public SslSmppSession(
        TcpClient client,
        ILogger logger,
        IOptions<SslConfiguration> sslConfig,
        X509Certificate2 serverCertificate)
    {
        ProcessId = Guid.NewGuid();
        Id = Guid.NewGuid().ToString("N")[..8];
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sslConfig = sslConfig.Value ?? throw new ArgumentNullException(nameof(sslConfig));
        _sendLock = new SemaphoreSlim(1, 1);
        _trustedCertificates = LoadTrustedCertificates();

        ServerCertificate = serverCertificate ?? throw new ArgumentNullException(nameof(serverCertificate));

        var endpoint = _client.Client?.RemoteEndPoint?.ToString() ?? "Unknown";
        _logger.LogSessionCreated(Id, endpoint);
    }

    /// <summary>
    /// Initialize SSL/TLS connection
    /// </summary>
    public async Task InitializeSslAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SslSmppSession));

        try
        {
            //_logger.LogInformation("Starting SSL handshake for session {SessionId} from {RemoteEndpoint}", Id, _client.Client.RemoteEndPoint);

            var networkStream = _client.GetStream();

            _sslStream = new SslStream(
                networkStream,
                false,
                ValidateClientCertificate,
                SelectServerCertificate);

            var sslOptions = new SslServerAuthenticationOptions
            {
                ServerCertificate = ServerCertificate,
                ClientCertificateRequired = _sslConfig.RequireClientCertificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = _sslConfig.CheckCertificateRevocation
                    ? X509RevocationMode.Online
                    : X509RevocationMode.NoCheck,
                EncryptionPolicy = EncryptionPolicy.RequireEncryption,
            };

            using var timeoutCts = new CancellationTokenSource(_sslConfig.HandshakeTimeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            await _sslStream.AuthenticateAsServerAsync(sslOptions, combinedCts.Token);

            _sslAuthenticated = true;
            ClientCertificate = _sslStream.RemoteCertificate as X509Certificate2;

            if (ClientCertificate != null)
            {
                _logger.LogInformation("Client certificate received - Subject: {Subject}, Issuer: {Issuer}",
                    ClientCertificate.Subject, ClientCertificate.Issuer);
            }
        }
        catch (IOException ex) when (ex.Message.Contains("EOF") || ex.Message.Contains("0 bytes"))
        {
            // change the log level since app container kept sending health check and encounter as error.
            /*
            _logger.LogInformation("Client closed connection during SSL handshake for session {SessionId}. " +
                "Possible causes: protocol mismatch, certificate rejection, or client timeout. Error: {Error}",
                Id, ex.Message);
            */

            //throw new SmppSessionException(Id, "Client terminated SSL handshake", ex);
        }
        catch (AuthenticationException ex)
        {
            _logger.LogError("SSL authentication failed for session {SessionId}: {Error}", Id, ex.Message);
            throw new SmppSessionException(Id, "SSL authentication failed", ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("SSL initialization cancelled for session {SessionId}", Id);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("SSL handshake timeout for session {SessionId}", Id);
            throw new SmppSessionException(Id, "SSL handshake timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSL initialization failed for session {SessionId}", Id);
            throw new SmppSessionException(Id, "SSL initialization failed", ex);
        }
    }

    public async Task<SmppPdu?> ReadPduAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _isPaused || _sslStream == null || !_sslAuthenticated)
            return null;

        try
        {
            var headerSize = SmppConstants.HeaderSize;

            // Read PDU header (16 bytes)
            var headerBuffer = new byte[headerSize];
            var bytesRead = await ReadExactAsync(headerBuffer, headerSize, cancellationToken);

            if (bytesRead < 16)
            {
                _logger.LogError("Session {SessionId} - Incomplete SSL header read: {BytesRead}/16", Id, bytesRead);
                return null;
            }

            // Parse header to get command length
            var pdu = new SmppPdu();
            pdu.ParseHeader(headerBuffer);

            //_logger.LogInformation("Session {SessionId} - SSL PDU header parsed: Length={Length}, CommandId=0x{CommandId:X8}",
            //    Id, pdu.CommandLength, pdu.CommandId);

            // Read PDU body if present
            if (pdu.CommandLength > headerSize)
            {
                var bodyLength = (int)pdu.CommandLength - headerSize;

                var bodyBuffer = new byte[bodyLength];
                bytesRead = await ReadExactAsync(bodyBuffer, bodyLength, cancellationToken);

                if (bytesRead < bodyLength)
                {
                    _logger.LogError("Session {SessionId} - Incomplete SSL body read: {BytesRead}/{Expected}",
                        Id, bytesRead, bodyLength);
                    return null;
                }

                pdu.Body = bodyBuffer;
            }

            return pdu;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Session {SessionId} - SSL PDU read cancelled", Id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId} - Error reading SSL PDU", Id);
            return null;
        }
    }

    public async Task SendPduAsync(SmppPdu pdu, CancellationToken cancellationToken = default)
    {
        if (_disposed || _sslStream == null || !_sslAuthenticated)
            throw new ObjectDisposedException(nameof(SslSmppSession));

        try
        {
            await _sendLock.WaitAsync(cancellationToken);

            var data = pdu.GetBytes();
            await _sslStream.WriteAsync(data, 0, data.Length, cancellationToken);
            await _sslStream.FlushAsync(cancellationToken);

            //_logger.LogInformation("Responding back to ACS Session {SessionId} - SSL PDU sent: CommandId=0x{CommandId:X8}, Length={Length}, Hex:{Hex}",
            //    Id, pdu.CommandId, data.Length, Convert.ToHexString(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId} - Error sending SSL PDU", Id);
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Helper method to read exact number of bytes from SSL stream
    /// </summary>
    private async Task<int> ReadExactAsync(byte[] buffer, int count, CancellationToken cancellationToken)
    {
        if (_sslStream == null)
            return 0;

        int totalBytesRead = 0;

        while (totalBytesRead < count && !cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await _sslStream.ReadAsync(
                buffer,
                totalBytesRead,
                count - totalBytesRead,
                cancellationToken);

            if (bytesRead == 0)
                break; // End of stream

            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }

    public void Pause()
    {
        _isPaused = true;
        _logger.LogDebug("Session {SessionId} SSL connection paused", Id);
    }

    public void Resume()
    {
        _isPaused = false;
        _logger.LogDebug("Session {SessionId} SSL connection resumed", Id);
    }

    public void Close()
    {
        try
        {
            _sslStream?.Close();
            _client?.Close();
            _logger.LogInformation("Session {SessionId} SSL connection closed", Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing SSL session {SessionId}", Id);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _sendLock?.Dispose();
            _sslStream?.Dispose();
            _client?.Dispose();
            _trustedCertificates?.OfType<IDisposable>().ToList().ForEach(cert => cert.Dispose());

            _logger.LogDebug("Session {SessionId} SSL resources disposed", Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing SSL session {SessionId}", Id);
        }
        finally
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// Load trusted CA certificates
    /// </summary>
    private X509Certificate2Collection LoadTrustedCertificates()
    {
        var collection = new X509Certificate2Collection();

        foreach (var certPath in _sslConfig.TrustedCACertificates)
        {
            try
            {
                if (File.Exists(certPath))
                {
                    var cert = new X509Certificate2(certPath);
                    collection.Add(cert);
                    _logger.LogDebug("Loaded trusted CA certificate: {Subject}", cert.Subject);
                }
                else
                {
                    _logger.LogWarning("Trusted CA certificate file not found: {Path}", certPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load trusted CA certificate: {Path}", certPath);
            }
        }

        return collection;
    }

    /// <summary>
    /// Convert our SSL protocols enum to .NET SslProtocols
    /// </summary>
    private static SslProtocols ConvertSslProtocols(SslProtocols protocols)
    {
        var result = SslProtocols.None;

        // Ensure you're checking your custom enum values correctly
        if (protocols.HasFlag(SslProtocols.Tls12))
            result |= SslProtocols.Tls12;
        if (protocols.HasFlag(SslProtocols.Tls13))
            result |= SslProtocols.Tls13;

        // Consider defaulting to TLS 1.2 if none specified
        if (result == SslProtocols.None)
            result = SslProtocols.Tls12;

        return result;
    }


    /// <summary>
    /// Certificate validation callback for client certificates
    /// </summary>
    private bool ValidateClientCertificate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        try
        {
            _logger.LogInformation("Validating client certificate for session {SessionId}, Certs: {Cert}, Errors: {Errors}", 
                Id, certificate?.Subject, sslPolicyErrors);

            // If client certificate is not required and none provided, allow
            if (!_sslConfig.RequireClientCertificate && certificate == null)
            {
                _logger.LogDebug("No client certificate required for session {SessionId}", Id);
                return true;
            }

            // If client certificate is required but none provided, reject
            if (_sslConfig.RequireClientCertificate && certificate == null)
            {
                _logger.LogWarning("Client certificate required but not provided for session {SessionId}", Id);
                return false;
            }

            if (certificate == null)
                return true; // Already handled above

            var cert2 = certificate as X509Certificate2 ?? new X509Certificate2(certificate);

            // Check for development/testing scenarios
            if (_sslConfig.AllowSelfSignedCertificates &&
                sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
            {
                _logger.LogWarning("Allowing self-signed certificate for session {SessionId} (development mode)",
                    Id);
                return ValidateClientCertificateCustom(cert2);
            }

            // Standard validation
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return ValidateClientCertificateCustom(cert2);
            }

            // Handle specific SSL policy errors
            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
            {
                _logger.LogError("Client certificate not available for session {SessionId}", Id);
                return false;
            }

            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            {
                _logger.LogWarning("Allowing certificate name mismatch for session {SessionId}", Id);
                return ValidateClientCertificateCustom(cert2);
            }

            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
            {
                _logger.LogError("Client certificate chain errors for session {SessionId}: {Errors}",
                    Id, string.Join(", ", chain?.ChainStatus.Select(x => x.StatusInformation) ?? []));
                return false;
            }

            _logger.LogError("Client certificate validation failed for session {SessionId}: {Errors}",
                Id, sslPolicyErrors);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during client certificate validation for session {SessionId}", Id);
            return false;
        }
    }


    /// <summary>
    /// Custom client certificate validation
    /// </summary>
    private bool ValidateClientCertificateCustom(X509Certificate2 certificate)
    {
        try
        {
            // Validate expiration
            if (certificate.NotAfter < DateTime.Now)
            {
                _logger.LogError("Client certificate expired for session {SessionId}: {NotAfter}", Id,
                    certificate.NotAfter);
                return false;
            }

            if (certificate.NotBefore > DateTime.Now)
            {
                _logger.LogError("Client certificate not yet valid for session {SessionId}: {NotBefore}", Id,
                    certificate.NotBefore);
                return false;
            }


            // Validate against trusted CA certificates
            if (_trustedCertificates.Count > 0)
            {
                using var chain = new X509Chain();
                chain.ChainPolicy.ExtraStore.AddRange(_trustedCertificates);
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                chain.ChainPolicy.RevocationMode = _sslConfig.CheckCertificateRevocation
                    ? X509RevocationMode.Online
                    : X509RevocationMode.NoCheck;

                var chainBuilt = chain.Build(certificate);
                if (!chainBuilt)
                {
                    _logger.LogError("Client certificate chain validation failed for session {SessionId}: {Errors}",
                        Id, string.Join(", ", chain.ChainStatus.Select(x => x.StatusInformation)));
                    return false;
                }
            }

            _logger.LogInformation("Client certificate validated for session {SessionId} - Subject: {Subject}",
                Id, certificate.Subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Custom certificate validation failed for session {SessionId}", Id);
            return false;
        }
    }

    /// <summary>
    /// Server certificate selection callback
    /// </summary>
    private X509Certificate SelectServerCertificate(
        object sender,
        string targetHost,
        X509CertificateCollection localCertificates,
        X509Certificate? remoteCertificate,
        string[] acceptableIssuers)
    {
        _logger.LogDebug("Selecting server certificate for session {SessionId}, target: {TargetHost}",
            Id, targetHost);

        return ServerCertificate;
    }
}