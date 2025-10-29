using sg.gov.cpf.esvc.smpp.server.Helpers;
using sg.gov.cpf.esvc.smpp.server.Models.DTOs;
using System.Text;
using static sg.gov.cpf.esvc.smpp.server.Constants.SmppConstants;

namespace sg.gov.cpf.esvc.smpp.server.Models;

public class SmppPdu
{
    public SmppPdu()
    {
        Body = [];
    }

    public uint CommandLength { get; set; }
    public uint CommandId { get; set; }
    public uint CommandStatus { get; set; }
    public uint SequenceNumber { get; set; }
    public byte[]? Body { get; set; }

    public string SystemId { get; internal set; }

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
        if (Body != null && Body.Length > 0) Body.CopyTo(buffer, 16);

        return buffer;
    }
}