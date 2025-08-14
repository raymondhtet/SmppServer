using Smpp.Server.Interfaces;
using Smpp.Server.Models;
using Smpp.Server.Models.DTOs;

namespace Smpp.Server.Services;

public class MessageConcatenationService : IMessageConcatenationService
{
    private readonly MessageTracker _messageTracker;
    private readonly ILogger<MessageConcatenationService> _logger;

    public MessageConcatenationService(MessageTracker messageTracker, ILogger<MessageConcatenationService> logger)
    {
        _messageTracker = messageTracker;
        _logger = logger;
    }

    public async Task<ConcatenationResult> ProcessMessagePartAsync(SubmitSmRequest request)
    {
        // Convert SubmitSmRequest back to SmppPdu for compatibility with existing MessageTracker
        var pdu = ConvertToSmppPdu(request);
        
        var (isComplete, completeMessage) = _messageTracker.TrackMessageParts(
            pdu, 
            request.SourceAddress, 
            request.DestinationAddress);

        return new ConcatenationResult(isComplete, completeMessage);
    }

    private static SmppPdu ConvertToSmppPdu(SubmitSmRequest request)
    {
        // Create a SmppPdu from SubmitSmRequest for compatibility
        return new SmppPdu
        {
            Body = request.ShortMessage,
            OptionalParameters = request.OptionalParameters
        };
    }
}