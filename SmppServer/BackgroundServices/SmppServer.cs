using Microsoft.Extensions.Options;
using Smpp.Server.Models;
using Smpp.Server.Models.AppSettings;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static Smpp.Server.Constants.SmppConstants;

namespace Smpp.Server.BackgroundServices
{
    public class SmppServer : BackgroundService
    {
        private readonly TcpListener _listener;
        private readonly SmppServerConfiguration _serverConfiguration;

        private readonly ILogger<SmppServer> _logger;
        private readonly SemaphoreSlim _connectionLimiter;
        
        private readonly ConcurrentDictionary<string, SmppSession> _sessions = new();
        private readonly ConcurrentDictionary<string, List<string>> _multipartBuffers = new();
        private readonly ConcurrentDictionary<string, int> _expectedParts = new();

        public SmppServer(
            IOptions<SmppServerConfiguration> serverConfiguration,
            ILogger<SmppServer> logger
            )
        {
            _logger = logger;
            _serverConfiguration = serverConfiguration.Value;
            _connectionLimiter = new SemaphoreSlim(_serverConfiguration.MaxConcurrentConnections);
            _listener = new TcpListener(IPAddress.Any, _serverConfiguration.Port);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _listener.Start();
                _logger.LogInformation("SMPP Server started on port {Port}", _serverConfiguration.Port);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                        _ = HandleClientAsync(client, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error accepting client connection");
                        _connectionLimiter.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in SMPP server");
            }
            finally
            {
                _listener.Stop();
                _logger.LogInformation("SMPP Server stopped");
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken stoppingToken)
        {
            var session = new SmppSession(client, _logger);
            try
            {
                var endpoint = client.Client?.RemoteEndPoint?.ToString();
                _logger.LogInformation("New client connected from {endpoint}", endpoint);

                while (!stoppingToken.IsCancellationRequested && client.Connected)
                {
                    var pdu = await session.ReadPduAsync();
                    if (pdu == null) break;

                    await ProcessPduAsync(session, pdu, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling client connection");
            }
            finally
            {
                session.Dispose();
                client.Dispose();
            }
        }

        private async Task ProcessPduAsync(SmppSession session, SmppPdu pdu, CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("PDU command: {CommandId}", pdu.CommandId);
                switch (pdu.CommandId)
                {
                    case SmppCommandId.BindTransceiver:
                        await HandleBindTransceiverAsync(session, pdu);
                        break;

                    case SmppCommandId.SubmitSm:
                        await HandleSubmitSmAsync(session, pdu, stoppingToken);
                        break;

                    case SmppCommandId.EnquireLink:
                        await HandleEnquireLinkAsync(session, pdu);
                        break;

                    case SmppCommandId.Unbind:
                        await HandleUnbindAsync(session, pdu);
                        break;

                    default:
                        _logger.LogError("Unhandled PDU command {CommandId}", pdu.CommandId);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PDU: {CommandId}", pdu.CommandId);
            }
        }

        private async Task HandleBindTransceiverAsync(SmppSession session, SmppPdu pdu)
        {
            session.Pause();

            try
            {
                var systemId = pdu.GetString(0);
                var password = pdu.GetString(systemId.Length + 1);

                _logger.LogInformation("{SystemID} attempting to establish a connection", systemId);

                _logger.LogInformation("Server credentials are {SessionUsername} and {SessionPassword}",
                    _serverConfiguration.SessionUsername,
                    _serverConfiguration.SessionPassword);

                if (systemId != _serverConfiguration.SessionUsername || password != _serverConfiguration.SessionPassword)
                {
                    _logger.LogWarning("{SystemID} failed to connect - Invalid credentials", systemId);

                    await session.SendPduAsync(new SmppPdu
                    {
                        CommandId = SmppCommandId.BindTransceiverResp,
                        SequenceNumber = pdu.SequenceNumber,
                        CommandStatus = SmppCommandStatus.ESME_RBINDFAIL
                    });

                    session.Close();
                    return;
                }

                await session.SendPduAsync(new SmppPdu
                {
                    CommandId = SmppCommandId.BindTransceiverResp,
                    SequenceNumber = pdu.SequenceNumber,
                    CommandStatus = SmppCommandStatus.ESME_ROK
                });

                session.SystemId = systemId;
                session.Password = password;
                _sessions.TryAdd(systemId, session);

                session.Resume();
                _logger.LogInformation($"{systemId} successfully connected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bind_transceiver");
                throw;
            }
        }

        private async Task HandleSubmitSmAsync(SmppSession session, SmppPdu pdu, CancellationToken stoppingToken)
        {
            var sequenceNumber = pdu.SequenceNumber;
            var recipientNumber = pdu.GetString(pdu.GetByte(2) + 3);
            var messageContent = pdu.GetMessageContent();
            var messageBytes = pdu.Body; // Get raw bytes if available

            if (messageBytes.Length > 0 && messageBytes[0] == 0x05 && messageBytes[1] == 0x00)
            {
                var referenceNumber = messageBytes[3].ToString("X2");
                var totalParts = messageBytes[4];
                var currentPart = messageBytes[5];
                var udhLength = 6;

                var key = $"{session.SystemId}:{referenceNumber}";

                var messagePart = Encoding.UTF8.GetString(messageBytes.Skip(udhLength).ToArray());

                _multipartBuffers.AddOrUpdate(key, new List<string> { messagePart }, (k, list) =>
                {
                    list.Add(messagePart);
                    return list;
                });

                _expectedParts.TryAdd(key, totalParts);

                if (_multipartBuffers[key].Count == totalParts)
                {
                    var fullMessage = string.Join("", _multipartBuffers[key]);

                    // Clean up
                    _multipartBuffers.TryRemove(key, out _);
                    _expectedParts.TryRemove(key, out _);

                    _logger.LogInformation("Reassembled full message: {FullMessage}", fullMessage);
                    // Proceed to process fullMessage here
                }
                else
                {
                    _logger.LogInformation("Received part {CurrentPart}/{TotalParts} of message", currentPart, totalParts);
                }
            }
            else
            {
                // Single-part message
                _logger.LogInformation("Received single-part message: {Message}", messageContent.Message);
            }
            
            var smppMessageId = Guid.NewGuid().ToString("N")[..10];

            _logger.LogInformation("{SessionSystemId} attempting to send message to {RecipientNumber}, messageId: {SmppMessageId}, and message body: {MessageBody}", session.SystemId, recipientNumber, smppMessageId, messageContent.Message);

            try
            {
                // Send submit_sm_resp first
                await session.SendPduAsync(new SmppPdu
                {
                    CommandId = SmppCommandId.SubmitSmResp,
                    SequenceNumber = sequenceNumber,
                    CommandStatus = SmppCommandStatus.ESME_ROK,
                    Body = Encoding.ASCII.GetBytes(smppMessageId)
                });
                /*
                // Send message to Postman API
                var response = await _postmanApiService.SendMessageAsync(
                    session.SystemId,
                    session.Password,
                    recipientNumber,
                    messageBody,
                    messageLanguage,
                    stoppingToken);

                if (response.Error != null)
                {
                    var deliveryStatus = GetDeliveryStatusForError(response.Error);
                    await SendDeliveryReceiptAsync(session, smppMessageId, deliveryStatus);
                }
                else
                {
                    // Send successful delivery receipt
                    await SendDeliveryReceiptAsync(session, smppMessageId, new DeliveryStatus
                    {
                        MessageState = MessageState.DELIVERED,
                        ErrorStatus = "DELIVRD",
                        ErrorCode = "000"
                    });
                }
                */
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing submit_sm");
                /*
                await SendDeliveryReceiptAsync(session, smppMessageId, new DeliveryStatus
                {
                    MessageState = MessageState.UNDELIVERABLE,
                    ErrorStatus = "UNDELIV",
                    ErrorCode = "005"
                });
                */
            }
        }

        private async Task HandleEnquireLinkAsync(SmppSession session, SmppPdu pdu)
        {
            await session.SendPduAsync(new SmppPdu
            {
                CommandId = SmppCommandId.EnquireLinkResp,
                SequenceNumber = pdu.SequenceNumber,
                CommandStatus = SmppCommandStatus.ESME_ROK
            });
        }

        private async Task HandleUnbindAsync(SmppSession session, SmppPdu pdu)
        {
            await session.SendPduAsync(new SmppPdu
            {
                CommandId = SmppCommandId.UnbindResp,
                SequenceNumber = pdu.SequenceNumber,
                CommandStatus = SmppCommandStatus.ESME_ROK
            });

            if (session.SystemId != null)
            {
                _sessions.TryRemove(session.SystemId, out _);
            }

            session.Close();
        }

        private async Task SendDeliveryReceiptAsync(SmppSession session, string messageId, DeliveryStatus status)
        {
            var shortMessage = $"id:{messageId} stat:{status.ErrorStatus} err:{status.ErrorCode}";

            await session.SendPduAsync(new SmppPdu
            {
                CommandId = SmppCommandId.DeliverSm,
                SequenceNumber = GetNextSequenceNumber(),
                CommandStatus = SmppCommandStatus.ESME_ROK,
                Body = Encoding.ASCII.GetBytes(shortMessage)
            });
        }
        /*
        private DeliveryStatus GetDeliveryStatusForError(ApiError error)
        {
            return error.Code switch
            {
                "parameter_invalid" or "invalid_ip_address_used" => new DeliveryStatus
                {
                    MessageState = MessageState.UNDELIVERABLE,
                    ErrorStatus = "UNDELIV",
                    ErrorCode = "001"
                },
                "authentication_required" or "invalid_api_key_provided" => new DeliveryStatus
                {
                    MessageState = MessageState.UNDELIVERABLE,
                    ErrorStatus = "UNDELIV",
                    ErrorCode = "002"
                },
                "invalid_path" => new DeliveryStatus
                {
                    MessageState = MessageState.UNDELIVERABLE,
                    ErrorStatus = "UNDELIV",
                    ErrorCode = "003"
                },
                "too_many_requests" => new DeliveryStatus
                {
                    MessageState = MessageState.REJECTED,
                    ErrorStatus = "REJECTD",
                    ErrorCode = "004"
                },
                _ => new DeliveryStatus
                {
                    MessageState = MessageState.UNDELIVERABLE,
                    ErrorStatus = "UNDELIV",
                    ErrorCode = "005"
                }
            };
        }
        */
        private uint _sequenceNumber;

        public bool IsRunning { get; internal set; }
        public object ActiveSessionsCount { get; internal set; }

        private uint GetNextSequenceNumber()
        {
            return Interlocked.Increment(ref _sequenceNumber);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _listener.Stop();

            foreach (var session in _sessions.Values)
            {
                session.Close();
                session.Dispose();
            }

            _sessions.Clear();
            _connectionLimiter.Dispose();

            await base.StopAsync(cancellationToken);
        }
    }
}
