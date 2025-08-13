using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Smpp.Server.Configurations;
using Smpp.Server.Constants;
using Smpp.Server.Interfaces;
using Smpp.Server.Models;

namespace Smpp.Server.Services;

public class SmppSession : ISmppSession, IDisposable
{
    private readonly TcpClient _client;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _sendLock;
    private readonly NetworkStream _stream;
    private bool _isPaused;
    private bool _disposed;

    public string Id { get; }
    
    public string SystemId { get; }
    public bool IsAuthenticated { get; set; }

    public SmppSession(TcpClient client, ILogger logger)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _stream = client.GetStream();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sendLock = new SemaphoreSlim(1, 1);
        
        // Log session creation
        var endpoint = _client.Client?.RemoteEndPoint?.ToString() ?? "Unknown";
        _logger.LogInformation("Session {SessionId} created for endpoint {Endpoint}", Id, endpoint);
    }
    
    /// <summary>
    /// Read a PDU from the network stream
    /// </summary>
    public async Task<SmppPdu?> ReadPduAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _isPaused)
            return null;

        try
        {
            var headerSize = SmppConstants.HeaderSize;
            // Read PDU header (16 bytes)
            var headerBuffer = new byte[headerSize];
            var bytesRead = await ReadExactAsync(headerBuffer, headerSize, cancellationToken);

            if (bytesRead < headerSize)
            {
                _logger.LogDebug("Session {SessionId} - Incomplete header read: {BytesRead}/headerSize", Id, bytesRead);
                return null;
            }

            // Parse header to get command length
            var pdu = new SmppPdu();
            pdu.ParseHeader(headerBuffer);

            _logger.LogDebug("Session {SessionId} - PDU header parsed: Length={Length}, CommandId=0x{CommandId:X8}", 
                Id, pdu.CommandLength, pdu.CommandId);

            // Read PDU body if present
            if (pdu.CommandLength > headerSize)
            {
                var bodyLength = (int)pdu.CommandLength - headerSize;
                
                // Validate reasonable body length (prevent DoS)
                if (bodyLength > 64 * 1024) // 64KB max
                {
                    _logger.LogWarning("Session {SessionId} - PDU body too large: {BodyLength} bytes", Id, bodyLength);
                    return null;
                }

                var bodyBuffer = new byte[bodyLength];
                bytesRead = await ReadExactAsync(bodyBuffer, bodyLength, cancellationToken);

                if (bytesRead < bodyLength)
                {
                    _logger.LogDebug("Session {SessionId} - Incomplete body read: {BytesRead}/{Expected}", 
                        Id, bytesRead, bodyLength);
                    return null;
                }

                pdu.Body = bodyBuffer;
            }

            return pdu;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Session {SessionId} - PDU read cancelled", Id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId} - Error reading PDU", Id);
            return null;
        }
    }


    /// <summary>
    /// Send a PDU over the network stream
    /// </summary>
    public async Task SendPduAsync(SmppPdu pdu, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SmppSession));

        try
        {
            await _sendLock.WaitAsync(cancellationToken);

            var data = pdu.GetBytes();
            await _stream.WriteAsync(data, 0, data.Length, cancellationToken);
            await _stream.FlushAsync(cancellationToken);

            _logger.LogDebug("Session {SessionId} - PDU sent: CommandId=0x{CommandId:X8}, Length={Length}", 
                Id, pdu.CommandId, data.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId} - Error sending PDU", Id);
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Pause the session (stops reading PDUs)
    /// </summary>
    public void Pause()
    {
        _isPaused = true;
        _logger.LogDebug("Session {SessionId} paused", Id);
    }

    /// <summary>
    /// Resume the session (allows reading PDUs)
    /// </summary>
    public void Resume()
    {
        _isPaused = false;
        _logger.LogDebug("Session {SessionId} resumed", Id);
    }

    /// <summary>
    /// Close the underlying TCP connection
    /// </summary>
    public void Close()
    {
        try
        {
            _client?.Close();
            _logger.LogInformation("Session {SessionId} closed", Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing session {SessionId}", Id);
        }
    }

    /// <summary>
    /// Helper method to read exact number of bytes from stream
    /// </summary>
    private async Task<int> ReadExactAsync(byte[] buffer, int count, CancellationToken cancellationToken)
    {
        int totalBytesRead = 0;
        
        while (totalBytesRead < count && !cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await _stream.ReadAsync(
                buffer, 
                totalBytesRead, 
                count - totalBytesRead, 
                cancellationToken);
                
            if (bytesRead == 0)
                break; // End of stream
                
            totalBytesRead += bytesRead;
        }
        
        return totalBytesRead;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _sendLock?.Dispose();
            _stream?.Dispose();
            _client?.Dispose();
            
            _logger.LogDebug("Session {SessionId} disposed", Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing session {SessionId}", Id);
        }
        finally
        {
            _disposed = true;
        }
    }
}