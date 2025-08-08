using System.Text;
using Smpp.Server.Helpers;

namespace Smpp.Server.Models;

public class SmppPdu
{
    public SmppPdu()
    {
        Body = Array.Empty<byte>();
    }

    public uint CommandLength { get; set; }
    public uint CommandId { get; set; }
    public uint CommandStatus { get; set; }
    public uint SequenceNumber { get; set; }
    public byte[]? Body { get; set; }

    public Dictionary<ushort, byte[]> OptionalParameters { get; set; } = new();

    public void ParseHeader(byte[] headerData)
    {
        if (headerData.Length < 16)
            throw new ArgumentException("Header data must be at least 16 bytes");

        CommandLength = BitConverter.ToUInt32(headerData.Take(4).Reverse().ToArray());
        CommandId = BitConverter.ToUInt32(headerData.Skip(4).Take(4).Reverse().ToArray());
        CommandStatus = BitConverter.ToUInt32(headerData.Skip(8).Take(4).Reverse().ToArray());
        SequenceNumber = BitConverter.ToUInt32(headerData.Skip(12).Take(4).Reverse().ToArray());
    }

    public string GetSourceAddress => GetString(GetByte(2) + 3);

    public string GetDestinationAddress => GetString(GetByte(GetByte(2) + 4) + this.GetByte(2) + 5);

    public byte[] GetBytes()
    {
        var headerLength = 16;
        var totalLength = headerLength + (Body?.Length ?? 0);
        var buffer = new byte[totalLength];

        // Command Length
        BitConverter.GetBytes(totalLength).Reverse().ToArray().CopyTo(buffer, 0);

        // Command ID
        BitConverter.GetBytes(CommandId).Reverse().ToArray().CopyTo(buffer, 4);

        // Command Status
        BitConverter.GetBytes(CommandStatus).Reverse().ToArray().CopyTo(buffer, 8);

        // Sequence Number
        BitConverter.GetBytes(SequenceNumber).Reverse().ToArray().CopyTo(buffer, 12);

        // Body
        if (Body != null && Body.Length > 0) Body.CopyTo(buffer, 16);

        return buffer;
    }

    public string GetString(int offset)
    {
        if (Body == null || offset >= Body.Length)
            return string.Empty;

        var endIndex = Array.IndexOf(Body, (byte)0, offset);
        if (endIndex == -1)
            endIndex = Body.Length;

        return Encoding.ASCII.GetString(Body, offset, endIndex - offset);
    }

    public byte GetByte(int offset)
    {
        return Body?[offset] ?? 0;
    }

    public (string Message, int DataEncoding) GetMessageContent()
    {
        return MessageParser.ExtractMessageFromPdu(this);
    }

    public void ParseOptionalParameters()
    {
        var currentIndex = GetOptionalParamsStartIndex();

        while (currentIndex < Body?.Length - 3) // Minimum 4 bytes needed (2 for tag, 2 for length)
        {
            var tag = BitConverter.ToUInt16(Body?.Skip(currentIndex).Take(2).Reverse().ToArray());
            var length = BitConverter.ToUInt16(Body?.Skip(currentIndex + 2).Take(2).Reverse().ToArray());

            if (currentIndex + 4 + length <= Body?.Length)
            {
                byte[]? value = Body?.Skip(currentIndex + 4).Take(length).ToArray();
                OptionalParameters[tag] = value;
            }

            currentIndex += 4 + length;
        }
    }
    
    private int GetOptionalParamsStartIndex()
    {
        // Calculate the start index of optional parameters
        // This depends on your PDU structure and mandatory parameters
        // You'll need to implement this based on your PDU format
        
        // Example implementation:
        int index = 0;
        
        // Skip service_type (null-terminated string)
        while (index < Body?.Length && Body[index] != 0) index++;
        index++; // Skip null terminator

        // Skip source_addr_ton and source_addr_npi
        index += 2;

        // Skip source_addr (null-terminated string)
        while (index < Body?.Length && Body[index] != 0) index++;
        index++;

        // Skip dest_addr_ton and dest_addr_npi
        index += 2;

        // Skip destination_addr (null-terminated string)
        while (index < Body?.Length && Body[index] != 0) index++;
        index++;

        // Skip other mandatory parameters
        index += 5; // esm_class, protocol_id, priority_flag, schedule_delivery_time, validity_period
        
        // Skip registered_delivery, replace_if_present_flag, data_coding, sm_default_msg_id
        index += 4;

        // Skip sm_length and short_message
        var smLength = Body![index];
        index += 1 + smLength;

        return index;
    }

    public static class OptionalParameterTags
    {
        public const ushort MESSAGE_PAYLOAD = 0x0424;
        public const ushort SAR_MSG_REF_NUM = 0x020C;
        public const ushort SAR_TOTAL_SEGMENTS = 0x020E;
        public const ushort SAR_SEGMENT_SEQNUM = 0x020F;
    }
}