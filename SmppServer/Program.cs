using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Secrets;
using Smpp.Server.Extensions;
using Smpp.Server.Models;
using Smpp.Server.Configurations;
using Smpp.Server.Interfaces;
using Smpp.Server.Services;

SmppPdu pdu = new();

pdu.ParseBody();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSmppServerWithSsl(builder.Configuration);

builder.Services.AddTransient<SecretClient, SecretClient>((provider => { return null; }));
/*
builder.Services.AddSingleton<SecretClient>(provider =>
{
    var keyVaultUrl = "https://keyvault-smpp-test.vault.azure.net/";
    
    // Use managed identity, service principal, or other credential
    var credential = new DefaultAzureCredential();
    // Or specific credential like:
    // var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
    
    return new SecretClient(new Uri(keyVaultUrl), credential);

});
*/


builder.Services.AddTransient<IKeyVaultService, AzureKeyVaultService>();
builder.Services.AddSingleton<ConfigurationCache>();

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

app.UseAuthorization();

app.MapControllers();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("SMPP Server application starting up...");

// Log configuration
var config = app.Services.GetRequiredService<IConfiguration>();
var smppConfig = config.GetSection(nameof(SmppServerConfiguration)).Get<SmppServerConfiguration>();
if (smppConfig != null)
{
    logger.LogInformation("⚙️ SMPP Configuration - Port: {Port}, Max Connections: {MaxConnections}", 
        smppConfig.Port, smppConfig.MaxConcurrentConnections);
}



app.Run();