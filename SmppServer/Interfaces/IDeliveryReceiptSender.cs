using Smpp.Server.Models;

namespace Smpp.Server.Interfaces;

public interface IDeliveryReceiptSender
{
    Task SendAsync(ISmppSession session, string messageId, DeliveryStatus status);

}