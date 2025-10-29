namespace sg.gov.cpf.esvc.smpp.server.Interfaces;

public interface IAuthenticationService
{
    Task<bool> AuthenticateAsync(string systemId, string password);
}