namespace Smpp.Server.Models.AppSettings;

public class SmppServerConfiguration
{
    public int Port { get; set; } = 2775;
    public string? SessionUsername { get; set; }

    public string? SessionPassword { get; set; }
    public int MaxConcurrentConnections { get; set; } = 10;

    public string StaleCleanUpInterval { get; set; } = "00:01:00";
    
    public string CleanUpJobInterval { get; set; } = "00:01:30";
}