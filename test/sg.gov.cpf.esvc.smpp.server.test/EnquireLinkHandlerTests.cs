using Microsoft.Extensions.Logging;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Handlers;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class EnquireLinkHandlerTests
{
    private readonly Mock<ILogger<EnquireLinkHandler>> _mockLogger;
    private readonly Mock<ISmppSession> _mockSession;

    public EnquireLinkHandlerTests()
    {
        _mockLogger = new Mock<ILogger<EnquireLinkHandler>>();
        _mockSession = new Mock<ISmppSession>();
        _mockSession.Setup(x => x.SystemId).Returns("test-system");
    }

    [Fact]
    public async Task CanHandle_WithEnquireLinkCommand_ReturnsTrue()
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
    public async Task Handle_ReturnsEnquireLinkResponse()
    {
        // Arrange
        var handler = CreateHandler();
        var sequenceNumber = 12345u;
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.EnquireLink,
            SequenceNumber = sequenceNumber
        };

        // Act
        var response = await handler.Handle(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(SmppConstants.SmppCommandId.EnquireLinkResp, response.CommandId);
        Assert.Equal(sequenceNumber, response.SequenceNumber);
        Assert.Equal(SmppConstants.SmppCommandStatus.ESME_ROK, response.CommandStatus);
    }

    [Fact]
    public async Task Handle_WithDifferentSequenceNumbers_ReturnsMatchingSequence()
    {
        // Arrange
        var handler = CreateHandler();
        var sequenceNumber1 = 100u;
        var sequenceNumber2 = 200u;

        var pdu1 = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.EnquireLink,
            SequenceNumber = sequenceNumber1
        };

        var pdu2 = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.EnquireLink,
            SequenceNumber = sequenceNumber2
        };

        // Act
        var response1 = await handler.Handle(pdu1, _mockSession.Object, CancellationToken.None);
        var response2 = await handler.Handle(pdu2, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.Equal(sequenceNumber1, response1!.SequenceNumber);
        Assert.Equal(sequenceNumber2, response2!.SequenceNumber);
    }

    [Fact]
    public async Task Handle_DoesNotModifySession()
    {
        // Arrange
        var handler = CreateHandler();
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.EnquireLink,
            SequenceNumber = 1
        };

        // Act
        await handler.Handle(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        _mockSession.VerifyNoOtherCalls();
    }

    private EnquireLinkHandler CreateHandler()
    {
        return new EnquireLinkHandler(_mockLogger.Object);
    }
}
