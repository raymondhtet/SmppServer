using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace sg.gov.cpf.esvc.smpp.client
{
    internal class SmppClient : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly string _systemId;
        private readonly string _password;
        private uint _sequenceNumber;
        private readonly SemaphoreSlim _sendLock;

        public SmppClient(string host, int port, string systemId, string password)
        {
            _client = new TcpClient(host, port);
            _stream = _client.GetStream();
            _systemId = systemId;
            _password = password;
            _sequenceNumber = 0;
            _sendLock = new SemaphoreSlim(1, 1);
        }

        private uint GetNextSequenceNumber()
        {
            return Interlocked.Increment(ref _sequenceNumber);
        }

        public async Task ConnectAsync()
        {
            // Prepare bind_transceiver PDU
            var sequenceNumber = GetNextSequenceNumber();

            // Create bind body
            var bodyBytes = new List<byte>();
            bodyBytes.AddRange(Encoding.ASCII.GetBytes(_systemId));
            bodyBytes.Add(0); // Null terminator
            bodyBytes.AddRange(Encoding.ASCII.GetBytes(_password));
            bodyBytes.Add(0); // Null terminator
            bodyBytes.AddRange(Encoding.ASCII.GetBytes("SMPP")); // System type
            bodyBytes.Add(0); // Null terminator
            bodyBytes.Add(0x34); // Interface version
            bodyBytes.Add(0); // Addr ton
            bodyBytes.Add(0); // Addr npi
            bodyBytes.AddRange(Encoding.ASCII.GetBytes("")); // Address range
            bodyBytes.Add(0); // Null terminator

            var bindPdu = new SmppPdu
            {
                CommandId = 0x00000009, // bind_transceiver
                SequenceNumber = sequenceNumber,
                CommandStatus = 0,
                Body = bodyBytes.ToArray()
            };

            await SendPduAsync(bindPdu);
            var response = await ReadPduAsync();

            if (response.CommandStatus != 0)
            {
                throw new Exception($"Bind failed with status: {response.CommandStatus}");
            }

            Console.WriteLine("Successfully bound to SMPP server");
        }

        public async Task SendMessageAsync(string sourceAddress, string destinationAddress, string message)
        {
            var sequenceNumber = GetNextSequenceNumber();

            // Create submit_sm body
            var bodyBytes = new List<byte>();
            bodyBytes.Add(0); // service_type (null terminated string)
            bodyBytes.Add(0); // source_addr_ton
            bodyBytes.Add(0); // source_addr_npi
            bodyBytes.AddRange(Encoding.ASCII.GetBytes(sourceAddress));
            bodyBytes.Add(0); // Null terminator
            bodyBytes.Add(1); // dest_addr_ton (1 = International)
            bodyBytes.Add(1); // dest_addr_npi (1 = E.164)
            bodyBytes.AddRange(Encoding.ASCII.GetBytes(destinationAddress));
            bodyBytes.Add(0); // Null terminator
            bodyBytes.Add(0); // esm_class
            bodyBytes.Add(0); // protocol_id
            bodyBytes.Add(0); // priority_flag
            bodyBytes.Add(0); // schedule_delivery_time (null terminated string)
            bodyBytes.Add(0); // validity_period (null terminated string)
            bodyBytes.Add(0); // registered_delivery
            bodyBytes.Add(0); // replace_if_present_flag
            bodyBytes.Add(0); // data_coding (0 = SMSC Default Alphabet)
            bodyBytes.Add(0); // sm_default_msg_id
            bodyBytes.Add((byte)message.Length); // sm_length
            bodyBytes.AddRange(Encoding.ASCII.GetBytes(message));

            var submitSmPdu = new SmppPdu
            {
                CommandId = 0x00000004, // submit_sm
                SequenceNumber = sequenceNumber,
                CommandStatus = 0,
                Body = bodyBytes.ToArray()
            };

            await SendPduAsync(submitSmPdu);
            var response = await ReadPduAsync();

            if (response.CommandStatus != 0)
            {
                throw new Exception($"Message submission failed with status: {response.CommandStatus}");
            }

            var messageId = response.Body != null ? Encoding.ASCII.GetString(response.Body).TrimEnd('\0') : "";
            Console.WriteLine($"Message sent successfully. Message ID: {messageId}");
        }

        private async Task SendPduAsync(SmppPdu pdu)
        {
            try
            {
                await _sendLock.WaitAsync();
                var data = pdu.GetBytes();
                await _stream.WriteAsync(data);
                await _stream.FlushAsync();
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task<SmppPdu> ReadPduAsync()
        {
            var headerBuffer = new byte[16];
            var bytesRead = await _stream.ReadAsync(headerBuffer, 0, 16);

            if (bytesRead < 16)
                throw new Exception("Failed to read PDU header");

            var pdu = new SmppPdu();
            pdu.ParseHeader(headerBuffer);

            if (pdu.CommandLength > 16)
            {
                var bodyLength = (int)pdu.CommandLength - 16;
                var bodyBuffer = new byte[bodyLength];
                bytesRead = await _stream.ReadAsync(bodyBuffer, 0, bodyLength);

                if (bytesRead < bodyLength)
                    throw new Exception("Failed to read PDU body");

                pdu.Body = bodyBuffer;
            }

            return pdu;
        }

        public void Dispose()
        {
            _sendLock?.Dispose();
            _stream?.Dispose();
            _client?.Dispose();
        }

    }
}
