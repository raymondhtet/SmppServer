using System.Security.Cryptography.X509Certificates;
using sg.gov.cpf.esvc.smpp.server.Models;

namespace sg.gov.cpf.esvc.smpp.server.Interfaces;

public interface ISslCertificateManager
{
    Task<X509Certificate2> LoadServerCertificateAsync(X509Certificate2 serverCertificate);
    Task<bool> ValidateCertificateAsync(X509Certificate2 certificate);
    
    Task RefreshCertificatesAsync();
    event EventHandler<CertificateExpiringEventArgs>? CertificateExpiring;
}