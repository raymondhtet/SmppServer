using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Smpp.Server.Configurations;
using Smpp.Server.Exceptions;
using Smpp.Server.Interfaces;
using Smpp.Server.Models;

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

    /// <summary>
    /// Load server certificate from configured source
    /// </summary>
    public async Task<X509Certificate2> LoadServerCertificateAsync()
    {
        await _certificateLoadLock.WaitAsync();
        try
        {
            // Return cached certificate if still valid
            if (_cachedServerCertificate != null && IsCertificateValid(_cachedServerCertificate))
            {
                _logger.LogInformation("Using cached server certificate");
                return _cachedServerCertificate;
            }

            _logger.LogInformation("Loading server certificate...");

            X509Certificate2 certificate;

            if (!string.IsNullOrEmpty(_sslConfig.CertificatePath))
            {
                // Load from file
                certificate = await LoadCertificateFromFileAsync(_sslConfig.CertificatePath, _sslConfig.CertificatePassword);
                _logger.LogInformation("Server certificate loaded from file: {Subject}", certificate.Subject);
            }
            else if (!string.IsNullOrEmpty(_sslConfig.CertificateSubject))
            {
                // Load from certificate store
                certificate = await LoadCertificateFromStoreAsync(
                    _sslConfig.CertificateStoreLocation,
                    _sslConfig.CertificateStoreName,
                    _sslConfig.CertificateSubject);
                _logger.LogInformation("Server certificate loaded from store: {Subject}", certificate.Subject);
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

    /// <summary>
    /// Validate certificate properties
    /// </summary>
    public async Task<bool> ValidateCertificateAsync(X509Certificate2 certificate)
    {
        try
        {
            _logger.LogDebug("Validating certificate: {Subject}", certificate.Subject);

            // Check expiration
            if (certificate.NotAfter <= DateTime.Now)
            {
                _logger.LogError("Certificate has expired: {ExpiryDate}", certificate.NotAfter);
                return false;
            }

            if (certificate.NotBefore > DateTime.Now)
            {
                _logger.LogError("Certificate is not yet valid: {ValidFrom}", certificate.NotBefore);
                return false;
            }

            // Check if expiring soon (30 days)
            if (certificate.NotAfter <= DateTime.Now.AddDays(30))
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

            _logger.LogDebug("Certificate validation passed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Certificate validation failed");
            return false;
        }
    }

    /// <summary>
    /// Load trusted CA certificates
    /// </summary>
    public async Task<X509Certificate2Collection> LoadTrustedCertificatesAsync()
    {
        if (_cachedTrustedCertificates != null)
        {
            _logger.LogInformation("Using cached trusted certificates");
            return _cachedTrustedCertificates;
        }

        _logger.LogInformation("Loading trusted CA certificates...");

        var collection = new X509Certificate2Collection();

        foreach (var certPath in _sslConfig.TrustedCACertificates)
        {
            try
            {
                var certificate = await LoadCertificateFromFileAsync(certPath, "");
                collection.Add(certificate);
                _logger.LogInformation("Loaded trusted CA certificate: {Subject}", certificate.Subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load trusted CA certificate: {Path}", certPath);
            }
        }

        _cachedTrustedCertificates = collection;
        _logger.LogInformation("Loaded {Count} trusted CA certificates", collection.Count);

        return collection;
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
        
        _cachedTrustedCertificates?.OfType<IDisposable>().ToList().ForEach(cert => cert.Dispose());
        _cachedTrustedCertificates = null;

        // Reload certificates
        await LoadServerCertificateAsync();
        await LoadTrustedCertificatesAsync();

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
        return certificate.NotBefore <= DateTime.Now && certificate.NotAfter > DateTime.Now;
    }

    /// <summary>
    /// Check certificate expiration and raise events
    /// </summary>
    private void CheckCertificateExpiration(X509Certificate2 certificate)
    {
        var daysUntilExpiry = (certificate.NotAfter - DateTime.Now).TotalDays;
        
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
        _cachedTrustedCertificates?.OfType<IDisposable>().ToList().ForEach(cert => cert.Dispose());
    }

}