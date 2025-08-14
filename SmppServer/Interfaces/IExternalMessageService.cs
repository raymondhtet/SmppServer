using Smpp.Server.Models.DTOs;

namespace Smpp.Server.Interfaces;

public interface IExternalMessageService
{
    Task<ExternalServiceResult> SendMessageAsync(
        string systemId, 
        string destinationAddress, 
        string message, 
        CancellationToken cancellationToken);

}