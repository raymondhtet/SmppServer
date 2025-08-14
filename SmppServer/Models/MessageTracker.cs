using System.Collections.Concurrent;
using System.Text;
using Smpp.Server.Constants;
using Smpp.Server.Helpers;

namespace Smpp.Server.Models;

public abstract class MessageTracker(ILogger<MessageTracker> logger)
{
    private readonly ConcurrentDictionary<string, MessagePartState> _messageStates = new();

    private string GenerateMessagePartKey(ushort referenceNumber, string sourceAddress, string destinationAddress) =>
        $"{referenceNumber}_{sourceAddress}_{destinationAddress}";

    public (bool IsComplete, string? CompleteMessage) TrackMessageParts(
        SmppPdu pdu, 
        string sourceAddress,
        string destinationAddress)
    {
        // SMPP v3.5 FIX: Check for UDH concatenation first (most common in v3.5)
        if (TryGetUdhConcatenationInfo(pdu, out var udhInfo))
        {
            logger.LogInformation("UDH Multipart message detected - Ref:{ReferenceNumber}, Part:{PartNumber}/{TotalParts}", 
                udhInfo.ReferenceNumber, udhInfo.PartNumber, udhInfo.TotalParts);
                
            var messagePartKey = GenerateMessagePartKey(udhInfo.ReferenceNumber, sourceAddress, destinationAddress);
            
            var state = _messageStates.GetOrAdd(messagePartKey, _ => new MessagePartState
            {
                TotalParts = udhInfo.TotalParts,
                SourceAddress = sourceAddress,
                DestinationAddress = destinationAddress,
                FirstPartReceived = DateTime.UtcNow
            });
            
            // Extract message content from UDH message
            var messageContent = ExtractMessageFromUdhPdu(pdu, udhInfo.UdhLength);
            state.ReceiveParts[udhInfo.PartNumber] = Encoding.UTF8.GetBytes(messageContent);
            
            logger.LogInformation("Received UDH segment {PartNumber}/{TotalParts} for message ({MessagePartKey}): '{MessageContent}' (Length: {Length})",
                udhInfo.PartNumber, udhInfo.TotalParts, messagePartKey, messageContent, messageContent.Length);

            if (state.IsComplete)
            {
                string completeMessage = CombineMessageParts(state);
                _messageStates.TryRemove(messagePartKey, out _);
                
                logger.LogInformation("Completed UDH multipart message {MessagePartKey} (Total Length: {TotalLength}): {CompleteMessage}", 
                    messagePartKey, completeMessage.Length, completeMessage);
                
                return (true, completeMessage);
            }
            
            logger.LogInformation("Still waiting for {RemainingParts} more UDH parts for message {MessagePartKey}", 
                udhInfo.TotalParts - state.ReceiveParts.Count, messagePartKey);
            
            return (false, null);
        }
        
        // Check for SAR optional parameters (less common but still supported)
        if (TryGetSarConcatenationInfo(pdu, out var sarInfo))
        {
            logger.LogInformation("SAR Multipart message detected - Ref:{ReferenceNumber}, Part:{PartNumber}/{TotalParts}", 
                sarInfo.ReferenceNumber, sarInfo.PartNumber, sarInfo.TotalParts);
                
            var messagePartKey = GenerateMessagePartKey(sarInfo.ReferenceNumber, sourceAddress, destinationAddress);
            
            var state = _messageStates.GetOrAdd(messagePartKey, _ => new MessagePartState
            {
                TotalParts = sarInfo.TotalParts,
                SourceAddress = sourceAddress,
                DestinationAddress = destinationAddress,
                FirstPartReceived = DateTime.UtcNow
            });
            
            var (partMessage, _) = MessageParser.ExtractMessageFromPdu(pdu);
            state.ReceiveParts[sarInfo.PartNumber] = Encoding.UTF8.GetBytes(partMessage);
            
            logger.LogInformation("Received SAR segment {PartNumber}/{TotalParts} for message {MessagePartKey}: '{PartMessage}'",
                sarInfo.PartNumber, sarInfo.TotalParts, messagePartKey, partMessage);

            if (state.IsComplete)
            {
                string completeMessage = CombineMessageParts(state);
                _messageStates.TryRemove(messagePartKey, out _);
                
                logger.LogInformation("Completed SAR multipart message {MessagePartKey}: '{CompleteMessage}'", 
                    messagePartKey, completeMessage);
                
                return (true, completeMessage);
            }
            
            return (false, null);
        }
        
        // Single part message
        var (message, _) = MessageParser.ExtractMessageFromPdu(pdu);
        logger.LogInformation("Single part message received: '{Message}' (Length: {Length})", message, message.Length);
        return (true, message);
    }

