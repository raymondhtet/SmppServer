using Microsoft.Extensions.Options;
using Smpp.Server.Configurations;
using Smpp.Server.Interfaces;

namespace Smpp.Server.Services;

public class ConfigurationAuthenticationService(
    IOptions<SmppServerConfiguration> config,
    ILogger<ConfigurationAuthenticationService> logger)
    : IAuthenticationService
{
    private readonly SmppServerConfiguration _config = config.Value;

    public Task<bool> AuthenticateAsync(string systemId, string password)
    {
        var isValid = systemId == _config.SessionUsername && password == _config.SessionPassword;
        
        logger.LogInformation("Authentication attempt for {SystemId}: {Result}", systemId, isValid ? "Success" : "Failed");
        
        return Task.FromResult(isValid);
    }

}