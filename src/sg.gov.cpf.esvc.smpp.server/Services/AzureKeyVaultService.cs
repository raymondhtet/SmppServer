using Azure.Security.KeyVault.Secrets;
using Microsoft.ApplicationInsights;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;

namespace sg.gov.cpf.esvc.smpp.server.Services
{
    public class AzureKeyVaultService : IKeyVaultService
    {
        private readonly ILogger<AzureKeyVaultService> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly SecretClient _secretClient;
        private readonly EnvironmentVariablesConfiguration _environmentVariables;

        public IList<PostmanCampaignApiKeyMapping>? PostmanCampaignApiKeyMappings { get; set; } = [];


        public string? SessionPassword
        {
            get => sessionPassword ?? "";
        }

        private string? sessionPassword { get; set; }
        
        private readonly X509Certificate2? _serverCertificate;

        public AzureKeyVaultService(ILogger<AzureKeyVaultService> logger,
            SecretClient secretClient,
            TelemetryClient telemetryClient,
            EnvironmentVariablesConfiguration environmentVariables)
        {
            _logger = logger;
            _secretClient = secretClient;
            _telemetryClient = telemetryClient;
            _environmentVariables = environmentVariables;

            LoadConfigurationValues();

            if (_environmentVariables.IsEnabledSSL)
            {
                _serverCertificate = LoadServerCertificate();
            }
        }

        private X509Certificate2 LoadServerCertificate()
        {
            _telemetryClient.TrackTrace("Loading server certificate");
            var certPassword = string.IsNullOrWhiteSpace(_environmentVariables.SslServerCertificatePassphrase) 
                ? string.Empty : GetSecret(_environmentVariables.SslServerCertificatePassphrase);
            return GetCertificateFromSecret(_environmentVariables.SslServerCertificateName, certPassword) ?? throw new Exception("unable to load server certificate");
        }

        private void LoadConfigurationValues()
        {
            _logger.LogInformation("Loading configuration values");

            LoadCampaignApiKeyMapping();
            LoadSessionPassword();
        }

        private void LoadSessionPassword()
        {
            if (string.IsNullOrWhiteSpace(sessionPassword))
            {
                _telemetryClient.TrackTrace("Loading Session Password");
                sessionPassword = GetSecret(_environmentVariables.SessionPasswordKey);
            }
        }

        private void LoadCampaignApiKeyMapping()
        {
            if (PostmanCampaignApiKeyMappings == null ||
                (PostmanCampaignApiKeyMappings != null && PostmanCampaignApiKeyMappings.Count < 1))
            {
                _telemetryClient.TrackTrace("Loading postman campaign api key mapping");
                var mappingJson = GetSecret(_environmentVariables.CampaignApiKeyMappingName);

                _logger.LogInformation("Loading postman campaign mappings: {MappingJson}", mappingJson);

                PostmanCampaignApiKeyMappings = JsonConvert.DeserializeObject<IList<PostmanCampaignApiKeyMapping>>(
                                                    mappingJson,
                                                    new JsonSerializerSettings()
                                                    {
                                                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                                                    }) ??
                                                throw new Exception("unable to load campaign and api key mapping");
            }
        }

        public string GetSecret(string secretName)
        {
            _logger.LogInformation("Secret name to retrieve: {SecretName}", secretName);
            var keyVaultSecretResponse = _secretClient.GetSecret(secretName);
            return keyVaultSecretResponse?.Value?.Value ?? string.Empty;
        }

        public X509Certificate2? GetCertificateFromSecret(string certSecretName, string certPassword)
        {
            var certBytesString =
                GetSecret(certSecretName) ?? throw new Exception("Certificate cannot be null or empty");

            if (string.IsNullOrEmpty(certBytesString))
                return null;

            var certBytes = Convert.FromBase64String(certBytesString.Trim());

            _logger.LogInformation("Cert Base64String: {Base64String}", certBytesString);
            _logger.LogInformation("Cert Password: {CertPassword}", certPassword);
            var certificate = GetCertificateForMac(certBytes);
            return new X509Certificate2(certificate);
        }

        private static X509Certificate2 GetCertificateForMac(byte[] CertBytes)
        {
            var pkcs12Store = new Pkcs12StoreBuilder().Build();
            using (var memStream = new MemoryStream(CertBytes))
                pkcs12Store.Load(memStream, []);

            var keyAlias = pkcs12Store.Aliases.SingleOrDefault(a => pkcs12Store.IsKeyEntry(a));

            var key = (RsaPrivateCrtKeyParameters)pkcs12Store.GetKey(keyAlias).Key;
            var bouncyCertificate = pkcs12Store.GetCertificate(keyAlias).Certificate;

            var certificate = new X509Certificate2(DotNetUtilities.ToX509Certificate(bouncyCertificate));
            var parameters = DotNetUtilities.ToRSAParameters(key);

            var rsa = new RSACryptoServiceProvider();
            rsa.ImportParameters(parameters);
            certificate = certificate.CopyWithPrivateKey(rsa);


            return certificate;
        }

        public void RefreshCacheValues()
        {
            LoadConfigurationValues();
        }

        public X509Certificate2 GetSSLCertificate()
        {
            _logger.LogInformation("Is certificate null? {IsNull}", _serverCertificate == null);
            if (_serverCertificate == null)
            {
                return LoadServerCertificate();
            }

            return _serverCertificate;
        }
    }

    public record PostmanCampaignApiKeyMapping(string CampaignId, string ApiKey, string Scheme);
}