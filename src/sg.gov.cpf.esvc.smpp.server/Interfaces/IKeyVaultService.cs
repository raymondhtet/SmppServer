using sg.gov.cpf.esvc.smpp.server.Services;
using System.Security.Cryptography.X509Certificates;

namespace sg.gov.cpf.esvc.smpp.server.Interfaces
{
    public interface IKeyVaultService
    {
        
        public IList<PostmanCampaignApiKeyMapping>? PostmanCampaignApiKeyMappings { get; set; }

        public string? SessionPassword { get; }

        public X509Certificate2 GetSSLCertificate();

        string GetSecret(string secretName);

        X509Certificate2? GetCertificateFromSecret(string certSecretName, string certPassword);

        void RefreshCacheValues();
    }
}
