using sg.gov.cpf.esvc.smpp.server.Models;

namespace sg.gov.cpf.esvc.smpp.server.Interfaces;

public interface IDeliveryReceiptSender
{
    /// <summary>
    /// Send enhanced delivery receipt with full addressing
    /// </summary>
    Task SendDeliveryReceiptAsync(
        ISmppSession session,
        string sourceAddress,
        String destinationAddress,
        string? messageId,
        DeliveryStatus status,
        DateTime? submitDate = null,
        DateTime? doneDate = null);

}