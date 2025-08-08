using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Smpp.Server.Models;
using Smpp.Server.Models.AppSettings;
using static Smpp.Server.Constants.SmppConstants;

namespace Smpp.Server.BackgroundServices;

public class SmppServer : BackgroundService
{
    private readonly SemaphoreSlim _connectionLimiter;
    private readonly ConcurrentDictionary<string, int> _expectedParts = new();
    private readonly TcpListener _listener;

    private readonly ILogger<SmppServer> _logger;
    private readonly MessageTracker _messageTracker;
    private readonly ConcurrentDictionary<string, List<string>> _multipartBuffers = new();
    private readonly SmppServerConfiguration _serverConfiguration;

    private readonly ConcurrentDictionary<string, SmppSession> _sessions = new();

    private uint _sequenceNumber;

    public SmppServer(
        IOptions<SmppServerConfiguration> serverConfiguration,
        ILogger<SmppServer> logger,
        MessageTracker messageTracker
    )
    {
        _logger = logger;
        _serverConfiguration = serverConfiguration.Value;
        _connectionLimiter = new SemaphoreSlim(_serverConfiguration.MaxConcurrentConnections);
        _listener = new TcpListener(IPAddress.Any, _serverConfiguration.Port);
        _messageTracker = messageTracker;
    }

    public bool IsRunning { get; internal set; }
    public int ActiveSessionsCount { get; internal set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _ = Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Stale message cleanup job is running at: {time}", DateTimeOffset.Now);
                    _messageTracker.CleanUpStaleMessagesParts(TimeSpan.Parse(_serverConfiguration.StaleCleanUpInterval));
                    await Task.Delay(TimeSpan.Parse(_serverConfiguration.CleanUpJobInterval), stoppingToken);
                    _logger.LogInformation("Stale message cleanup job is finished at: {time}", DateTimeOffset.Now);
                }
            }, stoppingToken);
            
            _listener.Start();
            _logger.LogInformation("SMPP Server started on port {Port}", _serverConfiguration.Port);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("SMPP server is running...");
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
                    _logger.LogError(ex, "An error occurred in the SMPP server");
                    _connectionLimiter.Release();

                    _logger.LogError(ex, "Restarting the server in 5 seconds");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
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
                var pdu = await session.ReadPduAsync(stoppingToken);
                if (pdu == null) break;

                await ProcessPduAsync(session, pdu, stoppingToken);
            }
        }
        finally
        {
            session.Dispose();
            client.Dispose();
        }
    }

    private async Task ProcessPduAsync(SmppSession session, SmppPdu pdu, CancellationToken stoppingToken)
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
        string recipientNumber = pdu.GetSourceAddress;
        string destinationAddress = pdu.GetDestinationAddress;
        var messageId = Guid.NewGuid().ToString("N")[..10];
        
        var (isComplete, message) = _messageTracker.TrackMessageParts(pdu, recipientNumber, destinationAddress);

        if (!isComplete)
        {
            // Send response for this segment
            await SendAckAsync(session, pdu.SequenceNumber, messageId);
            return;
        }

        // Process complete message
        await ProcessCompleteMessage(session, messageId, message, pdu.SequenceNumber);

        

        _logger.LogInformation(
            "{SessionSystemId} attempting to send message to {RecipientNumber}, messageId: {SmppMessageId}, and message body: {MessageBody}",
            session.SystemId, recipientNumber, messageId, message);

        try
        {
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

    private async Task SendAckAsync(SmppSession session, uint pduSequenceNumber, string messageId)
    {
        SmppPdu response = new()
        {
            CommandId = SmppCommandId.SubmitSmResp,
            SequenceNumber = pduSequenceNumber,
            CommandStatus = SmppCommandStatus.ESME_ROK,
            Body = Encoding.ASCII.GetBytes(messageId)
        };
        
        await session.SendPduAsync(response);
        _logger.LogInformation("Acknowledge message {MessageId}", messageId);
    }

    private async Task ProcessCompleteMessage(
        SmppSession session,
        string messageId,
        string? message,
        uint sequenceNumber)
    {
        _logger.LogInformation("Processing complete message - {message}", message);
        
        // Send final acknowledgment
        await SendAckAsync(session, sequenceNumber, messageId);

        // Send delivery receipt
        await SendDeliveryReceiptAsync(
            session,
            messageId: messageId,
            new DeliveryStatus
            {
                MessageState = MessageState.DELIVERED,
                ErrorStatus = "DELIVRD",
                ErrorCode = "000"
            });
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

        if (session.SystemId != null) _sessions.TryRemove(session.SystemId, out _);

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