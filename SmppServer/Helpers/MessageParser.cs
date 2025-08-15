using System.Text;
using Smpp.Server.Models;

namespace Smpp.Server.Helpers;

public class MessageParser
{
    private static readonly char[] Gsm7BitChars = {
        '@', '£', '$', '¥', 'è', 'é', 'ù', 'ì', 'ò', 'Ç', '\n', 'Ø', 'ø', '\r', 'Å', 'å',
        'Δ', '_', 'Φ', 'Γ', 'Λ', 'Ω', 'Π', 'Ψ', 'Σ', 'Θ', 'Ξ', ' ', 'Æ', 'æ', 'ß', 'É',
        ' ', '!', '"', '#', '¤', '%', '&', '\'', '(', ')', '*', '+', ',', '-', '.', '/',
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ':', ';', '<', '=', '>', '?',
        '¡', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O',
        'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'Ä', 'Ö', 'Ñ', 'Ü', '§',
        '¿', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o',
        'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'ä', 'ö', 'ñ', 'ü', 'à'
    };


    public static (string Message, int DataCoding) ExtractMessageFromPdu(SmppPdu pdu)
    {
        var bodyData = pdu.Body;
        if (bodyData == null || bodyData.Length == 0)
        {
            // Try optional parameters first
            var messageFromOptional = ExtractMessagePayload(pdu.OptionalParameters, 0);
            return (messageFromOptional, 0);
        }

        // Debug: Log the raw PDU body
        Console.WriteLine($"PDU Body Length: {bodyData.Length}");
        Console.WriteLine($"PDU Body Hex: {Convert.ToHexString(bodyData)}");

        try
        {
            // Use PduFieldParser for more reliable parsing
            var parser = new PduFieldParser(bodyData);
            
            // Parse submit_sm fields in order
            var serviceType = parser.ReadCString();
            var sourceAddrTon = parser.ReadByte();
            var sourceAddrNpi = parser.ReadByte();
            var sourceAddr = parser.ReadCString();
            var destAddrTon = parser.ReadByte();
            var destAddrNpi = parser.ReadByte();
            var destAddr = parser.ReadCString();
            var esmClass = parser.ReadByte();
            var protocolId = parser.ReadByte();
            var priorityFlag = parser.ReadByte();
            var scheduleDeliveryTime = parser.ReadCString();
            var validityPeriod = parser.ReadCString();
            var registeredDelivery = parser.ReadByte();
            var replaceIfPresentFlag = parser.ReadByte();
            var dataCoding = parser.ReadByte();
            var smDefaultMsgId = parser.ReadByte();
            
            // Debug logging
            Console.WriteLine($"Parsed fields:");
            Console.WriteLine($"  ServiceType: '{serviceType}'");
            Console.WriteLine($"  SourceAddr: '{sourceAddr}'");
            Console.WriteLine($"  DestAddr: '{destAddr}'");
            Console.WriteLine($"  DataCoding: {dataCoding}");
            Console.WriteLine($"  Parser position: {parser.Offset}, Remaining: {parser.RemainingBytes}");

            // Get short message
            if (parser.RemainingBytes > 0)
            {
                var shortMessage = parser.ReadShortMessage();
                
                Console.WriteLine($"Short message length: {shortMessage.Length}");
                Console.WriteLine($"Short message hex: {Convert.ToHexString(shortMessage)}");
                
                if (shortMessage.Length > 0)
                {
                    var message = ExtractMessage(shortMessage, dataCoding);
                    Console.WriteLine($"Extracted message: '{message}'");
                    return (message, dataCoding);
                }
            }

            // If no short message, try optional parameters
            var messageFromOptional = ExtractMessagePayload(pdu.OptionalParameters, dataCoding);
            Console.WriteLine($"Message from optional parameters: '{messageFromOptional}'");
            
            return (messageFromOptional, dataCoding);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing PDU: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Fallback: try to find message data manually
            return TryManualExtraction(bodyData);
        }
    }
    
    /// <summary>
    /// Decode GSM 7-bit default alphabet
    /// </summary>
    public static string DecodeGsm7Bit(byte[] data)
    {
        if (data == null || data.Length == 0)
            return string.Empty;

        var result = new StringBuilder();
        
        // For simple GSM 7-bit decoding (without packing)
        // Most SMPP test tools send unpacked GSM 7-bit (one character per byte)
        foreach (var b in data)
        {
            if (b < Gsm7BitChars.Length)
            {
                result.Append(Gsm7BitChars[b]);
            }
            else if (b >= 32 && b <= 126) // Printable ASCII range
            {
                result.Append((char)b);
            }
            else
            {
                result.Append('?'); // Unknown character
            }
        }
        
        return result.ToString();
    }
    public static byte[] EncodeGsm7Bit(string message)
    {
        if (string.IsNullOrEmpty(message))
            return Array.Empty<byte>();

        var result = new List<byte>();
        
        foreach (char c in message)
        {
            int index = Array.IndexOf(Gsm7BitChars, c);
            if (index >= 0)
            {
                result.Add((byte)index);
            }
            else
            {
                // Fallback to ASCII if character not in GSM set
                result.Add((byte)Math.Min((int)c, 127));
            }
        }
        
        return result.ToArray();
    }


