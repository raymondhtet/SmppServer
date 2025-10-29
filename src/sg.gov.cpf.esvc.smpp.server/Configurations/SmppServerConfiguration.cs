namespace sg.gov.cpf.esvc.smpp.server.Configurations;

public class SmppServerConfiguration
{
    public int Port { get; set; } = 2775;
    
    public int MaxConcurrentConnections { get; set; } = 10;

    public string StaleCleanUpInterval { get; set; } = "00:01:00";
    
    public string CleanUpJobInterval { get; set; } = "00:01:30";

    public string CacheDuration { get; set; } = "01:00:00";

    public string SystemId { get; set; } = "";
}