using System.Text;
using Smpp.Server.Constants;
using Smpp.Server.Factories;
using Smpp.Server.Helpers;
using Smpp.Server.Interfaces;
using Smpp.Server.Models;

namespace Smpp.Server.Handlers;

public class SubmitSmHandler(
    ILogger<SubmitSmHandler> logger,
    IMessageConcatenationService concatenationService,
    IMessageProcessor messageProcessor)
    : IPduHandler
{
    public Task<bool> CanHandle(SmppPdu pdu)
        => Task.FromResult(pdu.CommandId == SmppConstants.SmppCommandId.SubmitSm);

    public async Task<SmppPdu?> Handle(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken)
    {
        try
        {
            var submitSm = SmppPduFactory.CreateSubmitSm(pdu);
            var messageId = GenerateMessageId();

            logger.LogInformation(
                "📱 Submit_SM - From: '{SourceAddress}' To: '{DestinationAddress}' MessageId: {MessageId}",
                submitSm.SourceAddress, submitSm.DestinationAddress, messageId);

            var (isMultipartMessage, concatInfo, messageContent) = ConcatenationHelper.GetConcatenationInfo(submitSm);

            logger.LogInformation("(Length: {Length}) and Content:{Content}", messageContent.Length, messageContent);

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
                    session,
                    cancellationToken);
            }


            return response;
        }
        catch (Exception ex)
        {
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