using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Smpp.Server.Configurations;
using Smpp.Server.Interfaces;

namespace Smpp.Server.Services;

public class SslCertificateManager : ISslCertificateManager, IDisposable
{
    private readonly SslConfiguration _sslConfig;
    private readonly ILogger<SslCertificateManager> _logger;
    private readonly Timer _certificateMonitorTimer;
    private X509Certificate2? _cachedServerCertificate;
    private X509Certificate2Collection? _cachedTrustedCertificates;
    private DateTime _lastCertificateCheck = DateTime.MinValue;
    private readonly SemaphoreSlim _certificateLoadLock = new(1, 1);

    public event EventHandler<CertificateExpiringEventArgs>? CertificateExpiring;

    public SslCertificateManager(
        IOptions<SslConfiguration> sslConfig,
        ILogger<SslCertificateManager> logger)
    {
        _sslConfig = sslConfig.Value ?? throw new ArgumentNullException(nameof(sslConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Monitor certificates every hour
        _certificateMonitorTimer = new Timer(MonitorCertificates, null, 
            TimeSpan.FromHours(1), TimeSpan.FromHours(1));

        _logger.LogInformation("SSL Certificate Manager initialized");
    }

    
    public Task<X509Certificate2> LoadServerCertificateAsync()
    {
        await _certificateLoadLock.WaitAsync();
        try
        {
            // Return cached certificate if still valid
            if (_cachedServerCertificate != null && IsCertificateValid(_cachedServerCertificate))
            {
                _logger.LogDebug("📋 Using cached server certificate");
                return _cachedServerCertificate;
            }

            _logger.LogInformation("🔄 Loading server certificate...");

            X509Certificate2 certificate;

            if (!string.IsNullOrEmpty(_sslConfig.CertificatePath))
            {
                // Load from file
                certificate = await LoadCertificateFromFileAsync(_sslConfig.CertificatePath, _sslConfig.CertificatePassword);
                _logger.LogInformation("📁 Server certificate loaded from file: {Subject}", certificate.Subject);
            }
            else if (!string.IsNullOrEmpty(_sslConfig.CertificateSubject))
            {
                // Load from certificate store
                certificate = await LoadCertificateFromStoreAsync(
                    _sslConfig.CertificateStoreLocation,
                    _sslConfig.CertificateStoreName,
                    _sslConfig.CertificateSubject);
                _logger.LogInformation("🏪 Server certificate loaded from store: {Subject}", certificate.Subject);
            }
            else
            {
                throw new SmppConfigurationException("SSL", "No certificate configuration provided. Specify either CertificatePath or CertificateSubject.");
            }

            // Validate the certificate
            var isValid = await ValidateCertificateAsync(certificate);
            if (!isValid)
            {
                throw new SmppConfigurationException("SSL", "Server certificate validation failed");
            }

            _cachedServerCertificate = certificate;
            _lastCertificateCheck = DateTime.UtcNow;

            _logger.LogInformation("✅ Server certificate validated and cached - Expires: {ExpiryDate}", certificate.NotAfter);
            
            // Check if certificate is expiring soon
            CheckCertificateExpiration(certificate);

            return certificate;
        }
        finally
        {
            _certificateLoadLock.Release();
        }
    }

    public Task<bool> ValidateCertificateAsync(X509Certificate2 certificate)
    {
        throw new NotImplementedException();
    }

    public Task<X509Certificate2Collection> LoadTrustedCertificatesAsync()
    {
        throw new NotImplementedException();
    }

    public Task RefreshCertificatesAsync()
    {
        throw new NotImplementedException();
    }

    public event EventHandler<CertificateExpiringEventArgs>? CertificateExpiring;
    public void Dispose()
    {
        throw new NotImplementedException();
    }
}