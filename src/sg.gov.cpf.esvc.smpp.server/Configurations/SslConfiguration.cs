using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace sg.gov.cpf.esvc.smpp.server.Configurations;

/// <summary>
/// SSL/TLS configuration for manual SSL termination
/// </summary>
public class SslConfiguration
{

    /// <summary>
    /// SSL/TLS port for SMPP connections
    /// </summary>
    public int Port { get; set; } = 2776;

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
    public bool CheckCertificateRevocation { get; set; } = false;

    /// <summary>
    /// Allow self-signed certificates (for development/testing only)
    /// </summary>
    public bool AllowSelfSignedCertificates { get; set; } = false;

    /// <summary>
    /// Trusted CA certificate paths for client certificate validation
    /// </summary>
    public List<string> TrustedCACertificates { get; set; } = new();

    /// <summary>
    /// SSL handshake timeout
    /// </summary>
    public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(30);
}