// A test that trusts a certificate on the machine so it can point the plugin at
// a local endpoint. This file is a fixture. It is outside every project in the
// solution, nothing compiles it, and it exists so the rule can be watched
// refusing the lines it is about.
public class BreaksTheRule
{
    public void TrustTheLocalEndpoint()
    {
        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        store.Add(TheLocalCertificate());
    }

    public void OrJustStopChecking(HttpClientHandler handler)
    {
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
    }
}
