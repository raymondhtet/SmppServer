using System.Net.Sockets;

namespace Smpp.Server.Models
{
    public class SmppSession : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _sendLock;
        private bool _isPaused;

        public string? SystemId { get; set; }
        public string? Password { get; set; }

        public SmppSession(TcpClient client, ILogger logger)
        {
            _client = client;
            _stream = client.GetStream();
            _logger = logger;
            _sendLock = new SemaphoreSlim(1, 1);
        }

        public async Task<SmppPdu?> ReadPduAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_isPaused)
                    return null;

                var headerBuffer = new byte[16];
                var bytesRead = await _stream.ReadAsync(headerBuffer, 0, 16, cancellationToken);

                if (bytesRead < 16)
                    return null;

                var pdu = new SmppPdu();
                pdu.ParseHeader(headerBuffer);

                if (pdu.CommandLength > 16)
                {
                    var bodyLength = (int)pdu.CommandLength - 16;
                    var bodyBuffer = new byte[bodyLength];
                    bytesRead = await _stream.ReadAsync(bodyBuffer, 0, bodyLength, cancellationToken);

                    if (bytesRead < bodyLength)
                        return null;

                    pdu.Body = bodyBuffer;
                }

                return pdu;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading PDU");
                throw;
            }
        }

        public async Task SendPduAsync(SmppPdu pdu, CancellationToken cancellationToken = default)
        {
            try
            {
                await _sendLock.WaitAsync(cancellationToken);

                var data = pdu.GetBytes();
                await _stream.WriteAsync(data, 0, data.Length, cancellationToken);
                await _stream.FlushAsync(cancellationToken);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public void Pause()
        {
            _isPaused = true;
        }

        public void Resume()
        {
            _isPaused = false;
        }

        public void Close()
        {
            _client?.Close();
        }

        public void Dispose()
        {
            _sendLock?.Dispose();
            _stream?.Dispose();
            _client?.Dispose();
        }
    }
}
