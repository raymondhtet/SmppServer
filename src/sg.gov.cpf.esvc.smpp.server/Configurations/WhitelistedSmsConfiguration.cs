namespace sg.gov.cpf.esvc.smpp.server.Configurations
{
    public class WhitelistedSmsConfiguration
    {
        public List<WhitelistedInfo> WhitelistedMobileNumbers { get; set; } = [];
    }

    public class WhitelistedInfo
    {
        public string? MobileNumber { get; set; }

        public int Delay { get; set; }

        public bool IsSentSMS { get; set; }
    }
}
