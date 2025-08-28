using System.Text;
using Smpp.Server.Helpers;
using Smpp.Server.Models.DTOs;

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
    
    public byte[] ReadMessagePayload()
    {
        foreach (var param in OptionalParameters.Where(param => param.Key == 0x0424))
        {
            return param.Value;
        }

        return [];
    }

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

    public void ParseOptionalParameters()
    {
        OptionalParameters.Clear();

        var startIndex = GetOptionalParamsStartIndex();

        Console.WriteLine($"DEBUG: Body length: {Body.Length}, Optional params start at: {startIndex}");
        Console.WriteLine($"DEBUG: Body hex: {Convert.ToHexString(Body)}");

        if (startIndex >= Body?.Length)
        {
            Console.WriteLine("No optional parameters found - start index beyond body length");
            return;
        }

        Console.WriteLine($"Parsing optional parameters starting at index {startIndex}, body length: {Body?.Length}");

        var currentIndex = startIndex;
        var paramCount = 0;

        while (currentIndex <= (Body?.Length ?? 0) - 4) // Need at least 4 bytes (2 for tag, 2 for length)
        {
            if (currentIndex + 4 > Body?.Length)
            {
                Console.WriteLine($"Not enough bytes for next optional parameter at index {currentIndex}");
                break;
            }

            // Read tag (2 bytes, big-endian)
            var tag = (ushort)((Body![currentIndex] << 8) | Body[currentIndex + 1]);

            // Read length (2 bytes, big-endian)
            var length = (ushort)((Body[currentIndex + 2] << 8) | Body[currentIndex + 3]);

            Console.WriteLine($"Optional param #{paramCount + 1}: Tag=0x{tag:X4}, Length={length}");

            // Validate length
            if (currentIndex + 4 + length > Body.Length)
            {
                Console.WriteLine($"Optional parameter length {length} exceeds remaining body bytes");
                break;
            }

            // Read value
            byte[] value;
            if (length > 0)
            {
                value = new byte[length];
                Array.Copy(Body, currentIndex + 4, value, 0, length);
            }
            else
            {
                value = Array.Empty<byte>();
            }

            OptionalParameters[tag] = value;
            Console.WriteLine($"Added optional parameter: Tag=0x{tag:X4}, Value={Convert.ToHexString(value)}");

            currentIndex += 4 + length;
            paramCount++;

            // Safety check to prevent infinite loops
            if (paramCount > 100)
            {
                Console.WriteLine("Too many optional parameters, stopping parsing");
                break;
            }
        }

        Console.WriteLine($"Parsed {paramCount} optional parameters");
    }

    /// <summary>
    /// Fixed calculation of where optional parameters start
    /// </summary>
    private int GetOptionalParamsStartIndex()
    {
        if (Body == null || Body.Length == 0)
        {
            Console.WriteLine("DEBUG: No body for optional params calculation");
            return 0;
        }

        Console.WriteLine($"DEBUG: Calculating start index for body of {Body.Length} bytes");

        var parser = new PduFieldParser(Body);

        // Parse all mandatory submit_sm fields
        var serviceType = parser.ReadCString();
        Console.WriteLine($"DEBUG: service_type='{serviceType}' (offset: {parser.Offset})");

        parser.ReadByte(); // source_addr_ton
        parser.ReadByte(); // source_addr_npi
        Console.WriteLine($"DEBUG: source TON/NPI (offset: {parser.Offset})");

        var sourceAddr = parser.ReadCString();
        Console.WriteLine($"DEBUG: source_addr='{sourceAddr}' (offset: {parser.Offset})");

        parser.ReadByte(); // dest_addr_ton
        parser.ReadByte(); // dest_addr_npi
        Console.WriteLine($"DEBUG: dest TON/NPI (offset: {parser.Offset})");

        var destAddr = parser.ReadCString();
        Console.WriteLine($"DEBUG: dest_addr='{destAddr}' (offset: {parser.Offset})");

        parser.ReadByte(); // esm_class
        parser.ReadByte(); // protocol_id
        parser.ReadByte(); // priority_flag
        Console.WriteLine($"DEBUG: esm/protocol/priority (offset: {parser.Offset})");

        var scheduleTime = parser.ReadCString();
        Console.WriteLine($"DEBUG: schedule_time='{scheduleTime}' (offset: {parser.Offset})");

        var validityPeriod = parser.ReadCString();
        Console.WriteLine($"DEBUG: validity_period='{validityPeriod}' (offset: {parser.Offset})");

        parser.ReadByte(); // registered_delivery
        parser.ReadByte(); // replace_if_present_flag
        parser.ReadByte(); // data_coding
        parser.ReadByte(); // sm_default_msg_id
        Console.WriteLine($"DEBUG: reg_del/replace/data_coding/sm_default (offset: {parser.Offset})");

        // CRITICAL: Handle sm_length and short_message properly
        if (parser.RemainingBytes > 0)
        {
            var smLength = parser.ReadByte();
            Console.WriteLine($"DEBUG: sm_length={smLength} (offset: {parser.Offset})");

            if (smLength > 0)
            {
                if (parser.RemainingBytes >= smLength)
                {
                    var shortMessage = parser.ReadBytes(smLength);
                    Console.WriteLine(
                        $"DEBUG: short_message={Convert.ToHexString(shortMessage)} (offset: {parser.Offset})");
                }
                else
                {
                    Console.WriteLine(
                        $"DEBUG: WARNING - sm_length={smLength} but only {parser.RemainingBytes} bytes remaining");
                    // Skip remaining bytes
                    parser.Skip(parser.RemainingBytes);
                }
            }
        }
        else
        {
            Console.WriteLine($"DEBUG: No bytes left for sm_length field");
        }

        var finalOffset = parser.Offset;
        Console.WriteLine($"DEBUG: Optional parameters start at offset {finalOffset}");
        Console.WriteLine($"DEBUG: Remaining bytes: {parser.RemainingBytes}");

        if (parser.RemainingBytes > 0)
        {
            var remaining = parser.GetRemainingData();
            Console.WriteLine($"DEBUG: Remaining data: {Convert.ToHexString(remaining)}");
        }

        return finalOffset;
    }
    

    public static class OptionalParameterTags
    {
        public const ushort MESSAGE_PAYLOAD = 0x0424;
        public const ushort SAR_MSG_REF_NUM = 0x020C;
        public const ushort SAR_TOTAL_SEGMENTS = 0x020E;
        public const ushort SAR_SEGMENT_SEQNUM = 0x020F;
    }
}