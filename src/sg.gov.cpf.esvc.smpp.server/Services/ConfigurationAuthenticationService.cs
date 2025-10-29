using Microsoft.Extensions.Options;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Interfaces;

namespace sg.gov.cpf.esvc.smpp.server.Services;

public class ConfigurationAuthenticationService(
    IOptions<SmppServerConfiguration> config,
    ILogger<ConfigurationAuthenticationService> logger,
    IKeyVaultService keyVaultService,
    EnvironmentVariablesConfiguration environmentVariables)
    : IAuthenticationService
{
    private readonly SmppServerConfiguration _config = config.Value;

    public Task<bool> AuthenticateAsync(string systemId, string password)
    {
        var isValid = systemId == environmentVariables.SessionUserName && password == keyVaultService.SessionPassword;
        
        logger.LogInformation("Authentication attempt for {SystemId}: {Result}", systemId, isValid ? "Success" : "Failed");
        
        return Task.FromResult(isValid);
    }

}