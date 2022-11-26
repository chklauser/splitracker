namespace Klauser.Do.Infrastructure.CertManager;

using Pulumi;
// ReSharper disable UnusedAutoPropertyAccessor.Global


class AcmeSpecArgs : ResourceArgs
{
    [Input("email")]
    public Input<string>? Email { get; set; }
    
    [Input("server")]
    public required Input<string> Server { get; set; }
    
    [Input("preferredChain")]
    public Input<string>? PreferredChain { get; set; }
    
    [Input("skipTLSVerify")]
    public Input<bool>? SkipTlsVerify { get; set; }
    
    [Input("privateKeySecretRef", required: true)]
    public required Input<SecretKeySelectorArgs> PrivateKeySecretRef { get; set; }
    
    [Input("solvers")]
    public required InputList<AcmeChallengeSolverArgs> Solvers { get; set; }
}

class AcmeChallengeSolverArgs : ResourceArgs
{
    [Input("selector")]
    public Input<CertificateDnsNameSelectorArgs>? Selector { get; set; }
    
    [Input("dns01")]
    public Input<AcmeChallengeSolverDns01Args>? Dns01 { get; set; }
    
    [Input("http01")]
    public Input<AcmeChallengeSolverHttp01Args>? Http01 { get; set; }
}

class CertificateDnsNameSelectorArgs : ResourceArgs
{
    [Input("matchLabels")]
    public InputMap<string>? MatchLabels { get; set; }
    
    [Input("dnsZones")]
    public InputList<string>? DnsZones { get; set; }
    
    [Input("dnsNames")]
    public InputList<string>? DnsNames { get; set; }
}

class AcmeChallengeSolverDns01Args : ResourceArgs
{
    [Input("digitalocean")]
    public Input<AcmeIssuerDns01ProviderDigitalOceanArgs>? DigitalOcean { get; set; }
}

class AcmeIssuerDns01ProviderDigitalOceanArgs : ResourceArgs
{
    [Input("tokenSecretRef", required: true)]
    public required Input<SecretKeySelectorArgs> TokenSecretRef { get; set; }
}

class AcmeChallengeSolverHttp01Args : ResourceArgs
{
    [Input("ingress", required: true)]
    public required Input<AcmeChallengeSolverHttp01IngressArgs> Ingress { get; set; }
}
class AcmeChallengeSolverHttp01IngressArgs : ResourceArgs
{
    [Input("serviceType")]
    public Input<string>? ServiceType { get; set; }
    
    [Input("class")]
    public Input<string>? Class { get; set; }
    
    /// <summary>
    /// The name of the ingress resource that should have ACME challenge solving routes inserted
    /// into it in order to solve HTTP01 challenges. This is typically used in conjunction with
    /// ingress controllers like ingress-gce, which maintains a 1:1 mapping between external IPs and ingress resources.
    /// </summary>
    [Input("name")]
    public Input<string>? Name { get; set; }
}