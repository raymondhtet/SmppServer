using System.Text;

namespace Smpp.Server.Helpers;

public class PduFieldParser(byte[] data)
{
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
            return Array.Empty<byte>();

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
}