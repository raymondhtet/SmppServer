// See https://aka.ms/new-console-template for more information
using sg.gov.cpf.esvc.smpp.client;


try
{
    Console.WriteLine("Running SMPP client...");
    // Create client instance
    using var client = new SmppClient(
        host: "localhost",
        port: 2775,
        systemId: "smpp",
        password: "password"
    );

    Console.WriteLine("Connecting to SMPP server...");

    // Connect and bind to the server
    await client.ConnectAsync();

    Console.WriteLine("Sending message to SMPP server...");

    // Send a message
    await client.SendMessageAsync(
        sourceAddress: "1234",
        destinationAddress: "61412345678",
        message: "Hello from SMPP client!"
    );

    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}