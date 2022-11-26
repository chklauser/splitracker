using Pulumi;
using Pulumi.Kubernetes.ApiExtensions;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Klauser.Do.Infrastructure.CertManager;

public class SelfSignedSpecArgs : ResourceArgs
{
    /// <summary>
    /// The CRL distribution points is an X.509 v3 certificate extension which identifies the location of the CRL
    /// from which the revocation of this certificate can be checked. If not set certificate will be issued without CDP.
    /// Values are strings.
    /// </summary>
    [Input("crlDistributionPoints")]
    public InputList<string>? CrlDistributionPoints { get; set; }
}
