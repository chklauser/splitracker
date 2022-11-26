using Pulumi;
using Pulumi.Kubernetes.ApiExtensions;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Klauser.Do.Infrastructure.CertManager;

using K8sCustomResource = Pulumi.Kubernetes.ApiExtensions.CustomResource;

public class ClusterIssuer : K8sCustomResource  {
    public ClusterIssuer(string name, ClusterIssuerArgs args, CustomResourceOptions? options = null) : base(name, args, options)
    {
    }
}

public class ClusterIssuerArgs : CustomResourceArgs {
    public ClusterIssuerArgs() : base("cert-manager.io/v1", "ClusterIssuer")
    { 
    }

    [Input("spec", true)]
    public required Input<IssuerSpecArgs> Spec { get; set; }
}

public class IssuerSpecArgs : ResourceArgs
{
    [Input("acme")]
    public Input<AcmeSpecArgs>? Acme { get; set; }
    
    [Input("selfSigned")]
    public Input<SelfSignedSpecArgs>? SelfSigned { get; set; }
    
    [Input("ca")]
    public Input<CaSpecArgs>? Ca { get; set; }
    
    // For more, see https://cert-manager.io/docs/reference/api-docs/#cert-manager.io/v1.IssuerConfig
}


public class SecretKeySelectorArgs : ResourceArgs
{
    [Input("name")]
    public required Input<string> Name { get; set; }

    [Input("key")]
    public Input<string>? Key { get; set; }
}