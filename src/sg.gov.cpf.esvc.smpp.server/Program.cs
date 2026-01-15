using System.Diagnostics.CodeAnalysis;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using sg.gov.cpf.esvc.smpp.server.Extensions;
using sg.gov.cpf.esvc.smpp.server.Configurations;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

var envConfig = builder.Services.ParseEnvironmentVariables();

var aiOptions = new Microsoft.ApplicationInsights.AspNetCore.Extensions.ApplicationInsightsServiceOptions();
aiOptions.EnableAdaptiveSampling = false;
aiOptions.ConnectionString = envConfig.AppInsightConnectionString;

builder.Logging.ConfigureLogging(envConfig);

builder.Services.AddApplicationInsightsTelemetry(aiOptions);
builder.Services.AddSingleton<ITelemetryInitializer>(new CloudRoleTelemetryInitializer(builder.Configuration["AppId"]!));
builder.Services.AddSingleton<ITelemetryChannel>(new ServerTelemetryChannel()
{
    StorageFolder = builder.Configuration["ApplicationInsights:StorageFolder"]
});

// Add services to the container.
builder.Services.AddSmppServerWithSsl(builder.Configuration);
builder.Services.AddAzureKeyVaultFramework(builder.Configuration);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

[ExcludeFromCodeCoverage(Justification = "This is the main program entry point.")]
public abstract partial class Program
{

}


