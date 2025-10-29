using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using sg.gov.cpf.esvc.smpp.server.Models.DTOs;

namespace sg.gov.cpf.esvc.smpp.server.Services;

public class MessageConcatenationService(MessageTracker messageTracker, ILogger<MessageConcatenationService> logger)
    : IMessageConcatenationService
{
    private readonly ILogger<MessageConcatenationService> _logger = logger;

    public Task<ConcatenationResult> ProcessMessagePartAsync(
        SmppConstants.ConcatenationInfo? concatInfo,
        bool isMultipartMessage,
        string message,
        SubmitSmRequest request)
    {
        var (isComplete, completeMessage) = messageTracker.TrackMessageParts(
            concatInfo,
            isMultipartMessage,
            message,
            request.SourceAddress,
            request.DestinationAddress);

        return Task.FromResult(new ConcatenationResult(isComplete, completeMessage));
    }
}