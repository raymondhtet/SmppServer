namespace Smpp.Server.Interfaces;

public interface IAuthenticationService
{
    Task<bool> AuthenticateAsync(string systemId, string password);
}