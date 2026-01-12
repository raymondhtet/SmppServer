using Microsoft.Extensions.Options;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Exceptions;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using System.Security.Cryptography.X509Certificates;

namespace sg.gov.cpf.esvc.smpp.server.Services;

public class SslCertificateManager : ISslCertificateManager, IDisposable
{
    private readonly SslConfiguration _sslConfig;
    private readonly ILogger<SslCertificateManager> _logger;
    private readonly Timer _certificateMonitorTimer;
    private X509Certificate2? _cachedServerCertificate;
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

    /// <summary>
    /// Load server certificate from configured source
    /// </summary>
    public async Task<X509Certificate2> LoadServerCertificateAsync(X509Certificate2 serverCertificate)
    {
        await _certificateLoadLock.WaitAsync();
        try
        {
            // Return cached certificate if still valid
            if (_cachedServerCertificate != null && IsCertificateValid(_cachedServerCertificate))
            {
                return _cachedServerCertificate;
            }

            // Validate the certificate
            var isValid = await ValidateCertificateAsync(serverCertificate);
            if (!isValid)
            {
                throw new SmppConfigurationException("SSL", "Server certificate validation failed");
            }

            _cachedServerCertificate = serverCertificate;
            _lastCertificateCheck = DateTime.UtcNow;

            _logger.LogInformation("Server certificate validated and cached: {ExpiryDate}", serverCertificate);
            
            // Check if certificate is expiring soon
            CheckCertificateExpiration(serverCertificate);

            return serverCertificate;
        }
        finally
        {
            _certificateLoadLock.Release();
        }
    }

    /// <summary>
    /// Validate certificate properties
    /// </summary>
    public async Task<bool> ValidateCertificateAsync(X509Certificate2 certificate)
    {
        try
        {
            // Check if certificate has private key
            if (!certificate.HasPrivateKey)
            {
                _logger.LogError("Certificate does not have a private key");
                return false;
            }

            // Check expiration
            if (certificate.NotAfter <= DateTime.UtcNow)
            {
                _logger.LogError("Certificate has expired: {ExpiryDate}", certificate.NotAfter);
                return false;
            }

            if (certificate.NotBefore > DateTime.UtcNow)
            {
                _logger.LogError("Certificate is not yet valid: {ValidFrom}", certificate.NotBefore);
                return false;
            }

            // Check if expiring soon (30 days)
            if (certificate.NotAfter <= DateTime.UtcNow.AddDays(30))
            {
                _logger.LogWarning("Certificate expires soon: {ExpiryDate}", certificate.NotAfter);
            }

            // Validate certificate chain
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = _sslConfig.CheckCertificateRevocation 
                ? X509RevocationMode.Online 
                : X509RevocationMode.NoCheck;

            var chainBuilt = chain.Build(certificate);
            if (!chainBuilt)
            {
                var errors = string.Join(", ", chain.ChainStatus.Select(x => x.StatusInformation));
                
                if (_sslConfig.AllowSelfSignedCertificates)
                {
                    _logger.LogWarning("Certificate chain validation failed but allowing self-signed: {Errors}", errors);
                }
                else
                {
                    _logger.LogError("Certificate chain validation failed: {Errors}", errors);
                    return false;
                }
            }

            // Additional custom validation can be added here
            await Task.CompletedTask; // For potential async operations

            _logger.LogInformation("Certificate validation passed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Certificate validation failed");
            return false;
        }
    }

    
    /// <summary>
    /// Refresh all cached certificates
    /// </summary>
    public async Task RefreshCertificatesAsync()
    {
        _logger.LogInformation("Refreshing SSL certificates...");

        // Clear cache
        _cachedServerCertificate?.Dispose();
        _cachedServerCertificate = null;
        
        // Reload certificates
        //await LoadServerCertificateAsync();
        
        _logger.LogInformation("SSL certificates refreshed successfully");
    }

    /// <summary>
    /// Load certificate from file
    /// </summary>
    private async Task<X509Certificate2> LoadCertificateFromFileAsync(string path, string password)
    {
        try
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Certificate file not found: {path}");
            }

