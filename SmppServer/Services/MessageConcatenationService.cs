using System.Text;
using Smpp.Server.Constants;
using Smpp.Server.Helpers;
using Smpp.Server.Interfaces;
using Smpp.Server.Models;
using Smpp.Server.Models.DTOs;

namespace Smpp.Server.Services;

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