using System.Text;
using static sg.gov.cpf.esvc.smpp.server.Constants.SmppConstants;

namespace sg.gov.cpf.esvc.smpp.server.Helpers;

public class PduFieldParser(byte[] data)
{
    public Dictionary<ushort, byte[]> OptionalParameters { get; set; } = [];
    private readonly byte[] _data = data ?? throw new ArgumentNullException(nameof(data));
    private int _offset = 0;

    /// <summary>
    /// Current parsing offset
    /// </summary>
    public int Offset => _offset;

    /// <summary>
    /// Remaining bytes in the data
    /// </summary>
    public int RemainingBytes => Math.Max(0, _data.Length - _offset);

    /// <summary>
    /// Read a null-terminated C-style string
    /// </summary>
    public string ReadCString()
    {
        var start = _offset;
        while (_offset < _data.Length && _data[_offset] != 0)
            _offset++;
        
        var result = Encoding.ASCII.GetString(_data, start, _offset - start);
        
        // Skip null terminator if present
        if (_offset < _data.Length && _data[_offset] == 0)
            _offset++;
        
        return result;
    }

    /// <summary>
    /// Read a single byte
    /// </summary>
    public byte ReadByte()
    {
        if (_offset >= _data.Length)
            return 0;
        return _data[_offset++];
    }

    /// <summary>
    /// Read a 16-bit unsigned integer (big-endian)
    /// </summary>
    public ushort ReadUInt16()
    {
        if (_offset + 1 >= _data.Length)
            return 0;
            
        var result = (ushort)((_data[_offset] << 8) | _data[_offset + 1]);
        _offset += 2;
        return result;
    }

    /// <summary>
    /// Read a 32-bit unsigned integer (big-endian)
    /// </summary>
    public uint ReadUInt32()
    {
        if (_offset + 3 >= _data.Length)
            return 0;
            
        var result = (uint)((_data[_offset] << 24) | (_data[_offset + 1] << 16) | 
                           (_data[_offset + 2] << 8) | _data[_offset + 3]);
        _offset += 4;
        return result;
    }

    /// <summary>
    /// Read a byte array prefixed by its length (as a single byte)
    /// Used for short_message field in SMPP
    /// </summary>
    public byte[] ReadShortMessage()
    {
        var length = ReadByte();
        
        if (length == 0 || _offset + length > _data.Length)
            return [];

        var message = new byte[length];
        Array.Copy(_data, _offset, message, 0, length);
        _offset += length;
        return message;
    }

    /// <summary>
    /// Read a fixed-length byte array
    /// </summary>
    public byte[] ReadBytes(int count)
    {
        if (count <= 0 || _offset + count > _data.Length)
            return Array.Empty<byte>();

        var result = new byte[count];
        Array.Copy(_data, _offset, result, 0, count);
        _offset += count;
        return result;
    }

    /// <summary>
    /// Skip a specified number of bytes
    /// </summary>
    public PduFieldParser Skip(int bytes)
    {
        _offset = Math.Min(_data.Length, _offset + bytes);
        return this;
    }

    /// <summary>
    /// Skip a single byte
    /// </summary>
    public PduFieldParser SkipByte() => Skip(1);

    /// <summary>
    /// Peek at a byte without advancing the offset
    /// </summary>
    public byte PeekByte(int lookAhead = 0)
    {
        var peekOffset = _offset + lookAhead;
        return peekOffset < _data.Length ? _data[peekOffset] : (byte)0;
    }

    /// <summary>
    /// Check if we can read at least the specified number of bytes
    /// </summary>
    public bool CanRead(int bytes) => _offset + bytes <= _data.Length;

    /// <summary>
    /// Reset the parser to the beginning
    /// </summary>
    public void Reset() => _offset = 0;

    /// <summary>
    /// Set the parsing position to a specific offset
    /// </summary>
    public void SetOffset(int offset)
    {
        _offset = Math.Max(0, Math.Min(_data.Length, offset));
    }

    /// <summary>
    /// Get remaining data from current position
    /// </summary>
    public byte[] GetRemainingData()
    {
        if (_offset >= _data.Length)
            return [];

        var remaining = new byte[_data.Length - _offset];
        Array.Copy(_data, _offset, remaining, 0, remaining.Length);
        return remaining;
    }

    public void ParseOptionalParameters()
    {
        OptionalParameters.Clear();

        if (_offset >= _data.Length)
        {
            Console.WriteLine("No optional parameters found - start index beyond body length");
            return;
        }

        var currentIndex = _offset;
        var paramCount = 0;

        while (currentIndex <= _data.Length - 4) // Need at least 4 bytes (2 for tag, 2 for length)
        {
            if (currentIndex + 4 > _data.Length)
            {
                Console.WriteLine($"Not enough bytes for next optional parameter at index {currentIndex}");
                break;
            }

            // Read tag (2 bytes, big-endian)
            var tag = (ushort)((_data![currentIndex] << 8) | _data[currentIndex + 1]);

            // Read length (2 bytes, big-endian)
            var length = (ushort)((_data![currentIndex + 2] << 8) | _data[currentIndex + 3]);

            Console.WriteLine($"Optional param #{paramCount + 1}: Tag=0x{tag:X4}, Length={length}");

            // Validate length
            if (currentIndex + 4 + length > _data.Length)
            {
                Console.WriteLine($"Optional parameter length {length} exceeds remaining body bytes");
                break;
            }

            // Read value
            byte[] value;
            if (length > 0)
            {
                value = new byte[length];
                Array.Copy(_data, currentIndex + 4, value, 0, length);
            }
            else
            {
                value = Array.Empty<byte>();
            }

            OptionalParameters[tag] = value;

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

    public byte[] ReadMessagePayload()
    {
        var payload = OptionalParameters.ContainsKey(OptionalParameterTags.MESSAGE_PAYLOAD) ? OptionalParameters[OptionalParameterTags.MESSAGE_PAYLOAD] : [];

        return payload;
    }

    public string ReadCampaignId()
    {
        var campaignId = OptionalParameters.ContainsKey(OptionalParameterTags.CAMPAIGN_ID) ? OptionalParameters[OptionalParameterTags.CAMPAIGN_ID] : [];

        return Encoding.UTF8.GetString(campaignId);
    }

    public int? ReadDelayConfig()
    {
        if (!OptionalParameters.TryGetValue(OptionalParameterTags.DELAY, out byte[]? delay))
            return null;

        return int.Parse(Encoding.ASCII.GetString(delay));
    }
}