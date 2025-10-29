using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sg.gov.cpf.esvc.smpp.client
{
    internal class SmppPdu
    {
        public uint CommandLength { get; set; }
        public uint CommandId { get; set; }
        public uint CommandStatus { get; set; }
        public uint SequenceNumber { get; set; }
        public byte[] Body { get; set; }

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

            BitConverter.GetBytes((uint)totalLength).Reverse().ToArray().CopyTo(buffer, 0);
            BitConverter.GetBytes(CommandId).Reverse().ToArray().CopyTo(buffer, 4);
            BitConverter.GetBytes(CommandStatus).Reverse().ToArray().CopyTo(buffer, 8);
            BitConverter.GetBytes(SequenceNumber).Reverse().ToArray().CopyTo(buffer, 12);

            if (Body != null && Body.Length > 0)
            {
                Body.CopyTo(buffer, 16);
            }

            return buffer;
        }

    }
}
