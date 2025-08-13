using System.Security.Cryptography.X509Certificates;

namespace Smpp.Server.Interfaces;

public interface ISslCertificateManager
{
    Task<X509Certificate2> LoadServerCertificateAsync();
    Task<bool> ValidateCertificateAsync(X509Certificate2 certificate);
    Task<X509Certificate2Collection> LoadTrustedCertificatesAsync();
    Task RefreshCertificatesAsync();
    event EventHandler<CertificateExpiringEventArgs>? CertificateExpiring;
}