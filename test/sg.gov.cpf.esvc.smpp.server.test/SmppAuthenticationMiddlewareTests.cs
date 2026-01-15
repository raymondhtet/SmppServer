using Microsoft.Extensions.Logging;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using Ssg.gov.cpf.esvc.smpp.server.Middlewares;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class SmppAuthenticationMiddlewareTests
{
    private readonly Mock<ILogger<SmppAuthenticationMiddleware>> _mockLogger;
    private readonly Mock<PduProcessingMiddleware> _mockNextMiddleware;
    private readonly Mock<ISmppSession> _mockSession;

    public SmppAuthenticationMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<SmppAuthenticationMiddleware>>();
        _mockNextMiddleware = new Mock<PduProcessingMiddleware>();
        _mockSession = new Mock<ISmppSession>();
    }

    [Fact]
    public async Task HandleAsync_WithBindTransceiver_BypassesAuthenticationCheck()
    {
        // Arrange
        var middleware = new SmppAuthenticationMiddleware(_mockLogger.Object);
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.BindTransceiver,
            SequenceNumber = 1
        };
        var expectedResponse = CreateResponsePdu();

        _mockSession.Setup(x => x.IsAuthenticated).Returns(false);
        _mockNextMiddleware
            .Setup(x => x.HandleAsync(pdu, _mockSession.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        middleware.SetNext(_mockNextMiddleware.Object);

        // Act
        var result = await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.Equal(expectedResponse, result);
        _mockNextMiddleware.Verify(
            x => x.HandleAsync(pdu, _mockSession.Object, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_WithAuthenticatedSession_CallsNext()
    {
        // Arrange
        var middleware = new SmppAuthenticationMiddleware(_mockLogger.Object);
        var pdu = CreateTestPdu();
        var expectedResponse = CreateResponsePdu();

        _mockSession.Setup(x => x.IsAuthenticated).Returns(true);
        _mockNextMiddleware
            .Setup(x => x.HandleAsync(pdu, _mockSession.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        middleware.SetNext(_mockNextMiddleware.Object);

        // Act
        var result = await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.Equal(expectedResponse, result);
        _mockNextMiddleware.Verify(
            x => x.HandleAsync(pdu, _mockSession.Object, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_WithUnauthenticatedSession_ReturnsBindFailError()
    {
        // Arrange
        var middleware = new SmppAuthenticationMiddleware(_mockLogger.Object);
        var pdu = CreateTestPdu();

        _mockSession.Setup(x => x.IsAuthenticated).Returns(false);
        _mockSession.Setup(x => x.Id).Returns("test-session");

        middleware.SetNext(_mockNextMiddleware.Object);

        // Act
        var result = await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SmppConstants.SmppCommandStatus.ESME_RBINDFAIL, result.CommandStatus);
        Assert.Equal(pdu.SequenceNumber, result.SequenceNumber);
        // Verify response bit is set (0x80000000)
        Assert.Equal(pdu.CommandId | 0x80000000, result.CommandId);

        _mockNextMiddleware.Verify(
            x => x.HandleAsync(It.IsAny<SmppPdu>(), It.IsAny<ISmppSession>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleAsync_WithUnauthenticatedSession_LogsError()
    {
        // Arrange
        var middleware = new SmppAuthenticationMiddleware(_mockLogger.Object);
        var pdu = CreateTestPdu();
        var sessionId = "test-session-123";

        _mockSession.Setup(x => x.IsAuthenticated).Returns(false);
        _mockSession.Setup(x => x.Id).Returns(sessionId);

        middleware.SetNext(_mockNextMiddleware.Object);

        // Act
        await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unauthenticated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithDifferentCommands_ChecksAuthentication()
    {
        // Arrange
        var middleware = new SmppAuthenticationMiddleware(_mockLogger.Object);
        _mockSession.Setup(x => x.IsAuthenticated).Returns(true);
        middleware.SetNext(_mockNextMiddleware.Object);

        var commands = new[]
        {
            SmppConstants.SmppCommandId.SubmitSm,
            SmppConstants.SmppCommandId.EnquireLink,
            SmppConstants.SmppCommandId.Unbind
        };

        foreach (var commandId in commands)
        {
            var pdu = new SmppPdu
            {
                CommandId = commandId,
                SequenceNumber = 1
            };

            _mockNextMiddleware
                .Setup(x => x.HandleAsync(pdu, _mockSession.Object, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateResponsePdu());

            // Act
            await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

            // Assert
            _mockNextMiddleware.Verify(
                x => x.HandleAsync(pdu, _mockSession.Object, It.IsAny<CancellationToken>()),
                Times.Once
            );

            _mockNextMiddleware.Reset();
        }
    }

    [Fact]
    public async Task HandleAsync_WithUnauthenticatedEnquireLink_ReturnsError()
    {
        // Arrange
        var middleware = new SmppAuthenticationMiddleware(_mockLogger.Object);
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.EnquireLink,
            SequenceNumber = 1
        };

        _mockSession.Setup(x => x.IsAuthenticated).Returns(false);
        _mockSession.Setup(x => x.Id).Returns("test-session");

        middleware.SetNext(_mockNextMiddleware.Object);

        // Act
        var result = await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SmppConstants.SmppCommandStatus.ESME_RBINDFAIL, result.CommandStatus);
    }

    [Fact]
    public async Task HandleAsync_PreservesSequenceNumber()
    {
        // Arrange
        var middleware = new SmppAuthenticationMiddleware(_mockLogger.Object);
        var sequenceNumber = 12345u;
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.SubmitSm,
            SequenceNumber = sequenceNumber
        };

        _mockSession.Setup(x => x.IsAuthenticated).Returns(false);
        _mockSession.Setup(x => x.Id).Returns("test-session");

        middleware.SetNext(_mockNextMiddleware.Object);

        // Act
        var result = await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.Equal(sequenceNumber, result!.SequenceNumber);
    }

    [Fact]
    public void SetNext_ReturnsNextMiddleware()
    {
        // Arrange
        var middleware = new SmppAuthenticationMiddleware(_mockLogger.Object);

        // Act
        var result = middleware.SetNext(_mockNextMiddleware.Object);

        // Assert
        Assert.Equal(_mockNextMiddleware.Object, result);
    }

    private SmppPdu CreateTestPdu()
    {
        return new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.SubmitSm,
            CommandStatus = SmppConstants.SmppCommandStatus.ESME_ROK,
            SequenceNumber = 1,
            Body = new byte[] { 0x01, 0x02, 0x03 }
        };
    }

    private SmppPdu CreateResponsePdu()
    {
        return new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.SubmitSmResp,
            CommandStatus = SmppConstants.SmppCommandStatus.ESME_ROK,
            SequenceNumber = 1,
            Body = Array.Empty<byte>()
        };
    }
}