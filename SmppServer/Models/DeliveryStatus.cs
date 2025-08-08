using static Smpp.Server.Constants.SmppConstants;

namespace Smpp.Server.Models;

public class DeliveryStatus
{
    public MessageState MessageState { get; set; }
    public string? ErrorStatus { get; set; }
    public string? ErrorCode { get; set; }
}