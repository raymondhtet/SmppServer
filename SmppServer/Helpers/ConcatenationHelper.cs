using System.Text;
using Smpp.Server.Constants;
using Smpp.Server.Models.DTOs;

namespace Smpp.Server.Helpers;

public static class ConcatenationHelper
{
    /// <summary>
    /// Extract concatenation info from SubmitSmRequest
    /// </summary>
    public static (bool IsMultipartMessage, SmppConstants.ConcatenationInfo? Info, string message) GetConcatenationInfo(SubmitSmRequest request)
    {
        // First check: UDH indicator in ESM_CLASS (bit 6)
        if ((request.EsmClass & 0x40) != 0)
        {
            //Console.WriteLine($"UDH indicator found in ESM_CLASS: 0x{request.EsmClass:X2}");
            
            // Parse UDH from short message
            if (TryParseUdhConcatenation(request.ShortMessage, out var udhInfo))
            {
                var concatenationInfo = new SmppConstants.ConcatenationInfo
                {
                    ReferenceNumber = udhInfo.ReferenceNumber,
                    TotalParts = udhInfo.TotalParts,
                    PartNumber = udhInfo.PartNumber,
                    Type = SmppConstants.ConcatenationType.UDH
                };
                return (true, concatenationInfo, ExtractMessageContent(request, concatenationInfo));
            }
        }
        
        // Second check: SAR optional parameters
        if (TryParseSarConcatenation(request.OptionalParameters, out var sarInfo))
        {
            //Console.WriteLine($"SAR concatenation found in optional parameters");
            var concatenationInfo = new SmppConstants.ConcatenationInfo
            {
                ReferenceNumber = sarInfo.ReferenceNumber,
                TotalParts = sarInfo.TotalParts,
                PartNumber = sarInfo.PartNumber,
                Type = SmppConstants.ConcatenationType.SAR
            };
                
            return (true, concatenationInfo, ExtractMessageContent(request, concatenationInfo));
        }
        
        //Console.WriteLine($"Single message - ESM_CLASS: 0x{request.EsmClass:X2}");
        return (false, null, ExtractMessageContent(request, null));
    }

