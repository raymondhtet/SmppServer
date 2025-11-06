using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Factories;
using sg.gov.cpf.esvc.smpp.server.Helpers;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using System.Diagnostics;

namespace sg.gov.cpf.esvc.smpp.server.Handlers;

public class SubmitSmHandler(
    ILogger<SubmitSmHandler> logger,
    IMessageConcatenationService concatenationService,
    IMessageProcessor messageProcessor,
    TelemetryClient telemetryClient)
    : IPduHandler
{
    public Task<bool> CanHandle(SmppPdu pdu)
        => Task.FromResult(pdu.CommandId == SmppConstants.SmppCommandId.SubmitSm);

    public async Task<SmppPdu?> Handle(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();

            var messageId = GenerateMessageId();
            using var _ = telemetryClient.StartOperation<RequestTelemetry>(nameof(SubmitSmHandler));
            telemetryClient.Context.Operation.Id = messageId;

            telemetryClient.TrackTrace($"Received incoming request from ACS with message id: {messageId}");

            var submitSm = SmppPduFactory.CreateSubmitSm(pdu);
            
            /*
            logger.LogInformation(
                "Submit_SM - To: '{DestinationAddress}' MessageId: {MessageId}, Short Message:{ShortMessage}, Message Payload: {MessagePayload}," +
                "Short Message Hex:{ShortMessageHex}, Message Payload Hex:{MessagePayloadHex}",
                submitSm.DestinationAddress, messageId, Encoding.UTF8.GetString(submitSm.ShortMessage), Encoding.UTF8.GetString(submitSm.MessagePayload),
                Convert.ToHexString(submitSm.ShortMessage), Convert.ToHexString(submitSm.MessagePayload));
            
            logger.LogInformation("Submit_SM - Body Hex:{BodyHex}", Convert.ToHexString(pdu.Body!));
            */

            var (isMultipartMessage, concatInfo, messageContent) = ConcatenationHelper.GetConcatenationInfo(submitSm);

            //logger.LogInformation("(Length: {Length}) and Content:{Content} and Campaign ID:{CampaignID}", messageContent.Length, messageContent, submitSm.CampaignId);

            var concatenationResult =
                await concatenationService.ProcessMessagePartAsync(concatInfo, isMultipartMessage, messageContent,
                    submitSm);

            // Always send immediate acknowledgment
            var response = SmppResponseBuilder.Create()
                .AsSubmitSmResponse(pdu.SequenceNumber, messageId)
                .Build();

            if (concatenationResult.IsComplete)
            {
                logger.LogInformation("(Complete Message Length: {Length}) and Complete Message Content:'{Content}'", concatenationResult.CompleteMessage!.Length, concatenationResult.CompleteMessage);
                
                await messageProcessor.ProcessCompleteMessageAsync(
                    submitSm.SourceAddress,
                    submitSm.DestinationAddress,
                    concatenationResult.CompleteMessage!,
                    submitSm.CampaignId,
                    messageId,
                    session,
                    cancellationToken);
            }

            stopwatch.Stop();
            telemetryClient.TrackTrace($"Total execution time for message id ({messageId}) is {stopwatch.Elapsed.TotalSeconds} seconds");

            return response;
        }
        catch (Exception ex)
        {
            logger.LogInformation("Error processing submit_sm with body hex: {BodyHex}", Convert.ToHexString(pdu.Body!));
            logger.LogError(ex, "Error processing submit_sm");

            return SmppResponseBuilder.Create()
                .WithCommandId(SmppConstants.SmppCommandId.SubmitSmResp)
                .WithSequenceNumber(pdu.SequenceNumber)
                .AsError(SmppConstants.SmppCommandStatus.ESME_RSYSERR)
                .Build();
        }
    }

    private static string GenerateMessageId() => Guid.NewGuid().ToString("N")[..10];
    
}