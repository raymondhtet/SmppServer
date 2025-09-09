using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Smpp.Server.Configurations;

/// <summary>
/// SSL/TLS configuration for manual SSL termination
/// </summary>
public class SslConfiguration
{
    /// <summary>
    /// Enable SSL/TLS for SMPP connections
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// SSL/TLS port for SMPP connections
    /// </summary>
    public int Port { get; set; } = 2776;

    /// <summary>
    /// Path to the SSL certificate file (PFX format)
    /// </summary>
    public string CertificatePath { get; set; } = string.Empty;

    /// <summary>
    /// Password for the SSL certificate
    /// </summary>
    public string CertificatePassword { get; set; } = string.Empty;

    /// <summary>
    /// Certificate store location (for Windows certificate store)
    /// </summary>
    public StoreLocation CertificateStoreLocation { get; set; } = StoreLocation.LocalMachine;

    /// <summary>
    /// Certificate store name (for Windows certificate store)
    /// </summary>
    public StoreName CertificateStoreName { get; set; } = StoreName.My;

    /// <summary>
    /// Certificate subject or thumbprint (for Windows certificate store)
    /// </summary>
    public string CertificateSubject { get; set; } = string.Empty;

    /// <summary>
    /// SSL/TLS protocol versions to support
    /// </summary>
    public SslProtocols SupportedProtocols { get; set; } = SslProtocols.Tls12 | SslProtocols.Tls13;

    /// <summary>
    /// Require client certificates for mutual authentication
    /// </summary>
    public bool RequireClientCertificate { get; set; } = false;

    /// <summary>
    /// Check certificate revocation status
    /// </summary>
    public bool CheckCertificateRevocation { get; set; } = true;

    /// <summary>
    /// Allow self-signed certificates (for development/testing only)
    /// </summary>
    public bool AllowSelfSignedCertificates { get; set; } = true;

    /// <summary>
    /// Trusted CA certificate paths for client certificate validation
    /// </summary>
    public List<string> TrustedCACertificates { get; set; } = new();

    /// <summary>
    /// SSL handshake timeout
    /// </summary>
    public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Enable SSL session resumption
    /// </summary>
    public bool EnableSessionResumption { get; set; } = true;

    /// <summary>
    /// SSL session cache size
    /// </summary>
    public int SessionCacheSize { get; set; } = 1000;

    /// <summary>
    /// SSL session timeout
    /// </summary>
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Cipher suites to allow (empty = system default)
    /// </summary>
    public List<string> AllowedCipherSuites { get; set; } = new();

    /// <summary>
    /// Enable OCSP stapling
    /// </summary>
    public bool EnableOcspStapling { get; set; } = false;
}