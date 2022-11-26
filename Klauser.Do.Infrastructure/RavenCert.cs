using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Klauser.Do.Infrastructure.CertManager;
using Microsoft.IdentityModel.Logging;
using Pulumi;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Pulumi.Random;
using CertSecretKeySelectorArgs = Klauser.Do.Infrastructure.CertManager.SecretKeySelectorArgs;


namespace Klauser.Do.Infrastructure;

public class RavenCertificate : ComponentResource
{
    public RavenCertificate(string name, RavenCertificateArgs args, Output<string> kubeConfig, ComponentResourceOptions? opts = null) 
        : base("klauser:do.infrastructure:RavenCertificate", name, args, opts)
    {
        var certPassword = new RandomPassword($"{name}-cert-password", new() {
            Length = 26,
            Lower = true,
            Upper = true,
            Number = true,
            Special = false,
        }, new() { Parent = this });
        
        var passwordKey = "password";
        
        var passwordSecret = new Secret($"{name}-cert-password", new() {
            Metadata = new ObjectMetaArgs {
                Namespace = args.NamespaceName,
            },
            StringData = new() {
                [passwordKey] = certPassword.Result,
            },
        }, new() { Parent = this });

        var certSecretNameRaw = $"{name}-cert";
        var cert = new Certificate($"{name}", new() {
            Metadata = new ObjectMetaArgs {
                Namespace = args.NamespaceName,
            },
            Spec = new CertificateSpecArgs() {
                IssuerRef = new CertificateObjectReference() {
                    Kind = "ClusterIssuer",
                    Name = args.IssuerName,
                },
                Duration = "87600h",
                CommonName = $"{name}@do.klauser.link",
                EmailAddresses = new(){$"{name}@do.klauser.link"},
                Keystores = new CertificateKeystoresArgs() {
                    Pkcs12 = new Pkcs12KeystoreArgs() {
                        Create = true,
                        PasswordSecretRef = new CertSecretKeySelectorArgs() {
                            Key = passwordKey,
                            Name = passwordSecret.Metadata.Apply(m => m.Name),
                        },
                    },
                },
                SecretName = certSecretNameRaw,
            },
        }, new() { Parent = this });
        var certSecretName = Output.Tuple(cert.Metadata, args.NamespaceName.ToOutput(), kubeConfig).Apply(async (x) =>
        {
            var (_, ns, myKubeConfig) = x;

            await using var buf = new MemoryStream();
            await using (var writer = new StreamWriter(buf, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(myKubeConfig);
                writer.Flush();
            }

            buf.Seek(0, SeekOrigin.Begin);
            
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(buf);
            var client = new Kubernetes(config);
            var started = DateTime.UtcNow;
            while(DateTime.UtcNow - started < TimeSpan.FromSeconds(60*3))
            {
                var secret = await client.ReadNamespacedSecretAsync(certSecretNameRaw, ns, cancellationToken: default);
                if (secret != null && secret.Data.ContainsKey("keystore.p12"))
                {
                    Log.Info($"Certificate ({name}) output secret is ready: {secret.Metadata.Namespace()}/{secret.Metadata.Name}");
                    return secret.Metadata.Name;
                }
                Log.Info($"Waiting for certificate to be issued (secret: {certSecretNameRaw})...");
                await Task.Delay(TimeSpan.FromSeconds(15));
            }

            var error = $"Certificate was not issued in time (secret: {certSecretNameRaw})";
            Log.Error(error);
            throw new(error);
        });
        var certSecret = Secret.Get($"{name}-cert-secret",
            Output.Tuple(args.NamespaceName.ToOutput(), certSecretName).Apply(x => $"{x.Item1}/{x.Item2}"),
            new() { Parent = this, DependsOn = cert });
        
        CertificateThumbprint = Output.Unsecret(Output.Tuple(certSecret.Data, certPassword.Result).Apply(
            t =>
            {
                var (d,p) = t;
                SecureString password = new();
                foreach (var c in p) {
                    password.AppendChar(c);
                }

                return new X509Certificate2(Convert.FromBase64String(d["keystore.p12"]), password).Thumbprint;
            }));
        CertificatePassword = certPassword.Result;
        CertificateSecretName = certSecretName;
        
        RegisterOutputs();
    }
    
    public Output<string> CertificatePassword { get; }
    public Output<string> CertificateThumbprint { get; }
    public Output<string> CertificateSecretName { get; }
}

public class RavenCertificateArgs : ResourceArgs
{
    [Input("namespaceName", required: true)]
    public required Input<string> NamespaceName { get; set; }

    [Input("issuerName", required: true)]
    public required Input<string> IssuerName { get; set; }
}