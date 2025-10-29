using sg.gov.cpf.esvc.smpp.server.Constants;
using System.Collections.Concurrent;

namespace sg.gov.cpf.esvc.smpp.server.Models;

public class MessageTracker(ILogger<MessageTracker> logger)
{
    private readonly ConcurrentDictionary<string, MessagePartState> _messageStates = new();

    private string GenerateMessagePartKey(ushort referenceNumber, string sourceAddress, string destinationAddress) =>
        $"{referenceNumber}_{sourceAddress}_{destinationAddress}";

    public (bool IsComplete, string? CompleteMessage) TrackMessageParts(
        SmppConstants.ConcatenationInfo? concatInfo,
        bool isMultipartMessage,
        string message,
        string sourceAddress,
        string destinationAddress
    )
    {
        if (!isMultipartMessage) return (true, message);
        
        var multipartInfo = concatInfo!.Value;
        var messagePartKey =
            GenerateMessagePartKey(multipartInfo.ReferenceNumber, sourceAddress, destinationAddress);

        var state = _messageStates.GetOrAdd(messagePartKey, _ => new MessagePartState
        {
            TotalParts = multipartInfo.TotalParts,
            SourceAddress = sourceAddress,
            DestinationAddress = destinationAddress,
            FirstPartReceived = DateTime.UtcNow
        });

        // Extract message content from UDH message
        state.ReceiveParts[multipartInfo.PartNumber] = message;

        if (state.IsComplete)
        {
            string completeMessage = CombineMessageParts(state);
            _messageStates.TryRemove(messagePartKey, out _);
            /*
            logger.LogInformation(
                "Completed UDH multipart message {MessagePartKey} (Total Length: {TotalLength}): {CompleteMessage}",
                messagePartKey, completeMessage.Length, completeMessage);
            */
            return (true, completeMessage);
        }

        return (false, null);
    }

    public void CleanUpStaleMessagesParts(TimeSpan timeout)
    {
        var staleTime = DateTime.UtcNow - timeout;
        var staleKeys = _messageStates
            .Where(x => x.Value.FirstPartReceived < staleTime)
            .Select(x => x.Key)
            .ToList();

        foreach (var key in staleKeys)
        {
            if (_messageStates.TryRemove(key, out var removedState))
            {
                logger.LogWarning(
                    "Removed stale incomplete message {MessagePartKey} with {ReceivedParts}/{TotalParts} parts",
                    key, removedState.ReceiveParts.Count, removedState.TotalParts);
            }
        }

        if (staleKeys.Count > 0)
        {
            logger.LogInformation("Cleaned up {StaleCount} stale message parts", staleKeys.Count);
        }
    }
    
    private static string CombineMessageParts(MessagePartState state)
    {
        var orderedParts = state.ReceiveParts
            .OrderBy(p => p.Key)
            .Select(x => x.Value)
            .ToArray();

        return string.Join("", orderedParts);
    }

}

// Supporting classes
public struct UdhConcatenationInfo
{
    public ushort ReferenceNumber { get; set; }
    public byte TotalParts { get; set; }
    public byte PartNumber { get; set; }
    public int UdhLength { get; set; }
}

public struct SarConcatenationInfo
{
    public ushort ReferenceNumber { get; set; }
    public byte TotalParts { get; set; }
    public byte PartNumber { get; set; }
}