    /// <summary>
    /// Manual fallback extraction for debugging
    /// </summary>
    private static (string Message, int DataCoding) TryManualExtraction(byte[] bodyData)
    {
        try
        {
            Console.WriteLine("Attempting manual extraction...");
            
            // Look for the sm_length byte and message data
            // This is a simplified approach to find any message content
            for (int i = 0; i < bodyData.Length - 1; i++)
            {
                var possibleLength = bodyData[i];
                
                // Check if this could be a message length (reasonable range)
                if (possibleLength > 0 && possibleLength <= 160 && 
                    i + 1 + possibleLength <= bodyData.Length)
                {
                    var possibleMessage = new byte[possibleLength];
                    Array.Copy(bodyData, i + 1, possibleMessage, 0, possibleLength);
                    
                    // Check if it contains printable characters
                    var messageStr = Encoding.UTF8.GetString(possibleMessage);
                    if (IsPrintableString(messageStr))
                    {
                        Console.WriteLine($"Found potential message at offset {i}: '{messageStr}'");
                        return (messageStr, 0);
                    }
                }
            }
            
            Console.WriteLine("No readable message found in manual extraction");
            return ("", 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Manual extraction failed: {ex.Message}");
            return ("", 0);
        }
    }

    private static bool IsPrintableString(string str)
    {
        return !string.IsNullOrEmpty(str) && 
               str.All(c => char.IsControl(c) &&
               str.Any(c => char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsSymbol(c) || c == ' '));
    }

    private static string ExtractMessage(byte[] messageData, int dataCoding)
    {
        if (messageData == null || messageData.Length == 0)
            return string.Empty;

        try
        {
            var message = dataCoding switch
            {
                0x00 => Encoding.ASCII.GetString(messageData), // SMSC Default Alphabet
                0x01 => Encoding.ASCII.GetString(messageData), // IA5 (ASCII)
                0x02 => Encoding.BigEndianUnicode.GetString(messageData), // Octet unspecified
                0x03 => Encoding.Latin1.GetString(messageData), // Latin 1
                0x04 => Encoding.BigEndianUnicode.GetString(messageData), // Octet unspecified
                0x08 => Encoding.BigEndianUnicode.GetString(messageData), // UCS2
                _ => Encoding.UTF8.GetString(messageData) // Default to UTF-8 for safety
            };
            
            Console.WriteLine($"Decoded message with data coding {dataCoding}: '{message}'");
            return message;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error decoding message: {ex.Message}");
            
            // Try different encodings as fallback
            try
            {
                var fallback = Encoding.UTF8.GetString(messageData);
                Console.WriteLine($"UTF-8 fallback: '{fallback}'");
                return fallback;
            }
            catch
            {
                try
                {
                    var asciiFallback = Encoding.ASCII.GetString(messageData);
                    Console.WriteLine($"ASCII fallback: '{asciiFallback}'");
                    return asciiFallback;
                }
                catch
                {
                    // Last resort - return hex representation
                    var hexFallback = Convert.ToHexString(messageData);
                    Console.WriteLine($"Hex fallback: {hexFallback}");
                    return hexFallback;
                }
            }
        }
    }

    private static string ExtractMessagePayload(Dictionary<ushort, byte[]>? optionalParameters, int dataCoding)
    {
        if (optionalParameters == null || optionalParameters.Count == 0)
        {
            Console.WriteLine("No optional parameters found");
            return string.Empty;
        }

        Console.WriteLine($"Checking {optionalParameters.Count} optional parameters:");
        foreach (var param in optionalParameters)
        {
            Console.WriteLine($"  Tag: 0x{param.Key:X4}, Length: {param.Value.Length}, Data: {Convert.ToHexString(param.Value)}");
        }

        if (optionalParameters.TryGetValue(SmppPdu.OptionalParameterTags.MESSAGE_PAYLOAD, out var payloadData))
        {
            Console.WriteLine($"Found MESSAGE_PAYLOAD with {payloadData.Length} bytes");
            return ExtractMessage(payloadData, dataCoding);
        }

        Console.WriteLine("No MESSAGE_PAYLOAD found in optional parameters");
        return string.Empty;
    }

    public static byte[] CreateMessagePayload(string message, int dataCoding)
    {
        if (string.IsNullOrEmpty(message))
            return Array.Empty<byte>();

        try
        {
            var messageBytes = dataCoding switch
            {
                0x00 => Encoding.ASCII.GetBytes(message),
                0x01 => Encoding.ASCII.GetBytes(message),
                0x02 => Encoding.BigEndianUnicode.GetBytes(message),
                0x03 => Encoding.Latin1.GetBytes(message),
                0x04 => Encoding.BigEndianUnicode.GetBytes(message),
                0x08 => Encoding.BigEndianUnicode.GetBytes(message),
                _ => Encoding.UTF8.GetBytes(message)
            };

            return messageBytes;
        }
        catch (Exception)
        {
            // Fallback to ASCII if encoding fails
            return Encoding.ASCII.GetBytes(message);
        }
    }
}