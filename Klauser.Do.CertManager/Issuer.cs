using Pulumi;
using Pulumi.Kubernetes.ApiExtensions;

namespace Klauser.Do.Infrastructure.CertManager;

using K8sCustomResource = Pulumi.Kubernetes.ApiExtensions.CustomResource;

public class Issuer : K8sCustomResource  {
    public Issuer(string name, IssuerArgs args, CustomResourceOptions? options = null) : base(name, args, options)
    {
    }
}

public class IssuerArgs : CustomResourceArgs {
    public IssuerArgs() : base("cert-manager.io/v1", "Issuer")
    { 
    }

    [Input("spec", true)]
    public required Input<IssuerSpecArgs> Spec { get; set; }
}
