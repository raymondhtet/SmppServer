namespace Smpp.Server.Models;

public class MessagePartState
{
    public int TotalParts { get; set; }
    public Dictionary<int, byte[]> ReceiveParts { get; set; } = new();
    public string? SourceAddress { get; set; }
    public string? DestinationAddress { get; set; }
    public DateTime FirstPartReceived { get; set; }
    public bool IsComplete => ReceiveParts.Count == TotalParts;

}