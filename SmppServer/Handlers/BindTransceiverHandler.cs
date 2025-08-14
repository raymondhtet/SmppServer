using Smpp.Server.Constants;
using Smpp.Server.Factories;
using Smpp.Server.Interfaces;
using Smpp.Server.Models;

namespace Smpp.Server.Handlers;

public class BindTransceiverHandler(ILogger<BindTransceiverHandler> logger, IAuthenticationService authService)
    : IPduHandler
{
    public Task<bool> CanHandle(SmppPdu pdu) 
        => Task.FromResult(pdu.CommandId == SmppConstants.SmppCommandId.BindTransceiver);

    public async Task<SmppPdu?> Handle(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken)
    {
        try
        {
            session.Pause();

            var bindRequest = SmppPduFactory.CreateBindTransceiver(pdu);
            
            logger.LogInformation("{SystemID} attempting to establish a connection", bindRequest.SystemId);

            var isAuthenticated = await authService.AuthenticateAsync(bindRequest.SystemId, bindRequest.Password);

            if (isAuthenticated)
            {
                session.SystemId = bindRequest.SystemId;
                session.IsAuthenticated = true;
                session.Resume();
                
                logger.LogInformation("{SystemId} authenticated successfully", bindRequest.SystemId);
                
                return SmppResponseBuilder.Create()
                    .AsBindTransceiverResponse(pdu.SequenceNumber, true)
                    .Build();
            }
            else
            {
                logger.LogWarning("Authentication failed for {SystemId}", bindRequest.SystemId);
                
                var response = SmppResponseBuilder.Create()
                    .AsBindTransceiverResponse(pdu.SequenceNumber, false)
                    .Build();

                // Schedule session close after sending response
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100, cancellationToken);
                    session.Close();
                }, cancellationToken);

                return response;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during bind_transceiver");
            
            return SmppResponseBuilder.Create()
                .AsBindTransceiverResponse(pdu.SequenceNumber, false)
                .Build();
        }
    }

}