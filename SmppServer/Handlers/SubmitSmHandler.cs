using Smpp.Server.Constants;
using Smpp.Server.Factories;
using Smpp.Server.Interfaces;
using Smpp.Server.Models;

namespace Smpp.Server.Handlers;

public class SubmitSmHandler : IPduHandler
{
    private readonly ILogger<SubmitSmHandler> _logger;
    private readonly IMessageConcatenationService _concatenationService;
    private readonly IMessageProcessor _messageProcessor;

    public SubmitSmHandler(
        ILogger<SubmitSmHandler> logger,
        IMessageConcatenationService concatenationService,
        IMessageProcessor messageProcessor)
    {
        _logger = logger;
        _concatenationService = concatenationService;
        _messageProcessor = messageProcessor;
    }

    public Task<bool> CanHandle(SmppPdu pdu) 
        => Task.FromResult(pdu.CommandId == SmppConstants.SmppCommandId.SubmitSm);

    public async Task<SmppPdu?> Handle(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken)
    {
        try
        {
            var submitSm = SmppPduFactory.CreateSubmitSm(pdu);
            var messageId = GenerateMessageId();

            _logger.LogInformation("📱 Submit_SM - From: '{SourceAddress}' To: '{DestinationAddress}' MessageId: {MessageId}", 
                submitSm.SourceAddress, submitSm.DestinationAddress, messageId);

            // Handle message concatenation
            var concatenationResult = await _concatenationService.ProcessMessagePartAsync(submitSm);
            
            // Always send immediate acknowledgment
            var response = SmppResponseBuilder.Create()
                .AsSubmitSmResponse(pdu.SequenceNumber, messageId)
                .Build();

            // Process complete messages asynchronously
            if (concatenationResult.IsComplete)
            {
                _ = Task.Run(async () => 
                {
                    try
                    {
                        await _messageProcessor.ProcessCompleteMessageAsync(
                            submitSm.SourceAddress, 
                            submitSm.DestinationAddress, 
                            concatenationResult.CompleteMessage!,
                            session,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing complete message for {MessageId}", messageId);
                    }
                }, cancellationToken);

                _logger.LogInformation("Complete message processed - From: '{SourceAddress}' To: '{DestinationAddress}' Message: '{Message}'",
                    submitSm.SourceAddress, submitSm.DestinationAddress, concatenationResult.CompleteMessage);
            }
            else
            {
                _logger.LogInformation("Message part received, waiting for more parts - MessageId: {MessageId}", messageId);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing submit_sm");
            
            return SmppResponseBuilder.Create()
                .WithCommandId(SmppConstants.SmppCommandId.SubmitSmResp)
                .WithSequenceNumber(pdu.SequenceNumber)
                .AsError(SmppConstants.SmppCommandStatus.ESME_RSYSERR)
                .Build();
        }
    }

    private static string GenerateMessageId() => Guid.NewGuid().ToString("N")[..10];

}