using System.Windows.Input;
using Smpp.Server.Helpers;
using Smpp.Server.Interfaces;
using Smpp.Server.Models.DTOs;

namespace Smpp.Server.Services;

public class ProcessCompleteMessageCommand(
    string sourceAddress,
    string destinationAddress,
    string message,
    ISmppSession session,
    IDeliveryReceiptSender deliveryReceiptSender,
    IExternalMessageService externalService,
    ILogger logger)
    : IMessageCommand<MessageProcessingResult>
{
    public async Task<MessageProcessingResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Processing complete message from {Source} to {Destination}: {Message}", 
                sourceAddress, destinationAddress, message);

            // Send to external service
            var result = await externalService.SendMessageAsync(
                session.SystemId!,
                destinationAddress,
                message,
                cancellationToken);

            // Send delivery receipt based on result
            var deliveryStatus = result.IsSuccess
                ? DeliveryStatusHelper.Delivered()
                : DeliveryStatusHelper.Failed(result.ErrorCode);

            var messageId = "msg_" + Guid.NewGuid().ToString("N")[..8];
            await deliveryReceiptSender.SendAsync(session, messageId, deliveryStatus);

            if (result.IsSuccess)
            {
                logger.LogInformation("Message processed successfully from {Source} to {Destination}", 
                    sourceAddress, destinationAddress);
            }
            else
            {
                logger.LogWarning("⚠Message processing failed: {ErrorMessage}", result.ErrorMessage);
            }

            return new MessageProcessingResult(result.IsSuccess, result.ErrorMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing complete message from {Source} to {Destination}", 
                sourceAddress, destinationAddress);

            // Send error delivery receipt
            try
            {
                var errorMessageId = "msg_error_" + Guid.NewGuid().ToString("N")[..6];
                await deliveryReceiptSender.SendAsync(session, errorMessageId, DeliveryStatusHelper.Undeliverable());
            }
            catch (Exception receiptEx)
            {
                logger.LogError(receiptEx, "Failed to send error delivery receipt");
            }

            return new MessageProcessingResult(false, ex.Message);
        }
    }

}