using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;

namespace sg.gov.cpf.esvc.smpp.server.Models;

[ExcludeFromCodeCoverage]
public class CertificateExpiringEventArgs : EventArgs
{
    public required X509Certificate2 Certificate { get; init; }
    public required int DaysUntilExpiry { get; init; }
    public required DateTime ExpiryDate { get; init; }

}