    /// <summary>
    /// Parse UDH concatenation from short message bytes
    /// </summary>
    private static bool TryParseUdhConcatenation(byte[] shortMessage, out SmppConstants.UdhConcatenationData udhInfo)
    {
        udhInfo = default;
        
        if (shortMessage == null || shortMessage.Length < 6) // Minimum UDH size
        {
            Console.WriteLine($"Short message too small for UDH: {shortMessage?.Length ?? 0} bytes");
            return false;
        }

        try
        {
            Console.WriteLine($"Short message hex: {Convert.ToHexString(shortMessage)}");
            
            var udhl = shortMessage[0]; // UDH Length
            Console.WriteLine($"UDH Length: {udhl}");
            
            if (udhl == 0 || udhl + 1 > shortMessage.Length)
            {
                Console.WriteLine($"Invalid UDH length: {udhl}");
                return false;
            }

            // Parse Information Elements in UDH
            var offset = 1; // Skip UDHL
            while (offset < udhl + 1 && offset + 1 < shortMessage.Length)
            {
                var iei = shortMessage[offset++];     // Information Element Identifier
                var iedl = shortMessage[offset++];    // Information Element Data Length
                
                Console.WriteLine($"IE: IEI=0x{iei:X2}, IEDL={iedl}");

                if (offset + iedl > shortMessage.Length)
                {
                    Console.WriteLine($"IE data exceeds message bounds");
                    break;
                }

                // Check for concatenation IEs
                if (iei == 0x00 && iedl == 3) // 8-bit reference
                {
                    var refNum = shortMessage[offset];
                    var totalParts = shortMessage[offset + 1];
                    var partNum = shortMessage[offset + 2];
                    
                    Console.WriteLine($"8-bit UDH concatenation: Ref={refNum}, Part={partNum}/{totalParts}");
                    
                    if (IsValidConcatenation(partNum, totalParts))
                    {
                        udhInfo = new SmppConstants.UdhConcatenationData
                        {
                            ReferenceNumber = refNum,
                            TotalParts = totalParts,
                            PartNumber = partNum,
                            UdhLength = udhl
                        };
                        return true;
                    }
                }
                else if (iei == 0x08 && iedl == 4) // 16-bit reference
                {
                    var refNum = (ushort)((shortMessage[offset] << 8) | shortMessage[offset + 1]);
                    var totalParts = shortMessage[offset + 2];
                    var partNum = shortMessage[offset + 3];
                    
                    Console.WriteLine($"16-bit UDH concatenation: Ref={refNum}, Part={partNum}/{totalParts}");
                    
                    if (IsValidConcatenation(partNum, totalParts))
                    {
                        udhInfo = new SmppConstants.UdhConcatenationData
                        {
                            ReferenceNumber = refNum,
                            TotalParts = totalParts,
                            PartNumber = partNum,
                            UdhLength = udhl
                        };
                        return true;
                    }
                }

                offset += iedl;
            }

            Console.WriteLine($"No concatenation IE found in UDH");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing UDH: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Parse SAR concatenation from optional parameters
    /// </summary>
    private static bool TryParseSarConcatenation(Dictionary<ushort, byte[]> optionalParameters, out SmppConstants.SarConcatenationData sarInfo)
    {
        sarInfo = default;
        
        if (optionalParameters == null || optionalParameters.Count == 0)
        {
            Console.WriteLine($"No optional parameters for SAR check");
            return false;
        }

        Console.WriteLine($"Checking {optionalParameters.Count} optional parameters:");
        foreach (var param in optionalParameters)
        {
            Console.WriteLine($"  Tag: 0x{param.Key:X4}, Length: {param.Value.Length}, Data: {Convert.ToHexString(param.Value)}");
        }

        // SAR parameter tags (from SmppPdu.OptionalParameterTags)
        const ushort SAR_MSG_REF_NUM = 0x020C;
        const ushort SAR_TOTAL_SEGMENTS = 0x020E;
        const ushort SAR_SEGMENT_SEQNUM = 0x020F;

        if (!optionalParameters.TryGetValue(SAR_MSG_REF_NUM, out var referenceBytes) ||
            !optionalParameters.TryGetValue(SAR_TOTAL_SEGMENTS, out var totalBytes) ||
            !optionalParameters.TryGetValue(SAR_SEGMENT_SEQNUM, out var sequenceBytes))
        {
            Console.WriteLine($"SAR parameters not found");
            return false;
        }

        if (referenceBytes.Length < 2 || totalBytes.Length < 1 || sequenceBytes.Length < 1)
        {
            Console.WriteLine($"Invalid SAR parameter lengths");
            return false;
        }

        var referenceNumber = BitConverter.ToUInt16(referenceBytes.Reverse().ToArray(), 0);
        var totalSegments = totalBytes[0];
        var sequenceNum = sequenceBytes[0];

        Console.WriteLine($"SAR concatenation: Ref={referenceNumber}, Part={sequenceNum}/{totalSegments}");

        if (IsValidConcatenation(sequenceNum, totalSegments))
        {
            sarInfo = new SmppConstants.SarConcatenationData
            {
                ReferenceNumber = referenceNumber,
                TotalParts = totalSegments,
                PartNumber = sequenceNum
            };
            return true;
        }

        return false;
    }

    /// <summary>
    /// Extract the actual message content from UDH message
    /// </summary>
    private static string ExtractMessageContent(SubmitSmRequest request, SmppConstants.ConcatenationInfo? concatInfo)
    {
        if (concatInfo?.Type == SmppConstants.ConcatenationType.UDH)
        {
            // For UDH messages, skip the UDH header
            var udhInfo = concatInfo.Value;
            var shortMessage = request.ShortMessage;
            
            if (shortMessage != null && shortMessage.Length > 0)
            {
                var udhl = shortMessage[0];
                var messageStart = 1 + udhl; // Skip UDHL + UDH data
                
                if (messageStart < shortMessage.Length)
                {
                    var messageData = new byte[shortMessage.Length - messageStart];
                    Array.Copy(shortMessage, messageStart, messageData, 0, messageData.Length);
                    
                    //Console.WriteLine($"Extracted message data: {Convert.ToHexString(messageData)}");
                    
                    // Decode based on data coding
                    return DecodeMessage(messageData, request.DataCoding);
                }
            }
        }
        else if (concatInfo?.Type == SmppConstants.ConcatenationType.SAR)
        {
            // For SAR, the entire short message is the content
            return DecodeMessage(request.ShortMessage, request.DataCoding);
        }
        else
        {
            // Single message
            return DecodeMessage(request.ShortMessage, request.DataCoding);
        }

        return string.Empty;
    }

    private static string DecodeMessage(byte[] data, byte dataCoding)
    {
        if (data == null || data.Length == 0)
            return string.Empty;

        try
        {
            return dataCoding switch
            {
                0x00 => Encoding.UTF8.GetString(data), // GSM 7-bit (simplified)
                0x01 => Encoding.ASCII.GetString(data),
                0x03 => Encoding.GetEncoding("ISO-8859-1").GetString(data),
                0x08 => Encoding.BigEndianUnicode.GetString(data),
                _ => Encoding.UTF8.GetString(data)
            };
        }
        catch
        {
            return Encoding.UTF8.GetString(data);
        }
    }

    private static bool IsValidConcatenation(byte partNum, byte totalParts)
    {
        return partNum > 0 && totalParts > 1 && partNum <= totalParts;
    }
}