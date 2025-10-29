using sg.gov.cpf.esvc.smpp.server.Models;

namespace sg.gov.cpf.esvc.smpp.server.Helpers;

public static class DeliveryStatusHelper
{
    public static DeliveryStatus Delivered() => new()
    {
        MessageState = Constants.SmppConstants.MessageState.DELIVERED,
        ErrorStatus = "DELIV",
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
        ErrorStatus = "REJECT",
        ErrorCode = "004"
    };

}