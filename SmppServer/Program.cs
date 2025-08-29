using Smpp.Server.Extensions;
using Smpp.Server.Models;
using Smpp.Server.Configurations;

SmppPdu pdu = new();

pdu.ParseBody();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSmppServerWithSsl(builder.Configuration);

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