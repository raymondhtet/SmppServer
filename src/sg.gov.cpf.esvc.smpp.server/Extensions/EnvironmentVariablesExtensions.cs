using System.Diagnostics.CodeAnalysis;
using sg.gov.cpf.esvc.smpp.server.Configurations;

namespace sg.gov.cpf.esvc.smpp.server.Extensions
{
    [ExcludeFromCodeCoverage]
    public static class EnvironmentVariablesExtensions
    {
        public static EnvironmentVariablesConfiguration ParseEnvironmentVariables(this IServiceCollection services)
        {
            var envConfig = new EnvironmentVariablesConfiguration
            {
                KeyVaultUri = GetStringValue("AKV_URL"),
                IsEnabledSSL = GetBooleanValue("SSL_ENABLED"),
                IsDeliverSmEnabled = GetBooleanValue("IS_DELIVER_SM_ENABLED"),
                Environment = GetStringValue("ENV"),
                AppInsightConnectionString = GetStringValue("APPINSIGHT_CONNECTIONSTRING"),
                SessionUserName = GetStringValue("SESSION_NAME"),
                IsWhitelistedEnabled = GetBooleanValue("WHITEDLISTED_ENABLED"),
                SslServerCertificateName = GetStringValue("SSL_CERTIFICATE"),
                SessionPasswordKey = GetStringValue("SESSION_SECRET"),
                SslServerCertificatePassphrase = Environment.GetEnvironmentVariable("SSL_CERTIFICATE_PASSPHRASE")?.Trim(),
                CampaignApiKeyMappingName = GetStringValue("CAMPAIGN_API_KEY_MAPPING"),
                PostmanBaseUrl = GetStringValue("POSTMAN_BASE_URL"),
                LogLevelString = GetStringValue("LOG_LEVEL")
            };


            services.AddSingleton<EnvironmentVariablesConfiguration>(envConfig);

            return envConfig;
        }

        static bool GetBooleanValue(string envName)
        {
            var flag = GetStringValue(envName);

            return bool.Parse(flag);
        }

        static string GetStringValue(string envName)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envName)))
                throw new InvalidOperationException($"The Environment Value {envName} cannot be null or empty");

            return Environment.GetEnvironmentVariable(envName)!.Trim();
        }

    }
}
