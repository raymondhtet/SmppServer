using System.Text;
using Smpp.Server.Constants;
using Smpp.Server.Interfaces;
using Smpp.Server.Models;

namespace Smpp.Server.Services;

public class DeliveryReceiptSender : IDeliveryReceiptSender
{
    private readonly ILogger<DeliveryReceiptSender> _logger;
    private uint _sequenceNumber;

    public DeliveryReceiptSender(ILogger<DeliveryReceiptSender> logger)
    {
        _logger = logger;
    }

    public async Task SendAsync(ISmppSession session, string messageId, DeliveryStatus status)
    {
        var shortMessage = $"id:{messageId} stat:{status.ErrorStatus} err:{status.ErrorCode}";

        var deliverSm = SmppResponseBuilder.Create()
            .WithCommandId(SmppConstants.SmppCommandId.DeliverSm)
            .WithSequenceNumber(GetNextSequenceNumber())
            .AsSuccess()
            .WithBody(Encoding.ASCII.GetBytes(shortMessage))
            .Build();

        await session.SendPduAsync(deliverSm);
        
        _logger.LogInformation("📧 Sent delivery receipt for message {MessageId}: {Status}", messageId, status.ErrorStatus);
    }

    private uint GetNextSequenceNumber() => Interlocked.Increment(ref _sequenceNumber);
}