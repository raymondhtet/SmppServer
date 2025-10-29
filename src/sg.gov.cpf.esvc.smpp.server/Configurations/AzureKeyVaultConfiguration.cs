using Azure.Core;

namespace sg.gov.cpf.esvc.smpp.server.Configurations
{
    public class AzureKeyVaultConfiguration
    {
        public RetryOptions RetryOptions { get; set; }
    }

    public class RetryOptions
    {
        public int Delay { get; set; }
        public int MaxDelay { get; set; }
        public int MaxRetries { get; set; }
        public string Mode { get; set; }
    }
}
