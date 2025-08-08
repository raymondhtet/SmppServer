using System.Text;
using Smpp.Server.Models;

namespace Smpp.Server.Helpers;

public class MessageParser
{
    private const int MAX_SHORT_MESSAGE_LENGTH = 254;

    public static (string Message, int DataCoding) ExtractMessageFromPdu(SmppPdu pdu)
    {
        var bodyData = pdu.Body;
        if (bodyData == null || bodyData.Length < 17) // Minimum length check
            throw new ArgumentException("Invalid PDU body length");

        // Skip to sm_length position (varies based on address lengths)
        var currentPos = 0;

        // Skip service_type
        while (currentPos < bodyData.Length && bodyData[currentPos] != 0) currentPos++;
        currentPos++; // Skip null terminator

        // source_addr_ton and source_addr_npi
        currentPos += 2;

        // Skip source_addr
        while (currentPos < bodyData.Length && bodyData[currentPos] != 0) currentPos++;
        currentPos++; // Skip null terminator

        // dest_addr_ton and dest_addr_npi
        currentPos += 2;

        // Skip dest_addr
        while (currentPos < bodyData.Length && bodyData[currentPos] != 0) currentPos++;
        currentPos++; // Skip null terminator

        // esm_class, protocol_id, priority_flag
        currentPos += 3;

        // Skip schedule_delivery_time
        while (currentPos < bodyData.Length && bodyData[currentPos] != 0) currentPos++;
        currentPos++; // Skip null terminator

        // Skip validity_period
        while (currentPos < bodyData.Length && bodyData[currentPos] != 0) currentPos++;
        currentPos++; // Skip null terminator

        // registered_delivery, replace_if_present_flag, data_coding
        var dataCoding = bodyData[currentPos + 2];
        currentPos += 3;

        // sm_default_msg_id
        currentPos++;

        // Get sm_length
        var messageLength = bodyData[currentPos++];

        string message;

        if (messageLength > 0)
            // Extract short_message
            message = ExtractMessage(bodyData.Skip(currentPos).Take(messageLength).ToArray(), dataCoding);
        else
            // Try to get message_payload from optional parameters
            message = ExtractMessagePayload(pdu.OptionalParameters, dataCoding);

        return (message, dataCoding);
    }

    private static string ExtractMessage(byte[] messageData, int dataCoding)
    {
        return dataCoding switch
        {
            0x00 => Encoding.ASCII.GetString(messageData), // SMSC Default Alphabet
            0x01 => Encoding.ASCII.GetString(messageData), // IA5 (ASCII)
            0x02 => Encoding.BigEndianUnicode.GetString(messageData), // Octet unspecified
            0x03 => Encoding.BigEndianUnicode.GetString(messageData), // Latin 1
            0x04 => Encoding.BigEndianUnicode.GetString(messageData), // Octet unspecified
            0x08 => Encoding.BigEndianUnicode.GetString(messageData), // UCS2
            _ => Encoding.ASCII.GetString(messageData) // Default to ASCII
        };
    }

    private static string ExtractMessagePayload(Dictionary<ushort, byte[]> optionalParameters, int dataCoding)
    {
        if (optionalParameters.TryGetValue(SmppPdu.OptionalParameterTags.MESSAGE_PAYLOAD, out var payloadData))
            return ExtractMessage(payloadData, dataCoding);

        return string.Empty;
    }

    public static byte[] CreateMessagePayload(string message, int dataCoding)
    {
        var messageBytes = dataCoding switch
        {
            0x00 => Encoding.ASCII.GetBytes(message),
            0x01 => Encoding.ASCII.GetBytes(message),
            0x02 => Encoding.BigEndianUnicode.GetBytes(message),
            0x03 => Encoding.BigEndianUnicode.GetBytes(message),
            0x04 => Encoding.BigEndianUnicode.GetBytes(message),
            0x08 => Encoding.BigEndianUnicode.GetBytes(message),
            _ => Encoding.ASCII.GetBytes(message)
        };

        return messageBytes;
    }
}