            _logger.LogDebug("Loading certificate from file: {Path}", path);

            var certificateData = await File.ReadAllBytesAsync(path);
            
            var certificate = string.IsNullOrEmpty(password) 
                ? new X509Certificate2(certificateData)
                : new X509Certificate2(certificateData, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

            return certificate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load certificate from file: {Path}", path);
            throw;
        }
    }

    /// <summary>
    /// Load certificate from Windows certificate store
    /// </summary>
    private async Task<X509Certificate2> LoadCertificateFromStoreAsync(
        StoreLocation storeLocation, 
        StoreName storeName, 
        string subjectOrThumbprint)
    {
        try
        {
            _logger.LogDebug("Loading certificate from store - Location: {Location}, Store: {Store}, Subject: {Subject}",
                storeLocation, storeName, subjectOrThumbprint);

            using var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);

            var certificates = store.Certificates;

            // Try to find by thumbprint first
            var foundCerts = certificates.Find(X509FindType.FindByThumbprint, subjectOrThumbprint, false);
            
            // If not found by thumbprint, try by subject name
            if (foundCerts.Count == 0)
            {
                foundCerts = certificates.Find(X509FindType.FindBySubjectName, subjectOrThumbprint, false);
            }

            // If still not found, try by subject distinguished name
            if (foundCerts.Count == 0)
            {
                foundCerts = certificates.Find(X509FindType.FindBySubjectDistinguishedName, subjectOrThumbprint, false);
            }

            if (foundCerts.Count == 0)
            {
                throw new InvalidOperationException($"Certificate not found in store: {subjectOrThumbprint}");
            }

            if (foundCerts.Count > 1)
            {
                _logger.LogWarning("Multiple certificates found matching '{Subject}', using the first one", subjectOrThumbprint);
            }

            var certificate = foundCerts[0];
            
            // Create a copy with private key access
            var certificateWithPrivateKey = new X509Certificate2(certificate.Export(X509ContentType.Pfx), 
                "", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

            await Task.CompletedTask; // For consistency with async signature
            return certificateWithPrivateKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load certificate from store");
            throw;
        }
    }

    /// <summary>
    /// Check if certificate is still valid
    /// </summary>
    private static bool IsCertificateValid(X509Certificate2 certificate)
    {
        return certificate.NotBefore <= DateTime.UtcNow && certificate.NotAfter > DateTime.UtcNow;
    }

    /// <summary>
    /// Check certificate expiration and raise events
    /// </summary>
    private void CheckCertificateExpiration(X509Certificate2 certificate)
    {
        var daysUntilExpiry = (certificate.NotAfter - DateTime.UtcNow).TotalDays;
        
        if (daysUntilExpiry is <= 30 and > 0)
        {
            var eventArgs = new CertificateExpiringEventArgs
            {
                Certificate = certificate,
                DaysUntilExpiry = (int)daysUntilExpiry,
                ExpiryDate = certificate.NotAfter
            };

            _logger.LogWarning("Certificate expiring in {Days} days: {Subject}", 
                (int)daysUntilExpiry, certificate.Subject);

            CertificateExpiring?.Invoke(this, eventArgs);
        }
    }

    /// <summary>
    /// Monitor certificates for expiration
    /// </summary>
    private void MonitorCertificates(object? state)
    {
        try
        {
            if (_cachedServerCertificate != null)
            {
                CheckCertificateExpiration(_cachedServerCertificate);
                
                // Auto-refresh if certificate is invalid
                if (!IsCertificateValid(_cachedServerCertificate))
                {
                    _logger.LogWarning("Server certificate is invalid, triggering refresh");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await RefreshCertificatesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to auto-refresh certificates");
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during certificate monitoring");
        }
    }

    public void Dispose()
    {
        _certificateMonitorTimer?.Dispose();
        _certificateLoadLock?.Dispose();
        _cachedServerCertificate?.Dispose();
    }

}