using Pulumi;
using Pulumi.Kubernetes.ApiExtensions;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;

namespace Klauser.Do.Infrastructure.CertManager;

using K8sCustomResource = Pulumi.Kubernetes.ApiExtensions.CustomResource;

class Certificate : K8sCustomResource
{
    public Certificate(string name, CertificateArgs args, CustomResourceOptions? options = null) : base(name, args,
        options)
    {
    }
}

class CertificateArgs : CustomResourceArgs
{
    public CertificateArgs() : base("cert-manager.io/v1", "Certificate")
    {
    }

    [Input("spec")]
    public required Input<CertificateSpecArgs> Spec { get; set; }
}

class CertificateSpecArgs : ResourceArgs
{
    [Input("commonName", required: true)]
    public required Input<string> CommonName { get; set; }

    [Input("dnsNames")]
    public InputList<string>? DnsNames { get; set; }

    [Input("ipAddresses")]
    public InputList<string>? IpAddresses { get; set; }

    [Input("uris")]
    public InputList<string>? Uris { get; set; }
    
    [Input("duration")]
    public Input<string>? Duration { get; set; }

    [Input("emailAddresses")]
    public InputList<string>? EmailAddresses { get; set; }

    [Input("secretName", required: true)]
    public required Input<string> SecretName { get; set; }

    [Input("keystores")]
    public Input<CertificateKeystoresArgs>? Keystores { get; set; }

    /// <summary>
    /// IssuerRef is a reference to the issuer for this certificate. If the kind field is not set, or set to Issuer,
    /// an Issuer resource with the given name in the same namespace as the Certificate will be used. If the kind
    /// field is set to ClusterIssuer, a ClusterIssuer with the provided name will be used. The name field in this stanza is required at all times. 
    /// </summary>
    [Input("issuerRef", required: true)]
    public required Input<CertificateObjectReference> IssuerRef { get; set; }

    /// <summary>
    /// IsCA will mark this Certificate as valid for certificate signing. This will automatically add the cert sign usage to the list of usages. 
    /// </summary>
    [Input("isCA")]
    public Input<bool>? IsCa { get; set; }
    
    /// <summary>
    /// Defaults to <c>digital signature</c> and <c>key encipherment</c>.
    /// <list type="bullet">
    /// <item><c>any</c></item>
    /// <item><c>crl sign</c></item>
    /// <item><c>cert sign</c></item>
    /// <item><c>client auth</c></item>
    /// <item><c>code signing</c></item>
    /// <item><c>content commitment</c></item>
    /// <item><c>data encipherment</c></item>
    /// <item><c>decipher only</c></item>
    /// <item><c>digital signature</c></item>
    /// <item><c>email protection</c></item>
    /// <item><c>encipher only</c></item>
    /// <item><c>ipsec end system</c></item>
    /// <item><c>ipsec tunnel</c></item>
    /// <item><c>ipsec user</c></item>
    /// <item><c>key agreement</c></item>
    /// <item><c>key encipherment</c></item>
    /// <item><c>microsoft sgc</c></item>
    /// <item><c>netscape sgc</c></item>
    /// <item><c>ocsp signing</c></item>
    /// <item><c>s/mime</c></item>
    /// <item><c>server auth</c></item>
    /// <item><c>signing</c></item>
    /// <item><c>timestamping</c></item>
    /// </list>
    /// </summary>
    [Input("usages")]
    public InputList<string>? Usages { get; set; }
    
    [Input("encodeUsagesInRequest")]
    public Input<bool>? EncodeUsagesInRequest { get; set; }
}

class CertificateObjectReference : ResourceArgs
{
    [Input("name", required: true)]
    public required Input<string> Name { get; set; }
    
    [Input("kind")]
    public Input<string>? Kind { get; set; }
    
    [Input("group")]
    public Input<string>? Group { get; set; }
}

class CertificateKeystoresArgs : ResourceArgs
{
    /// <summary>
    /// A file named keystore.p12 will be created in the target Secret resource,
    /// encrypted using the password stored in passwordSecretRef.
    /// The keystore file will only be updated upon re-issuance.
    /// A file named truststore.p12 will also be created in the target Secret resource,
    /// encrypted using the password stored in passwordSecretRef containing the issuing Certificate Authority 
    /// </summary>
    [Input("pkcs12", required: true)]
    public required Input<Pkcs12KeystoreArgs> Pkcs12 { get; set; }
}

class Pkcs12KeystoreArgs : ResourceArgs
{
    [Input("create", required: true)]
    public required Input<bool> Create { get; set; }

    [Input("passwordSecretRef", required: true)]
    public required Input<SecretKeySelectorArgs> PasswordSecretRef { get; set; }
}