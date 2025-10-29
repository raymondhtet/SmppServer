using Microsoft.Extensions.Diagnostics.HealthChecks;
using sg.gov.cpf.esvc.smpp.server.BackgroundServices;

namespace sg.gov.cpf.esvc.smpp.server.Models;

public class SmppHealthCheck : IHealthCheck
{
    private readonly SmppServer _server;

    public SmppHealthCheck(SmppServer server)
    {
        _server = server;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isRunning = _server.IsRunning;
            var activeSessions = _server.ActiveSessionsCount;

            return Task.FromResult(
                isRunning
                    ? HealthCheckResult.Healthy($"SMPP Server is running. Active sessions: {activeSessions}")
                    : HealthCheckResult.Unhealthy("SMPP Server is not running"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("SMPP Server health check failed", ex));
        }
    }
}