    // NEW: UDH Concatenation Detection (SMPP v3.5 standard)
    private bool TryGetUdhConcatenationInfo(SmppPdu pdu, out UdhConcatenationInfo udhInfo)
    {
        udhInfo = default;
        
        if (pdu.Body == null || pdu.Body.Length < 20)
            return false;

        try
        {
            // Parse ESM_CLASS to check for UDH indicator (bit 6 = 0x40)
            var esmClassOffset = GetEsmClassOffset(pdu);
            if (esmClassOffset == -1 || esmClassOffset >= pdu.Body.Length)
                return false;
                
            var esmClass = pdu.Body[esmClassOffset];
            logger.LogDebug("ESM_CLASS: 0x{EsmClass:X2}", esmClass);
            
            // Check if UDH indicator is set (bit 6)
            if ((esmClass & 0x40) == 0)
            {
                logger.LogDebug("No UDH indicator in ESM_CLASS");
                return false;
            }
            
            // Get short message data
            var shortMessageOffset = GetShortMessageOffset(pdu);
            if (shortMessageOffset == -1 || shortMessageOffset >= pdu.Body.Length)
                return false;
                
            var smLength = pdu.Body[shortMessageOffset];
            if (smLength == 0 || shortMessageOffset + 1 + smLength > pdu.Body.Length)
                return false;
                
            var shortMessage = new byte[smLength];
            Array.Copy(pdu.Body, shortMessageOffset + 1, shortMessage, 0, smLength);
            
            logger.LogInformation("Short message length: {Length}, First bytes: {Bytes}", 
                smLength, string.Join(" ", shortMessage.Take(Math.Min(10, (int)smLength)).Select(b => $"0x{b:X2}")));
            
            // Parse UDH
            if (shortMessage.Length < 1)
                return false;
                
            var udhLength = shortMessage[0];
            logger.LogInformation("UDH Length: {UdhLength}", udhLength);
            
            if (udhLength == 0 || udhLength + 1 > shortMessage.Length)
                return false;
            
            // Look for concatenation Information Element (IEI = 0x00 for 8-bit ref, 0x08 for 16-bit ref)
            var offset = 1;
            while (offset < udhLength)
            {
                if (offset + 1 >= shortMessage.Length)
                    break;
                    
                var iei = shortMessage[offset++];
                var iedl = shortMessage[offset++];
                
                logger.LogDebug("Found IEI: 0x{IEI:X2}, IEDL: {IEDL}", iei, iedl);
                
                if (offset + iedl > shortMessage.Length)
                    break;
                
                if (iei == 0x00 && iedl == 3) // 8-bit reference number
                {
                    var refNum = shortMessage[offset];
                    var totalParts = shortMessage[offset + 1];
                    var partNum = shortMessage[offset + 2];
                    
                    logger.LogInformation("Found 8-bit UDH concatenation: Ref={RefNum}, Part={PartNum}/{TotalParts}", 
                        refNum, partNum, totalParts);
                    
                    udhInfo = new UdhConcatenationInfo
                    {
                        ReferenceNumber = refNum,
                        TotalParts = totalParts,
                        PartNumber = partNum,
                        UdhLength = udhLength
                    };
                    return true;
                }
                else if (iei == 0x08 && iedl == 4) // 16-bit reference number
                {
                    var refNum = (ushort)((shortMessage[offset] << 8) | shortMessage[offset + 1]);
                    var totalParts = shortMessage[offset + 2];
                    var partNum = shortMessage[offset + 3];
                    
                    logger.LogInformation("Found 16-bit UDH concatenation: Ref={RefNum}, Part={PartNum}/{TotalParts}", 
                        refNum, partNum, totalParts);
                    
                    udhInfo = new UdhConcatenationInfo
                    {
                        ReferenceNumber = refNum,
                        TotalParts = totalParts,
                        PartNumber = partNum,
                        UdhLength = udhLength
                    };
                    return true;
                }
                
                offset += iedl;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing UDH concatenation info");
        }
        
        return false;
    }
    
    // Extract message content after UDH
    private string ExtractMessageFromUdhPdu(SmppPdu pdu, int udhLength)
    {
        try
        {
            var shortMessageOffset = GetShortMessageOffset(pdu);
            if (shortMessageOffset == -1)
                return "";
                
            var smLength = pdu.Body![shortMessageOffset];
            var messageDataStart = shortMessageOffset + 1 + udhLength + 1; // +1 for UDH length byte
            var messageDataLength = smLength - udhLength - 1;
            
            if (messageDataStart >= pdu.Body.Length || messageDataLength <= 0)
                return "";
            
            var messageData = new byte[Math.Min(messageDataLength, pdu.Body.Length - messageDataStart)];
            Array.Copy(pdu.Body, messageDataStart, messageData, 0, messageData.Length);
            
            // Try different encodings based on data_coding
            var dataCodingOffset = GetDataCodingOffset(pdu);
            var dataCoding = dataCodingOffset != -1 ? pdu.Body[dataCodingOffset] : 0;
            
            return dataCoding switch
            {
                0x08 => Encoding.BigEndianUnicode.GetString(messageData), // UCS2
                _ => Encoding.UTF8.GetString(messageData) // Default to UTF-8
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting message from UDH PDU");
            return "";
        }
    }
    
    // Helper methods to find field offsets in PDU body
    private int GetEsmClassOffset(SmppPdu pdu)
    {
        try
        {
            var offset = 0;
            
            // Skip service_type (null-terminated)
            while (offset < pdu.Body!.Length && pdu.Body[offset] != 0) offset++;
            offset++; // Skip null terminator
            
            // Skip source_addr_ton, source_addr_npi
            offset += 2;
            
            // Skip source_addr (null-terminated)
            while (offset < pdu.Body.Length && pdu.Body[offset] != 0) offset++;
            offset++;
            
            // Skip dest_addr_ton, dest_addr_npi  
            offset += 2;
            
            // Skip destination_addr (null-terminated)
            while (offset < pdu.Body.Length && pdu.Body[offset] != 0) offset++;
            offset++;
            
            // Now at esm_class
            return offset < pdu.Body.Length ? offset : -1;
        }
        catch
        {
            return -1;
        }
    }
    
    private int GetDataCodingOffset(SmppPdu pdu)
    {
        var esmOffset = GetEsmClassOffset(pdu);
        if (esmOffset == -1) return -1;
        
        // esm_class + protocol_id + priority_flag + schedule_delivery_time + validity_period + registered_delivery + replace_if_present_flag + data_coding
        var offset = esmOffset + 1 + 1 + 1; // esm_class + protocol_id + priority_flag
        
        // Skip schedule_delivery_time (null-terminated)
        while (offset < pdu.Body!.Length && pdu.Body[offset] != 0) offset++;
        offset++;
        
        // Skip validity_period (null-terminated)  
        while (offset < pdu.Body.Length && pdu.Body[offset] != 0) offset++;
        offset++;
        
        offset += 2; // registered_delivery + replace_if_present_flag
        
        return offset < pdu.Body.Length ? offset : -1;
    }
    
    private int GetShortMessageOffset(SmppPdu pdu)
    {
        var dataCodingOffset = GetDataCodingOffset(pdu);
        if (dataCodingOffset == -1) return -1;
        
        return dataCodingOffset + 1 + 1; // data_coding + sm_default_msg_id + sm_length
    }

    // SAR Optional Parameters (fallback method)
    private bool TryGetSarConcatenationInfo(SmppPdu pdu, out SarConcatenationInfo sarInfo)
    {
        sarInfo = default;
        
        if (pdu.OptionalParameters == null)
            return false;
        
        if (!pdu.OptionalParameters.TryGetValue(SmppPdu.OptionalParameterTags.SAR_MSG_REF_NUM, out var referenceNumberBytes) ||
            !pdu.OptionalParameters.TryGetValue(SmppPdu.OptionalParameterTags.SAR_TOTAL_SEGMENTS, out var totalBytes) ||
            !pdu.OptionalParameters.TryGetValue(SmppPdu.OptionalParameterTags.SAR_SEGMENT_SEQNUM, out var sequenceBytes))
        {
            return false;
        }

        if (referenceNumberBytes.Length < 2 || totalBytes.Length < 1 || sequenceBytes.Length < 1)
            return false;
                
        var referenceNumber = BitConverter.ToUInt16(referenceNumberBytes.Reverse().ToArray());
        var totalSegments = totalBytes[0];
        var sequenceNum = sequenceBytes[0];
        
        sarInfo = new SarConcatenationInfo
        {
            ReferenceNumber = referenceNumber,
            TotalParts = totalSegments,
            PartNumber = sequenceNum
        };
        
        return true;
    }

    private static string CombineMessageParts(MessagePartState state)
    {
        var orderedParts = state.ReceiveParts
            .OrderBy(p => p.Key)
            .Select(x => Encoding.UTF8.GetString(x.Value))
            .ToArray();

        return string.Join("", orderedParts);
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
                logger.LogWarning("Removed stale incomplete message {MessagePartKey} with {ReceivedParts}/{TotalParts} parts", 
                    key, removedState.ReceiveParts.Count, removedState.TotalParts);
            }
        }
        
        if (staleKeys.Count > 0)
        {
            logger.LogInformation("Cleaned up {StaleCount} stale message parts", staleKeys.Count);
        }
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