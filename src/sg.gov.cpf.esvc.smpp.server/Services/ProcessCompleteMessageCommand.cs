using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Helpers;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using sg.gov.cpf.esvc.smpp.server.Models.DTOs;

namespace sg.gov.cpf.esvc.smpp.server.Services;

public class ProcessCompleteMessageCommand(
    string sourceAddress,
    string destinationAddress,
    string message,
    string campaignId,
    string messageId,
    ISmppSession session,
    IDeliveryReceiptSender deliveryReceiptSender,
    IExternalMessageService externalService,
    EnvironmentVariablesConfiguration environmentVariablesConfiguration,
    ILogger logger)
    : IMessageCommand<MessageProcessingResult>
{
    public async Task<MessageProcessingResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {

            // Send to external service
            var result = await externalService.SendMessageAsync(
                session.SystemId!,
                destinationAddress,
                message,
                campaignId,
                messageId,
                cancellationToken);

            if (!result.IsSuccess || environmentVariablesConfiguration.IsDeliverSmEnabled)
            {
                // Send delivery receipt only when failure in calling postman api or postman returns with error response
                var deliveryStatus = DeliveryStatusHelper.Failed(result.ErrorCode);

                await deliveryReceiptSender.SendDeliveryReceiptAsync(
                    session,
                    sourceAddress,     // Original sender
                    destinationAddress, // Original recipient  
                    messageId,
                    deliveryStatus);
                logger.LogInformation("Message processing failed: {ErrorMessage}", result.ErrorMessage);

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

                await deliveryReceiptSender.SendDeliveryReceiptAsync(
                            session,
                            sourceAddress,     // Original sender
                            destinationAddress, // Original recipient  
                            errorMessageId,
                            DeliveryStatusHelper.Undeliverable());
            }
            catch (Exception receiptEx)
            {
                logger.LogError(receiptEx, "Failed to send error delivery receipt");
            }

            return new MessageProcessingResult(false, ex.Message);
        }
    }

}