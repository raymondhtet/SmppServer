using Smpp.Server.Models;

namespace Smpp.Server.Helpers;

public static class DeliveryStatusHelper
{
    public static DeliveryStatus Delivered() => new()
    {
        MessageState = Constants.SmppConstants.MessageState.DELIVERED,
        ErrorStatus = "DELIVRD",
        ErrorCode = "000"
    };

    public static DeliveryStatus Failed(string? errorCode = null) => new()
    {
        MessageState = Constants.SmppConstants.MessageState.UNDELIVERABLE,
        ErrorStatus = "UNDELIV",
        ErrorCode = errorCode ?? "001"
    };

    public static DeliveryStatus Undeliverable() => new()
    {
        MessageState = Constants.SmppConstants.MessageState.UNDELIVERABLE,
        ErrorStatus = "UNDELIV",
        ErrorCode = "005"
    };

    public static DeliveryStatus Rejected() => new()
    {
        MessageState = Constants.SmppConstants.MessageState.REJECTED,
        ErrorStatus = "REJECTD",
        ErrorCode = "004"
    };

}