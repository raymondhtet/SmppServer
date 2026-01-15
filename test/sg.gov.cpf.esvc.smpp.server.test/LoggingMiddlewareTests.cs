using Microsoft.Extensions.Logging;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using Ssg.gov.cpf.esvc.smpp.server.Middlewares;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class LoggingMiddlewareTests
{
    private readonly Mock<ILogger<LoggingMiddleware>> _mockLogger;
    private readonly Mock<PduProcessingMiddleware> _mockNextMiddleware;
    private readonly Mock<ISmppSession> _mockSession;

    public LoggingMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<LoggingMiddleware>>();
        _mockNextMiddleware = new Mock<PduProcessingMiddleware>();
        _mockSession = new Mock<ISmppSession>();
    }

    [Fact]
    public async Task HandleAsync_CallsNextMiddleware()
    {
        // Arrange
        var middleware = new LoggingMiddleware(_mockLogger.Object);
        var pdu = CreateTestPdu();
        var expectedResponse = CreateResponsePdu();

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
    public async Task HandleAsync_MeasuresExecutionTime()
    {
        // Arrange
        var middleware = new LoggingMiddleware(_mockLogger.Object);
        var pdu = CreateTestPdu();
        var responseDelay = TimeSpan.FromMilliseconds(100);

        _mockNextMiddleware
            .Setup(x => x.HandleAsync(pdu, _mockSession.Object, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(responseDelay);
                return CreateResponsePdu();
            });

        middleware.SetNext(_mockNextMiddleware.Object);

        // Act
        var startTime = DateTime.UtcNow;
        await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);
        var endTime = DateTime.UtcNow;

        // Assert
        var actualDuration = endTime - startTime;
        Assert.True(actualDuration >= responseDelay, "Execution should take at least as long as the mock delay");
    }

    [Fact]
    public async Task HandleAsync_WithException_PropagatesException()
    {
        // Arrange
        var middleware = new LoggingMiddleware(_mockLogger.Object);
        var pdu = CreateTestPdu();
        var expectedException = new InvalidOperationException("Test exception");

        _mockNextMiddleware
            .Setup(x => x.HandleAsync(pdu, _mockSession.Object, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        middleware.SetNext(_mockNextMiddleware.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None)
        );

        Assert.Equal(expectedException.Message, exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var middleware = new LoggingMiddleware(_mockLogger.Object);
        var pdu = CreateTestPdu();
        var cts = new CancellationTokenSource();

        _mockNextMiddleware
            .Setup(x => x.HandleAsync(pdu, _mockSession.Object, It.IsAny<CancellationToken>()))
            .Returns(async (SmppPdu p, ISmppSession s, CancellationToken ct) =>
            {
                await Task.Delay(1000, ct);
                return CreateResponsePdu();
            });

        middleware.SetNext(_mockNextMiddleware.Object);
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await middleware.HandleAsync(pdu, _mockSession.Object, cts.Token)
        );
    }

    [Fact]
    public async Task HandleAsync_WithMultipleRequests_HandlesEachIndependently()
    {
        // Arrange
        var middleware = new LoggingMiddleware(_mockLogger.Object);
        var pdu1 = CreateTestPdu();
        var pdu2 = CreateTestPdu();
        var response1 = CreateResponsePdu();
        var response2 = CreateResponsePdu();

        _mockNextMiddleware
            .Setup(x => x.HandleAsync(pdu1, _mockSession.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response1);

        _mockNextMiddleware
            .Setup(x => x.HandleAsync(pdu2, _mockSession.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response2);

        middleware.SetNext(_mockNextMiddleware.Object);

        // Act
        var result1 = await middleware.HandleAsync(pdu1, _mockSession.Object, CancellationToken.None);
        var result2 = await middleware.HandleAsync(pdu2, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.Equal(response1, result1);
        Assert.Equal(response2, result2);
    }

    [Fact]
    public void SetNext_ReturnsNextMiddleware()
    {
        // Arrange
        var middleware = new LoggingMiddleware(_mockLogger.Object);

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
