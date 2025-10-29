namespace sg.gov.cpf.esvc.smpp.server.Configurations
{
    public class EnvironmentVariablesConfiguration
    {
        public string KeyVaultUri { get; set; } = string.Empty;

        public bool IsEnabledSSL { get; set; }

        public bool IsDeliverSmEnabled { get; set; }

        public string SessionUserName { get; set; } = string.Empty;

        public string AppInsightConnectionString { get; set; } = string.Empty;

        public bool IsWhitelistedEnabled { get; set; }
        public bool IsProduction { get => Environment.Equals("prd", StringComparison.OrdinalIgnoreCase); }

        public string Environment { get; set; } = string.Empty;

        public string SslServerCertificateName { get; set; } = string.Empty;

        public string SslServerCertificatePassphrase { get; set; } = string.Empty;

        public string CampaignApiKeyMappingName { get; set; } = string.Empty;

        public string SessionPasswordKey { get; set; } = string.Empty;

        public string PostmanBaseUrl { get; set; } = string.Empty;

        public string LogLevelString { get; set; } = string.Empty;

        public LogLevel MinimumLogLevel
        {
            get
            {
                return LogLevelString switch
                {
                    "info" => LogLevel.Information, 
                    "debug" => LogLevel.Debug, 
                    "warn" => LogLevel.Warning,
                    "error" => LogLevel.Error,
                    _ => LogLevel.Error,
                };
            }
        }
    }
}
