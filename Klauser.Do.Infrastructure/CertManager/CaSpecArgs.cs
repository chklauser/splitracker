using Pulumi;
using Pulumi.Kubernetes.ApiExtensions;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Klauser.Do.Infrastructure.CertManager;

public class CaSpecArgs : ResourceArgs
{
    [Input("secretName", required: true)]
    public required Input<string> SecretName { get; set; }
    
    /// <summary>
    /// The CRL distribution points is an X.509 v3 certificate extension which identifies the location of the CRL
    /// from which the revocation of this certificate can be checked. If not set certificate will be issued without CDP.
    /// Values are strings.
    /// </summary>
    [Input("crlDistributionPoints")]
    public InputList<string>? CrlDistributionPoints { get; set; }
    
    /// <summary>
    /// The OCSP server list is an X.509 v3 extension that defines a list of URLs of OCSP responders.
    /// The OCSP responders can be queried for the revocation status of an issued certificate.
    /// If not set, the certificate will be issued with no OCSP servers set.
    /// For example, an OCSP server URL could be “http://ocsp.int-x3.letsencrypt.org”. 
    /// </summary>
    [Input("ocspServers")]
    public InputList<string>? OcspServers { get; set; }
}