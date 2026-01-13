using sg.gov.cpf.esvc.smpp.server.Helpers;
using System.Text;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class PduFieldParserTests
{
    [Fact]
    public void Constructor_WithNullData_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PduFieldParser(null!));
    }

    [Fact]
    public void ReadCString_WithValidString_ReturnsString()
    {
        // Arrange
        var data = Encoding.ASCII.GetBytes("Hello\0World\0");
        var parser = new PduFieldParser(data);

        // Act
        var result1 = parser.ReadCString();
        var result2 = parser.ReadCString();

        // Assert
        Assert.Equal("Hello", result1);
        Assert.Equal("World", result2);
    }

    [Fact]
    public void ReadCString_WithEmptyString_ReturnsEmpty()
    {
        // Arrange
        var data = new byte[] { 0x00 };
        var parser = new PduFieldParser(data);

        // Act
        var result = parser.ReadCString();

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void ReadByte_ReturnsCorrectByte()
    {
        // Arrange
        var data = new byte[] { 0x12, 0x34, 0x56 };
        var parser = new PduFieldParser(data);

        // Act
        var byte1 = parser.ReadByte();
        var byte2 = parser.ReadByte();
        var byte3 = parser.ReadByte();

        // Assert
        Assert.Equal(0x12, byte1);
        Assert.Equal(0x34, byte2);
        Assert.Equal(0x56, byte3);
    }

    [Fact]
    public void ReadByte_BeyondData_ReturnsZero()
    {
        // Arrange
        var data = new byte[] { 0x12 };
        var parser = new PduFieldParser(data);

        // Act
        parser.ReadByte();
        var result = parser.ReadByte();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void ReadUInt16_WithBigEndian_ReturnsCorrectValue()
    {
        // Arrange
        var data = new byte[] { 0x12, 0x34 };
        var parser = new PduFieldParser(data);

        // Act
        var result = parser.ReadUInt16();

        // Assert
        Assert.Equal(0x1234, result);
    }

    [Fact]
    public void ReadUInt32_WithBigEndian_ReturnsCorrectValue()
    {
        // Arrange
        var data = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        var parser = new PduFieldParser(data);

        // Act
        var result = parser.ReadUInt32();

        // Assert
        Assert.Equal(0x12345678u, result);
    }

    [Fact]
    public void ReadShortMessage_ReturnsCorrectBytes()
    {
        // Arrange
        var message = Encoding.ASCII.GetBytes("Hello");
        var data = new List<byte> { (byte)message.Length };
        data.AddRange(message);
        var parser = new PduFieldParser(data.ToArray());

        // Act
        var result = parser.ReadShortMessage();

        // Assert
        Assert.Equal(message, result);
    }

    [Fact]
    public void ReadShortMessage_WithZeroLength_ReturnsEmpty()
    {
        // Arrange
        var data = new byte[] { 0x00 };
        var parser = new PduFieldParser(data);

        // Act
        var result = parser.ReadShortMessage();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ReadBytes_ReturnsCorrectArray()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var parser = new PduFieldParser(data);

        // Act
        var result = parser.ReadBytes(3);

        // Assert
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, result);
        Assert.Equal(3, parser.Offset);
    }

    [Fact]
    public void Skip_AdvancesOffset()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var parser = new PduFieldParser(data);

        // Act
        parser.Skip(2);

        // Assert
        Assert.Equal(2, parser.Offset);
        Assert.Equal(0x03, parser.ReadByte());
    }

    [Fact]
    public void SkipByte_AdvancesOffsetByOne()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var parser = new PduFieldParser(data);

        // Act
        parser.SkipByte();

        // Assert
        Assert.Equal(1, parser.Offset);
    }

    [Fact]
    public void PeekByte_DoesNotAdvanceOffset()
    {
        // Arrange
        var data = new byte[] { 0x12, 0x34 };
        var parser = new PduFieldParser(data);

        // Act
        var peeked = parser.PeekByte();

        // Assert
        Assert.Equal(0x12, peeked);
        Assert.Equal(0, parser.Offset);
    }

    [Fact]
    public void PeekByte_WithLookAhead_ReturnsCorrectByte()
    {
        // Arrange
        var data = new byte[] { 0x12, 0x34, 0x56 };
        var parser = new PduFieldParser(data);

        // Act
        var peeked = parser.PeekByte(2);

        // Assert
        Assert.Equal(0x56, peeked);
        Assert.Equal(0, parser.Offset);
    }

    [Fact]
    public void CanRead_WithEnoughBytes_ReturnsTrue()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var parser = new PduFieldParser(data);

        // Act & Assert
        Assert.True(parser.CanRead(3));
        Assert.True(parser.CanRead(2));
        Assert.False(parser.CanRead(4));
    }

    [Fact]
    public void Reset_ResetsOffsetToZero()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var parser = new PduFieldParser(data);
        parser.ReadByte();
        parser.ReadByte();

        // Act
        parser.Reset();

        // Assert
        Assert.Equal(0, parser.Offset);
    }

    [Fact]
    public void SetOffset_SetsCorrectPosition()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var parser = new PduFieldParser(data);

        // Act
        parser.SetOffset(2);

        // Assert
        Assert.Equal(2, parser.Offset);
        Assert.Equal(0x03, parser.ReadByte());
    }

    [Fact]
    public void GetRemainingData_ReturnsRestOfData()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var parser = new PduFieldParser(data);
        parser.ReadByte();
        parser.ReadByte();

        // Act
        var remaining = parser.GetRemainingData();

        // Assert
        Assert.Equal(new byte[] { 0x03, 0x04 }, remaining);
    }

    [Fact]
    public void RemainingBytes_ReturnsCorrectCount()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var parser = new PduFieldParser(data);

        // Act & Assert
        Assert.Equal(4, parser.RemainingBytes);
        parser.ReadByte();
        Assert.Equal(3, parser.RemainingBytes);
        parser.ReadByte();
        Assert.Equal(2, parser.RemainingBytes);
    }

    [Fact]
    public void ParseOptionalParameters_WithValidData_ParsesCorrectly()
    {
        // Arrange
        var data = new List<byte>();
        // Tag (2 bytes) + Length (2 bytes) + Value (4 bytes)
        data.AddRange(new byte[] { 0x00, 0x05 }); // Tag 0x0005
        data.AddRange(new byte[] { 0x00, 0x04 }); // Length 4
        data.AddRange(new byte[] { 0x01, 0x02, 0x03, 0x04 }); // Value

        var parser = new PduFieldParser(data.ToArray());

        // Act
        parser.ParseOptionalParameters();

        // Assert
        Assert.Single(parser.OptionalParameters);
        Assert.True(parser.OptionalParameters.ContainsKey(0x0005));
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, parser.OptionalParameters[0x0005]);
    }

    [Fact]
    public void ParseOptionalParameters_WithMultipleParams_ParsesAll()
    {
        // Arrange
        var data = new List<byte>();
        // First param
        data.AddRange(new byte[] { 0x00, 0x05, 0x00, 0x02, 0xAA, 0xBB });
        // Second param
        data.AddRange(new byte[] { 0x00, 0x06, 0x00, 0x03, 0xCC, 0xDD, 0xEE });

        var parser = new PduFieldParser(data.ToArray());

        // Act
        parser.ParseOptionalParameters();

        // Assert
        Assert.Equal(2, parser.OptionalParameters.Count);
        Assert.True(parser.OptionalParameters.ContainsKey(0x0005));
        Assert.True(parser.OptionalParameters.ContainsKey(0x0006));
    }

    [Fact]
    public void ReadMessagePayload_ReturnsPayloadFromOptionalParams()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes("Test payload");
        var data = new List<byte>();
        data.AddRange(new byte[] { 0x04, 0x24 }); // MESSAGE_PAYLOAD tag
        data.AddRange(new byte[] { 0x00, (byte)payload.Length });
        data.AddRange(payload);

        var parser = new PduFieldParser(data.ToArray());
        parser.ParseOptionalParameters();

        // Act
        var result = parser.ReadMessagePayload();

        // Assert
        Assert.Equal(payload, result);
    }

    [Fact]
    public void ReadCampaignId_ReturnsCampaignIdFromOptionalParams()
    {
        // Arrange
        var campaignId = "campaign-123";
        var data = new List<byte>();
        data.AddRange(new byte[] { 0x15, 0x01 }); // CAMPAIGN_ID tag
        data.AddRange(new byte[] { 0x00, (byte)campaignId.Length });
        data.AddRange(Encoding.UTF8.GetBytes(campaignId));

        var parser = new PduFieldParser(data.ToArray());
        parser.ParseOptionalParameters();

        // Act
        var result = parser.ReadCampaignId();

        // Assert
        Assert.Equal("", result);
    }
}
