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
    
    public void ParseBody()
    {
        Body = Convert.FromHexString("000000313131313131310000003635393436353139373100000000003235303930333038313533393030302B0001000000274C6F72656D20697073756D20646F6C6F722073697420616D65742C20636F6E7365637465747572000500010012AB002863616D706169676E2D69643132333432313233313432343231343231353135313631313631363136");
        
        var parser = new PduFieldParser(Body!);
        

        var request = new SubmitSmRequest
        {
            ServiceType = parser.ReadCString(),
            SourceAddrTon = parser.ReadByte(),
            SourceAddrNpi = parser.ReadByte(),
            SourceAddress = parser.ReadCString(),
            DestAddrTon = parser.ReadByte(),
            DestAddrNpi = parser.ReadByte(),
            DestinationAddress = parser.ReadCString(),
            EsmClass = parser.ReadByte(),
            ProtocolId = parser.ReadByte(),
            PriorityFlag = parser.ReadByte(),
            ScheduleDeliveryTime = parser.ReadCString(),
            ValidityPeriod = parser.ReadCString(),
            RegisteredDelivery = parser.ReadByte(),
            ReplaceIfPresentFlag = parser.ReadByte(),
            DataCoding = parser.ReadByte(),
            SmDefaultMsgId = parser.ReadByte(),
            ShortMessage = parser.ReadShortMessage(),
            MessagePayload = ReadMessagePayload(),
            OptionalParameters = OptionalParameters
        };
        
        ParseOptionalParameters(parser.Offset);
        string campaignId = ReadCampaignId();
        
        Console.WriteLine(request);
    }
    
    public byte[] ReadMessagePayload()
    {
        foreach (var param in OptionalParameters.Where(param => param.Key == 0x0424))
        {
            return param.Value;
        }

        return [];
    }
    
    public string ReadCampaignId()
    {
        foreach (var param in OptionalParameters.Where(param => param.Key == 0x12AB))
        {
            return Encoding.UTF8.GetString(param.Value);
        }

        return string.Empty;
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

    public void ParseOptionalParameters(int startIndex)
    {
        OptionalParameters.Clear();

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


    public static class OptionalParameterTags
    {
        public const ushort MESSAGE_PAYLOAD = 0x0424;
        public const ushort SAR_MSG_REF_NUM = 0x020C;
        public const ushort SAR_TOTAL_SEGMENTS = 0x020E;
        public const ushort SAR_SEGMENT_SEQNUM = 0x020F;
    }

    
}