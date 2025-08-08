using System.Collections.Concurrent;
using System.Text;
using Smpp.Server.Constants;
using Smpp.Server.Helpers;

namespace Smpp.Server.Models;

public class MessageTracker(ILogger<MessageTracker> logger)
{
    private readonly ConcurrentDictionary<string, MessagePartState> _messageStates = new();

    private string GenerateMessagePartKey(ushort referenceNumber, string sourceAddress, string destinationAddress) =>
        $"{referenceNumber}_{sourceAddress}_{destinationAddress}";

    public (bool IsComplete, string? CompleteMessage) TrackMessageParts(
        SmppPdu pdu, 
        string sourceAddress,
        string destinationAddress)
    {
        
        if (TryGetSegmentInfo(pdu, out var segmentInfo))
        {
            var (message, _) = MessageParser.ExtractMessageFromPdu(pdu);
            return (true, message);
        }
        
        var (referenceNumber, totalSegments, sequenceNumber) = segmentInfo;
        var messagePartKey = GenerateMessagePartKey(referenceNumber, sourceAddress, destinationAddress);
        
        var state = _messageStates.GetOrAdd(messagePartKey, _ => new MessagePartState
        {
            TotalParts = totalSegments,
            SourceAddress = sourceAddress,
            DestinationAddress = destinationAddress,
            FirstPartReceived = DateTime.UtcNow
        });
        
        state.ReceiveParts[sequenceNumber] = pdu.Body!;
        
        logger.LogInformation("Received segment {SequenceNumber}/{TotalSegments} for message {MessagePartKey}",
            sequenceNumber, totalSegments, messagePartKey);

        if (state.IsComplete)
        {
            string completeMessage = CombineMessageParts(state);
            logger.LogInformation("Completed message {MessagePartKey} with {TotalParts} parts", 
                messagePartKey,
                completeMessage);
            
            return (true, completeMessage);
        }
        
        return (false, null);
    }

    private static string CombineMessageParts(MessagePartState state)
    {
        var orderedParts = state.ReceiveParts
                                                            .OrderBy(p => p.Key)
                                                            .Select(x => x.Value)
                                                            .ToList();

        StringBuilder combinedMessage = new();
        foreach (var part in orderedParts)
        {
            var (message, _) = MessageParser.ExtractMessageFromPdu(new SmppPdu() { Body = part });
            combinedMessage.Append(message);
        }
        
        return combinedMessage.ToString();
    }

    private static bool TryGetSegmentInfo(SmppPdu pdu, out (ushort ReferenceNumber, byte TotalSegements, byte SequenceNum) segmentInfo)
    {
        segmentInfo = default;
        
        if (!pdu.OptionalParameters.TryGetValue(SmppPdu.OptionalParameterTags.SAR_MSG_REF_NUM, out var referenceNumberBytes) ||
            !pdu.OptionalParameters.TryGetValue(SmppPdu.OptionalParameterTags.SAR_TOTAL_SEGMENTS, out var totalBytes) ||
            !pdu.OptionalParameters.TryGetValue(SmppPdu.OptionalParameterTags.SAR_SEGMENT_SEQNUM, out var sequenceBytes))
        {
            return false;
        }
                
        var referenceNumber = BitConverter.ToUInt16(referenceNumberBytes.Reverse().ToArray());
        var totalSegments = totalBytes[0];
        var sequenceNum = sequenceBytes[0];
        
        segmentInfo = (referenceNumber, totalSegments, sequenceNum);
        
        return true;
    }

    public void CleanUpStaleMessagesParts(TimeSpan timeout)
    {
        var staleTime = DateTime.UtcNow - timeout;
        var staleKeys = _messageStates
                                    .Where(x => x.Value.FirstPartReceived < staleTime)
                                    .Select(x => x.Key).ToList();

        foreach (var key in staleKeys)
        {
            _messageStates.TryRemove(key, out _);
            logger.LogInformation("Removed stale message {MessagePartKey}", key);
        }
    }
}