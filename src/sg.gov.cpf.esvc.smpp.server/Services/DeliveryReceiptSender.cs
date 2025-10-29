using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using System.Text;

namespace sg.gov.cpf.esvc.smpp.server.Services;

public class DeliveryReceiptSender(ILogger<DeliveryReceiptSender> logger, TelemetryClient telemetryClient) : IDeliveryReceiptSender
{
    private uint _sequenceNumber;


    public async Task SendDeliveryReceiptAsync(ISmppSession session, string sourceAddress, string destinationAddress, string? messageId,
        DeliveryStatus status, DateTime? submitDate = null, DateTime? doneDate = null)
    {
        using var _ = telemetryClient.StartOperation<DependencyTelemetry>(nameof(DeliveryReceiptSender));
        telemetryClient.Context.Operation.Id = messageId;

        var deliverSm = CreateDeliverSmPdu(
            sourceAddress,
            destinationAddress,
            messageId,
            status,
            submitDate ?? DateTime.UtcNow,
            doneDate ?? DateTime.UtcNow);

        await session.SendPduAsync(deliverSm);

        logger.LogInformation("Deliver_SM Hex: {Hex}", Convert.ToHexString(deliverSm.GetBytes()));

        telemetryClient.TrackTrace($"Delivery receipt sent for message {messageId}: {status.ErrorStatus}, {status.ErrorCode}, to {destinationAddress.Substring(6)}");
    }

    /// <summary>
    /// Create enhanced deliver_sm PDU with proper addressing
    /// </summary>
    private SmppPdu CreateDeliverSmPdu(
        string sourceAddress,
        string destinationAddress,
        string? messageId,
        DeliveryStatus status,
        DateTime submitDate,
        DateTime doneDate)
    {
        // Create delivery receipt message
        var shortMessage = $"id:{messageId} stat:{status.ErrorStatus} err:{status.ErrorCode}";
        var shortMessageBytes = Encoding.ASCII.GetBytes(shortMessage);

        var body = new List<byte>();

        // service_type
        body.AddRange(Encoding.ASCII.GetBytes(""));
        body.Add(0x00);

        // source_addr_ton, source_addr_npi
        body.Add(0x00); // SMSC TON
        body.Add(0x00); // SMSC NPI

        // source_addr - Use original destination as source (message was delivered TO this number)
        body.AddRange(Encoding.ASCII.GetBytes(destinationAddress));
        body.Add(0x00);

        // dest_addr_ton, dest_addr_npi
        body.Add(0x01); // International TON
        body.Add(0x01); // ISDN NPI

        // destination_addr - Use original source (deliver receipt TO the sender)
        body.AddRange(Encoding.ASCII.GetBytes(sourceAddress));
        body.Add(0x00);

        // esm_class - Set delivery receipt flag
        body.Add((byte)SmppConstants.EsmClass.MC_DELIVERY_RECEIPT);

        // protocol_id
        body.Add(0x00);

        // priority_flag
        body.Add(0x00);

        // schedule_delivery_time
        body.Add(0x00);

        // validity_period  
        body.Add(0x00);

        // registered_delivery
        body.Add(0x00);

        // replace_if_present_flag
        body.Add(0x00);

        // data_coding
        body.Add(0x00);

        // sm_default_msg_id
        body.Add(0x00);

        // sm_length and short_message
        body.Add((byte)shortMessageBytes.Length);
        body.AddRange(shortMessageBytes);

        // Optional parameters for enhanced delivery receipts
        //var optionalParams = CreateOptionalParameters(messageId, status);
        //body.AddRange(optionalParams);

        return new SmppPdu
        {
            CommandLength = (uint)(16 + body.Count),
            CommandId = SmppConstants.SmppCommandId.DeliverSm,
            CommandStatus = SmppConstants.SmppCommandStatus.ESME_ROK,
            SequenceNumber = GetNextSequenceNumber(),
            Body = body.ToArray()
        };
    }

    /// <summary>
    /// Create optional parameters for delivery receipt
    /// </summary>
    private static byte[] CreateOptionalParameters(string messageId, DeliveryStatus status)
    {
        var optionalParams = new List<byte>();

        // RECEIPTED_MESSAGE_ID (0x001E)
        optionalParams.Add(0x00); optionalParams.Add(0x1E); // tag
        var messageIdBytes = Encoding.ASCII.GetBytes(messageId);
        optionalParams.Add(0x00); optionalParams.Add((byte)messageIdBytes.Length); // length
        optionalParams.AddRange(messageIdBytes); // value

        // MESSAGE_STATE (0x0427)
        optionalParams.Add(0x04); optionalParams.Add(0x27); // tag
        optionalParams.Add(0x00); optionalParams.Add(0x01); // length = 1
        optionalParams.Add((byte)status.MessageState); // message state value

        return optionalParams.ToArray();
    }

    private uint GetNextSequenceNumber() => Interlocked.Increment(ref _sequenceNumber);
}