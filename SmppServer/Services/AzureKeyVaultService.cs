using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Secrets;
using Smpp.Server.Interfaces;

namespace Smpp.Server.Services;

public class AzureKeyVaultService
    : IKeyVaultService
{
    private readonly ILogger<AzureKeyVaultService> _logger;
    private SecretClient _secretClient;

    public AzureKeyVaultService( ILogger<AzureKeyVaultService> logger, SecretClient secretClient)
    {
        _logger = logger;
        _secretClient = secretClient;

        if (_secretClient == null)
        {
            _secretClient = new SecretClient(new Uri("https://keyvault-smpp-test.vault.azure.net/"), new DefaultAzureCredential());
        }
    }

    public string GetSecret(string secretName)
    {
        var keyVaultSecretResponse = _secretClient.GetSecret(secretName);
        return keyVaultSecretResponse?.Value?.Value ?? string.Empty;
    }

    public X509Certificate2? GetCertificateFromSecret(string certSecretName, string certPassword)
    {
        var certBytesString = GetSecret(certSecretName);

        if (string.IsNullOrEmpty(certBytesString))
            return null;
        
        var certBytes = Convert.FromBase64String(certBytesString);
        return new X509Certificate2(certBytes, certPassword);
    }

    public X509Certificate2? GetCertificate(string certName, string certPassword)
    {
        throw new NotImplementedException();
    }
}