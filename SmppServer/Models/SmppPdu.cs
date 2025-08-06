using Smpp.Server.Helpers;
using System.Text;

namespace Smpp.Server.Models
{
    public class SmppPdu
    {
        public uint CommandLength { get; set; }
        public uint CommandId { get; set; }
        public uint CommandStatus { get; set; }
        public uint SequenceNumber { get; set; }
        public byte[]? Body { get; set; }

        public SmppPdu()
        {
            Body = Array.Empty<byte>();
        }

        public void ParseHeader(byte[] headerData)
        {
            if (headerData.Length < 16)
                throw new ArgumentException("Header data must be at least 16 bytes");

            CommandLength = BitConverter.ToUInt32(headerData.Take(4).Reverse().ToArray());
            CommandId = BitConverter.ToUInt32(headerData.Skip(4).Take(4).Reverse().ToArray());
            CommandStatus = BitConverter.ToUInt32(headerData.Skip(8).Take(4).Reverse().ToArray());
            SequenceNumber = BitConverter.ToUInt32(headerData.Skip(12).Take(4).Reverse().ToArray());
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
            if (Body != null && Body.Length > 0)
            {
                Body.CopyTo(buffer, 16);
            }

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

        public static class OptionalParameterTags
        {
            public const ushort MESSAGE_PAYLOAD = 0x0424;
            public const ushort SAR_MSG_REF_NUM = 0x020C;
            public const ushort SAR_TOTAL_SEGMENTS = 0x020E;
            public const ushort SAR_SEGMENT_SEQNUM = 0x020F;
        }

        public Dictionary<ushort, byte[]> OptionalParameters { get; set; } = new();

        public void ParseOptionalParameters(byte[] data, int startIndex)
        {
            var currentIndex = startIndex;

            while (currentIndex < data.Length - 3) // Minimum 4 bytes needed (2 for tag, 2 for length)
            {
                var tag = BitConverter.ToUInt16(data.Skip(currentIndex).Take(2).Reverse().ToArray());
                var length = BitConverter.ToUInt16(data.Skip(currentIndex + 2).Take(2).Reverse().ToArray());

                if (currentIndex + 4 + length <= data.Length)
                {
                    var value = data.Skip(currentIndex + 4).Take(length).ToArray();
                    OptionalParameters[tag] = value;
                }

                currentIndex += 4 + length;
            }
        }
    }
}
