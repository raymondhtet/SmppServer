using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Handlers;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using sg.gov.cpf.esvc.smpp.server.Models.DTOs;
using System.Text;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class SubmitSmHandlerTests
{
    private readonly Mock<ILogger<SubmitSmHandler>> _mockLogger;
    private readonly Mock<IMessageConcatenationService> _mockConcatenationService;
    private readonly Mock<IMessageProcessor> _mockMessageProcessor;
    private readonly Mock<ISmppSession> _mockSession;
    private readonly TelemetryClient _telemetryClient;

    public SubmitSmHandlerTests()
    {
        _mockLogger = new Mock<ILogger<SubmitSmHandler>>();
        _mockConcatenationService = new Mock<IMessageConcatenationService>();
        _mockMessageProcessor = new Mock<IMessageProcessor>();
        _mockSession = new Mock<ISmppSession>();

        var telemetryConfig = new TelemetryConfiguration();
        _telemetryClient = new TelemetryClient(telemetryConfig);

        _mockSession.Setup(x => x.SystemId).Returns("test-system");
    }

    [Fact]
    public async Task CanHandle_WithSubmitSmCommand_ReturnsTrue()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.SubmitSm
        };

        // Act
        var result = await handler.CanHandle(pdu);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanHandle_WithDifferentCommand_ReturnsFalse()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.EnquireLink
        };

        // Act
        var result = await handler.CanHandle(pdu);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Handle_WithSingleMessage_SendsResponseAndProcessesMessage()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = CreateSubmitSmPdu("Hello World");

        _mockConcatenationService
            .Setup(x => x.ProcessMessagePartAsync(
                It.IsAny<SmppConstants.ConcatenationInfo?>(),
                false,
                It.IsAny<string>(),
                It.IsAny<SubmitSmRequest>()))
            .ReturnsAsync(new ConcatenationResult(true, "Hello World"));

        _mockMessageProcessor
            .Setup(x => x.ProcessCompleteMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<ISmppSession>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await handler.Handle(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(SmppConstants.SmppCommandId.SubmitSmResp, response.CommandId);
        _mockSession.Verify(
            x => x.SendPduAsync(It.IsAny<SmppPdu>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _mockMessageProcessor.Verify(
            x => x.ProcessCompleteMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "Hello World",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                _mockSession.Object,
                It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_WithIncompleteMultipartMessage_SendsResponseOnly()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = CreateSubmitSmPdu("Part 1");

        _mockConcatenationService
            .Setup(x => x.ProcessMessagePartAsync(
                It.IsAny<SmppConstants.ConcatenationInfo?>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<SubmitSmRequest>()))
            .ReturnsAsync(new ConcatenationResult(false, null));

        // Act
        var response = await handler.Handle(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(SmppConstants.SmppCommandId.SubmitSmResp, response.CommandId);
        _mockSession.Verify(
            x => x.SendPduAsync(It.IsAny<SmppPdu>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _mockMessageProcessor.Verify(
            x => x.ProcessCompleteMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<ISmppSession>(),
                It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_WithException_ReturnsErrorResponse()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = CreateSubmitSmPdu("Test");

        _mockConcatenationService
            .Setup(x => x.ProcessMessagePartAsync(
                It.IsAny<SmppConstants.ConcatenationInfo?>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<SubmitSmRequest>()))
            .ThrowsAsync(new Exception("Processing error"));

        // Act
        var response = await handler.Handle(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(SmppConstants.SmppCommandId.SubmitSmResp, response.CommandId);
        Assert.Equal(SmppConstants.SmppCommandStatus.ESME_RSYSERR, response.CommandStatus);
    }

    [Fact]
    public async Task Handle_GeneratesUniqueMessageIds()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu1 = CreateSubmitSmPdu("Message 1");
        var pdu2 = CreateSubmitSmPdu("Message 2");

        _mockConcatenationService
            .Setup(x => x.ProcessMessagePartAsync(
                It.IsAny<SmppConstants.ConcatenationInfo?>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<SubmitSmRequest>()))
            .ReturnsAsync(new ConcatenationResult(true, "Message"));

        // Act
        var response1 = await handler.Handle(pdu1, _mockSession.Object, CancellationToken.None);
        var response2 = await handler.Handle(pdu2, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotEqual(response1!.MessageId, response2!.MessageId);
    }

    [Fact]
    public async Task Handle_LogsSubmitSmProcessing()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = CreateSubmitSmPdu("Test message");

        _mockConcatenationService
            .Setup(x => x.ProcessMessagePartAsync(
                It.IsAny<SmppConstants.ConcatenationInfo?>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<SubmitSmRequest>()))
            .ReturnsAsync(new ConcatenationResult(true, "Test message"));

        // Act
        await handler.Handle(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Submit_SM")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private SubmitSmHandler CreateHandler()
    {
        return new SubmitSmHandler(
            _mockLogger.Object,
            _mockConcatenationService.Object,
            _mockMessageProcessor.Object,
            _telemetryClient
        );
    }

    private SmppPdu CreateSubmitSmPdu(string message)
    {
        var body = new List<byte>();

        // service_type
        body.Add(0x00);

        // source_addr_ton, source_addr_npi
        body.Add(0x01);
        body.Add(0x01);

        // source_addr
        body.AddRange(Encoding.ASCII.GetBytes("1234567890"));
        body.Add(0x00);

        // dest_addr_ton, dest_addr_npi
        body.Add(0x01);
        body.Add(0x01);

        // destination_addr
        body.AddRange(Encoding.ASCII.GetBytes("0987654321"));
        body.Add(0x00);

        // esm_class
        body.Add(0x00);

        // protocol_id
        body.Add(0x00);

        // priority_flag
        body.Add(0x00);

        // schedule_delivery_time
        body.Add(0x00);

        // validity_period
        body.Add(0x00);

        // registered_delivery
        body.Add(0x01);

        // replace_if_present_flag
        body.Add(0x00);

        // data_coding
        body.Add(0x00);

        // sm_default_msg_id
        body.Add(0x00);

        // sm_length
        var messageBytes = Encoding.ASCII.GetBytes(message);
        body.Add((byte)messageBytes.Length);

        // short_message
        body.AddRange(messageBytes);

        return new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.SubmitSm,
            CommandStatus = SmppConstants.SmppCommandStatus.ESME_ROK,
            SequenceNumber = 1,
            Body = body.ToArray()
        };
    }
}
