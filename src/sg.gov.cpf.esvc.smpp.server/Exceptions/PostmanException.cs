namespace sg.gov.cpf.esvc.smpp.server.Exceptions
{
    public class PostmanException : Exception
    {
        public PostmanException(string? errorMessage, string? errorCode, string? errorStatus, string campaignId, string maskedRecipientMobileNumber, string? ID, string? messageId) 
            : base($"Failed to send message via Postman API for Message ID: {messageId} (Postman ID:{ID}) to mobile number:{maskedRecipientMobileNumber} and Campaign ID:{campaignId}, Error Code:{errorCode}, Error Status:{errorStatus}")
        {

        }

    }
}
