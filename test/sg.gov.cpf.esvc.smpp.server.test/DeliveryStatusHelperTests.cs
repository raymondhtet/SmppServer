using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Helpers;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class DeliveryStatusHelperTests
{
    [Fact]
    public void Delivered_ReturnsCorrectStatus()
    {
        // Act
        var status = DeliveryStatusHelper.Delivered();

        // Assert
        Assert.Equal(SmppConstants.MessageState.DELIVERED, status.MessageState);
        Assert.Equal("DELIV", status.ErrorStatus);
        Assert.Equal("000", status.ErrorCode);
    }

    [Fact]
    public void Failed_WithoutErrorCode_ReturnsDefaultErrorCode()
    {
        // Act
        var status = DeliveryStatusHelper.Failed();

        // Assert
        Assert.Equal(SmppConstants.MessageState.UNDELIVERABLE, status.MessageState);
        Assert.Equal("UNDELIV", status.ErrorStatus);
        Assert.Equal("001", status.ErrorCode);
    }

    [Fact]
    public void Failed_WithCustomErrorCode_ReturnsCustomCode()
    {
        // Arrange
        var errorCode = "999";

        // Act
        var status = DeliveryStatusHelper.Failed(errorCode);

        // Assert
        Assert.Equal(SmppConstants.MessageState.UNDELIVERABLE, status.MessageState);
        Assert.Equal("UNDELIV", status.ErrorStatus);
        Assert.Equal(errorCode, status.ErrorCode);
    }

    [Fact]
    public void Undeliverable_ReturnsCorrectStatus()
    {
        // Act
        var status = DeliveryStatusHelper.Undeliverable();

        // Assert
        Assert.Equal(SmppConstants.MessageState.UNDELIVERABLE, status.MessageState);
        Assert.Equal("UNDELIV", status.ErrorStatus);
        Assert.Equal("005", status.ErrorCode);
    }

    [Fact]
    public void Rejected_ReturnsCorrectStatus()
    {
        // Act
        var status = DeliveryStatusHelper.Rejected();

        // Assert
        Assert.Equal(SmppConstants.MessageState.REJECTED, status.MessageState);
        Assert.Equal("REJECT", status.ErrorStatus);
        Assert.Equal("004", status.ErrorCode);
    }

    [Fact]
    public void AllStatuses_HaveUniqueErrorCodes()
    {
        // Arrange
        var delivered = DeliveryStatusHelper.Delivered();
        var failed = DeliveryStatusHelper.Failed();
        var undeliverable = DeliveryStatusHelper.Undeliverable();
        var rejected = DeliveryStatusHelper.Rejected();

        // Act
        var errorCodes = new[]
        {
            delivered.ErrorCode,
            failed.ErrorCode,
            undeliverable.ErrorCode,
            rejected.ErrorCode
        };

        // Assert
        Assert.Equal(4, errorCodes.Distinct().Count());
    }

    [Fact]
    public void AllStatuses_HaveValidMessageStates()
    {
        // Arrange & Act
        var statuses = new[]
        {
            DeliveryStatusHelper.Delivered(),
            DeliveryStatusHelper.Failed(),
            DeliveryStatusHelper.Undeliverable(),
            DeliveryStatusHelper.Rejected()
        };

        // Assert
        foreach (var status in statuses)
        {
            Assert.NotNull(status.MessageState);
            Assert.NotNull(status.ErrorStatus);
            Assert.NotNull(status.ErrorCode);
        }
    }
}
