using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Models;
using sg.gov.cpf.esvc.smpp.server.Models.DTOs;
using sg.gov.cpf.esvc.smpp.server.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class MessageConcatenationServiceTests
{
    private readonly MessageTracker _messageTracker;
    private readonly Mock<ILogger<MessageConcatenationService>> _mockLogger;

    public MessageConcatenationServiceTests()
    {
        var mockTrackerLogger = new Mock<ILogger<MessageTracker>>();
        // Create a real MessageTracker instance instead of mocking it
        _messageTracker = new MessageTracker(mockTrackerLogger.Object);
        _mockLogger = new Mock<ILogger<MessageConcatenationService>>();
    }

    [Fact]
    public async Task ProcessMessagePartAsync_WithSingleMessage_ReturnsCompleteMessage()
    {
        // Arrange
        var service = CreateService();
        var message = "Hello World";
        var request = new SubmitSmRequest
        {
            SourceAddress = "1234567890",
            DestinationAddress = "0987654321",
            ShortMessage = Encoding.UTF8.GetBytes(message)
        };

        // Act
        var result = await service.ProcessMessagePartAsync(null, false, message, request);

        // Assert
        Assert.True(result.IsComplete);
        Assert.Equal(message, result.CompleteMessage);
    }

    [Fact]
    public async Task ProcessMessagePartAsync_WithMultipartMessage_TracksMessageParts()
    {
        // Arrange
        var service = CreateService();
        var message = "Part 1";
        var request = new SubmitSmRequest
        {
            SourceAddress = "1234567890",
            DestinationAddress = "0987654321",
            ShortMessage = Encoding.UTF8.GetBytes(message)
        };

        var concatInfo = new SmppConstants.ConcatenationInfo
        {
            ReferenceNumber = 123,
            TotalParts = 2,
            PartNumber = 1
        };

        // Act
        var result = await service.ProcessMessagePartAsync(concatInfo, true, message, request);

        // Assert
        Assert.False(result.IsComplete);
        Assert.Null(result.CompleteMessage);
    }

    [Fact]
    public async Task ProcessMessagePartAsync_WithLastPart_ReturnsCompleteMessage()
    {
        // Arrange
        var service = CreateService();
        var sourceAddress = "1234567890";
        var destinationAddress = "0987654321";
        
        var concatInfo = new SmppConstants.ConcatenationInfo
        {
            ReferenceNumber = 456,
            TotalParts = 2,
            PartNumber = 1
        };

        var request1 = new SubmitSmRequest
        {
            SourceAddress = sourceAddress,
            DestinationAddress = destinationAddress,
            ShortMessage = Encoding.UTF8.GetBytes("Part 1")
        };

        // First part
        await service.ProcessMessagePartAsync(concatInfo, true, "Part 1", request1);

        // Second part
        concatInfo.PartNumber = 2;
        var request2 = new SubmitSmRequest
        {
            SourceAddress = sourceAddress,
            DestinationAddress = destinationAddress,
            ShortMessage = Encoding.UTF8.GetBytes("Part 2")
        };

        // Act
        var result = await service.ProcessMessagePartAsync(concatInfo, true, "Part 2", request2);

        // Assert
        Assert.True(result.IsComplete);
        Assert.NotNull(result.CompleteMessage);
        Assert.Contains("Part 1", result.CompleteMessage);
        Assert.Contains("Part 2", result.CompleteMessage);
    }

    [Fact]
    public async Task ProcessMessagePartAsync_WithNullConcatInfo_HandlesGracefully()
    {
        // Arrange
        var service = CreateService();
        var message = "Hello";
        var request = new SubmitSmRequest
        {
            SourceAddress = "1234567890",
            DestinationAddress = "0987654321",
            ShortMessage = Encoding.UTF8.GetBytes(message)
        };

        // Act
        var result = await service.ProcessMessagePartAsync(null, false, message, request);

        // Assert
        Assert.True(result.IsComplete);
        Assert.Equal(message, result.CompleteMessage);
    }

    private MessageConcatenationService CreateService()
    {
        return new MessageConcatenationService(
            _messageTracker,
            _mockLogger.Object
        );
    }
}
