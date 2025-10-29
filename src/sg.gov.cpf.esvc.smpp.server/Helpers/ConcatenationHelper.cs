using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Models.DTOs;
using System.Text;
using static sg.gov.cpf.esvc.smpp.server.Constants.SmppConstants;

namespace sg.gov.cpf.esvc.smpp.server.Helpers;

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

        
        if (!optionalParameters.TryGetValue(OptionalParameterTags.SAR_MSG_REF_NUM, out var referenceBytes) ||
            !optionalParameters.TryGetValue(OptionalParameterTags.SAR_TOTAL_SEGMENTS, out var totalBytes) ||
            !optionalParameters.TryGetValue(OptionalParameterTags.SAR_SEGMENT_SEQNUM, out var sequenceBytes))
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

            if (shortMessage is not { Length: > 0 }) return string.Empty;
            var udhl = shortMessage[0];
            var messageStart = 1 + udhl; // Skip UDHL + UDH data

            if (messageStart < shortMessage.Length)
            {
                var messageData = new byte[shortMessage.Length - messageStart];
                Array.Copy(shortMessage, messageStart, messageData, 0, messageData.Length);

                // Decode based on data coding
                return DecodeMessage(messageData, request.MessagePayload, request.DataCoding);
            }
        }
        else if (concatInfo?.Type == SmppConstants.ConcatenationType.SAR)
        {
            // For SAR, the entire short message is the content
            return DecodeMessage(request.ShortMessage, request.MessagePayload, request.DataCoding);
        }
        else
        {
            // Single message
            return DecodeMessage(request.ShortMessage, request.MessagePayload, request.DataCoding);
        }

        return string.Empty;
    }

    private static string DecodeMessage(byte[]? shortMessage, byte[]? messagePayload, byte dataCoding)
    {
        if ((shortMessage == null || shortMessage.Length == 0) && (messagePayload == null || messagePayload.Length == 0))
            return string.Empty;

        var data = shortMessage!.Length > 0 ? shortMessage : messagePayload!;

        return dataCoding switch
        {
            0x00 => DecodeGsm7Bit(data), // GSM 7-bit (simplified)
            0x01 => Encoding.ASCII.GetString(data),
            0x03 => Encoding.GetEncoding("ISO-8859-1").GetString(data),
            0x08 => Encoding.BigEndianUnicode.GetString(data),
            _ => Encoding.UTF8.GetString(data)
        };

    }

    private static readonly char[] GSM_7BIT_CHARS = {
        '@', '£', '$', '¥', 'è', 'é', 'ù', 'ì', 'ò', 'Ç', '\n', 'Ø', 'ø', '\r', 'Å', 'å',
        'Δ', '_', 'Φ', 'Γ', 'Λ', 'Ω', 'Π', 'Ψ', 'Σ', 'Θ', 'Ξ', '\x1B', 'Æ', 'æ', 'ß', 'É',
        ' ', '!', '"', '#', '¤', '%', '&', '\'', '(', ')', '*', '+', ',', '-', '.', '/',
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ':', ';', '<', '=', '>', '?',
        '¡', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O',
        'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'Ä', 'Ö', 'Ñ', 'Ü', '§',
        '¿', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o',
        'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'ä', 'ö', 'ñ', 'ü', 'à'
        };

    public static string DecodeGsm7Bit(byte[] data)
    {
        var result = new StringBuilder();
        foreach (byte b in data)
        {
            if (b < GSM_7BIT_CHARS.Length)
            {
                result.Append(GSM_7BIT_CHARS[b]);
            }
        }
        return result.ToString();
    }

    private static bool IsValidConcatenation(byte partNum, byte totalParts)
    {
        return partNum > 0 && totalParts > 1 && partNum <= totalParts;
    }
}