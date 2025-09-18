using System.Security.Cryptography.X509Certificates;

namespace Smpp.Server.Interfaces;

public interface IKeyVaultService
{
    string GetSecret(string secretName);
    
    X509Certificate2? GetCertificateFromSecret(string certSecretName, string certPassword);
    
    X509Certificate2? GetCertificate(string certName, string certPassword);
}