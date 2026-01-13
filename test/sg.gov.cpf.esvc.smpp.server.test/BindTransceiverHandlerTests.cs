using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Handlers;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class BindTransceiverHandlerTests
{
    private readonly Mock<ILogger<BindTransceiverHandler>> _mockLogger;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<ISmppSession> _mockSession;

    public BindTransceiverHandlerTests()
    {
        _mockLogger = new Mock<ILogger<BindTransceiverHandler>>();
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockSession = new Mock<ISmppSession>();
    }

    [Fact]
    public async Task CanHandle_WithBindTransceiverCommand_ReturnsTrue()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.BindTransceiver
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
            CommandId = SmppConstants.SmppCommandId.SubmitSm
        };

        // Act
        var result = await handler.CanHandle(pdu);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_AuthenticatesAndReturnsSuccessResponse()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = CreateBindTransceiverPdu("testuser", "testpass");

        _mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var response = await handler.Handle(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(SmppConstants.SmppCommandId.BindTransceiverResp, response.CommandId);
        Assert.Equal(SmppConstants.SmppCommandStatus.ESME_ROK, response.CommandStatus);
    }

    [Fact]
    public async Task Handle_WithInvalidCredentials_ReturnsErrorResponse()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = CreateBindTransceiverPdu("testuser", "wrongpass");

        _mockAuthService
            .Setup(x => x.AuthenticateAsync("testuser", "wrongpass"))
            .ReturnsAsync(false);

        // Act
        var response = await handler.Handle(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(SmppConstants.SmppCommandId.BindTransceiverResp, response.CommandId);
        Assert.NotEqual(SmppConstants.SmppCommandStatus.ESME_ROK, response.CommandStatus);
        _mockSession.Verify(x => x.Pause(), Times.Once);
        _mockSession.Verify(x => x.Resume(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithException_ReturnsErrorResponse()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = CreateBindTransceiverPdu("testuser", "testpass");

        _mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Auth service error"));

        // Act
        var response = await handler.Handle(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(SmppConstants.SmppCommandId.BindTransceiverResp, response.CommandId);
        Assert.NotEqual(SmppConstants.SmppCommandStatus.ESME_ROK, response.CommandStatus);
    }

    [Fact]
    public async Task Handle_WithInvalidCredentials_SchedulesSessionClose()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = CreateBindTransceiverPdu("testuser", "wrongpass");

        _mockAuthService
            .Setup(x => x.AuthenticateAsync("testuser", "wrongpass"))
            .ReturnsAsync(false);

        // Act
        await handler.Handle(pdu, _mockSession.Object, CancellationToken.None);

        // Wait a bit for the scheduled task
        await Task.Delay(200);

        // Assert
        _mockSession.Verify(x => x.Close(), Times.Once);
    }

    [Fact]
    public async Task Handle_LogsAuthenticationAttempt()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = CreateBindTransceiverPdu("testuser", "testpass");

        _mockAuthService
            .Setup(x => x.AuthenticateAsync("testuser", "testpass"))
            .ReturnsAsync(true);

        // Act
        await handler.Handle(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("attempting to establish")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private BindTransceiverHandler CreateHandler()
    {
        return new BindTransceiverHandler(_mockLogger.Object, _mockAuthService.Object);
    }

    private SmppPdu CreateBindTransceiverPdu(string systemId, string password)
    {
        var body = new List<byte>();

        // service_type (null-terminated)
        body.Add(0x00);

        // system_id
        body.AddRange(Encoding.ASCII.GetBytes(systemId));
        body.Add(0x00);

        // password
        body.AddRange(Encoding.ASCII.GetBytes(password));
        body.Add(0x00);

        // system_type
        body.Add(0x00);

        // interface_version
        body.Add(0x34);

        // addr_ton
        body.Add(0x00);

        // addr_npi
        body.Add(0x00);

        // address_range
        body.Add(0x00);

        return new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.BindTransceiver,
            CommandStatus = SmppConstants.SmppCommandStatus.ESME_ROK,
            SequenceNumber = 1,
            Body = body.ToArray()
        };
    }
}
