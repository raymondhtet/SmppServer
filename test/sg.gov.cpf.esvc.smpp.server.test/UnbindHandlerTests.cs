using Microsoft.Extensions.Logging;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Handlers;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class UnbindHandlerTests
{
    private readonly Mock<ILogger<EnquireLinkHandler>> _mockLogger;
    private readonly Mock<ISmppSession> _mockSession;

    public UnbindHandlerTests()
    {
        _mockLogger = new Mock<ILogger<EnquireLinkHandler>>();
        _mockSession = new Mock<ISmppSession>();
        _mockSession.Setup(x => x.SystemId).Returns("test-system");
    }

    [Fact]
    public async Task CanHandle_WithUnbindCommand_ReturnsTrue()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.Unbind
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
    public async Task Handle_ReturnsUnbindResponse()
    {
        // Arrange
        var handler = CreateHandler();
        var sequenceNumber = 12345u;
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.Unbind,
            SequenceNumber = sequenceNumber
        };

        // Act
        var response = await handler.Handle(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(SmppConstants.SmppCommandId.UnbindResp, response.CommandId);
        Assert.Equal(sequenceNumber, response.SequenceNumber);
        Assert.Equal(SmppConstants.SmppCommandStatus.ESME_ROK, response.CommandStatus);
    }

    [Fact]
    public async Task Handle_SchedulesSessionClose()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.Unbind,
            SequenceNumber = 1
        };

        // Act
        await handler.Handle(pdu, _mockSession.Object, CancellationToken.None);

        // Wait for scheduled task
        await Task.Delay(200);

        // Assert
        _mockSession.Verify(x => x.Close(), Times.Once);
    }

    [Fact]
    public async Task Handle_LogsUnbindAction()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.Unbind,
            SequenceNumber = 1
        };

        // Act
        await handler.Handle(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("unbinding")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCancellation_StillReturnsResponse()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.Unbind,
            SequenceNumber = 1
        };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var response = await handler.Handle(pdu, _mockSession.Object, cts.Token);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(SmppConstants.SmppCommandId.UnbindResp, response.CommandId);
    }

    private UnbindHandler CreateHandler()
    {
        return new UnbindHandler(_mockLogger.Object);
    }
}
