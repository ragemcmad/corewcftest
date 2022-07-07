using System.Management.Automation;
using System.Security.Cryptography.X509Certificates;

public class CertificateService
{
    public const string DefaultCertificateSubject = "local-webservices";

    private readonly string subject;
    private string SubjectAttribute => "CN=" + subject;

    public CertificateService(IConfiguration configuration) // string subject = DefaultCertificateSubject)
    {
        subject = configuration
            .GetRequiredSection("Kestrel")
            .GetValue<string>("CertificateSubject") ?? DefaultCertificateSubject;
    }

    public X509Certificate2 Get() 
        => FromStore() 
        ?? CreateWithPowershell() 
        ?? throw new KeyNotFoundException("Certificate not found in store");

    public X509Certificate2? CreateWithPowershell()
    {
        var collection = PowerShell.Create()
            .AddCommand("New-SelfSignedCertificate")
                .AddParameter("Subject", subject ?? DefaultCertificateSubject)
                .AddParameter("CertStoreLocation", "Cert:\\LocalMachine\\My")
                .AddParameter("KeyAlgorithm", "ECDSA_p256")
            .Invoke();
        return FromStore();
    }

    public X509Certificate2? FromStore()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);
       
        return store.Certificates
            .FirstOrDefault(cert => cert.Subject == SubjectAttribute);
    }
}