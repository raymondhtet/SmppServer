using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using sg.gov.cpf.esvc.smpp.server.Constants;

namespace Ssg.gov.cpf.esvc.smpp.server.Middlewares;

public class CloudRoleTelemetryInitializer : ITelemetryInitializer
{
    private readonly string _roleName;
    private readonly string _roleInstance;

    public CloudRoleTelemetryInitializer(string roleName, string? roleInstance = null)
    {
        _roleName = string.IsNullOrWhiteSpace(roleName)
            ? throw new ArgumentException(roleName)
            : roleName;

        _roleInstance = string.IsNullOrWhiteSpace(roleInstance)
            ? Environment.MachineName
            : roleInstance;
    }

    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.Cloud.RoleName = _roleName; //AppId
        telemetry.Context.Cloud.RoleInstance = _roleInstance; //Machine Name
        var activity = System.Diagnostics.Activity.Current;

        if (activity != null)
        {
            Dictionary<string, string> activityDictionary =
                activity.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (activityDictionary.ContainsKey(SmppConstants.TelemetryConstants.OperationID))
            {
                telemetry.Context.Operation.Id = activityDictionary[SmppConstants.TelemetryConstants.OperationID];
            }

            if (activityDictionary.ContainsKey(SmppConstants.TelemetryConstants.UserID))
            {
                telemetry.Context.User.Id = activityDictionary[SmppConstants.TelemetryConstants.UserID];
            }

            if (activityDictionary.ContainsKey(SmppConstants.TelemetryConstants.ActionID))
            {
                telemetry.Context.Properties.Add(SmppConstants.TelemetryConstants.ActionID,
                    activityDictionary[SmppConstants.TelemetryConstants.ActionID]);
            }
        }
    }
}