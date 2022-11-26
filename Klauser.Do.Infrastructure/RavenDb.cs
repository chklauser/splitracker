using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Klauser.Do.Infrastructure.CertManager;
using Pulumi;
using Pulumi.Kubernetes.Apps.V1;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Networking.V1;
using Pulumi.Kubernetes.Types.Inputs.Apps.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Pulumi.Kubernetes.Types.Inputs.Networking.V1;
using Pulumi.Random;
using CertSecretKeySelectorArgs = Klauser.Do.Infrastructure.CertManager.SecretKeySelectorArgs;
using SecretKeySelectorArgs = Pulumi.Kubernetes.Types.Inputs.Core.V1.SecretKeySelectorArgs;

namespace Klauser.Do.Infrastructure;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class RavenDb : ComponentResource
{
    public RavenDb(string name, RavenDbArgs args, Output<string> kubeConfig, ComponentResourceOptions? options = null)
        : base("klauser:do.infrastructure:RavenDb", name, args, options)
    {
        var ns = new Namespace(name, new() {
            Metadata = new ObjectMetaArgs {
                Name = args.Namespace ?? name,
            }
        }, new() { Parent = this });

        var nsName = ns.Metadata.Apply(m => m.Name);

        var podSelector = new InputMap<string> {
            ["app"] = name,
        };

        var serviceName = "db";
        var service = new Service(name, new() {
            Metadata = new ObjectMetaArgs {
                Name = serviceName,
                Namespace = nsName,
                Annotations = new() {
                    ["pulumi.com/skipAwait"] = "true",
                },
            },
            Spec = new ServiceSpecArgs {
                Type = "ClusterIP",
                Selector = podSelector,
                Ports = new() {
                    new ServicePortArgs {
                        Port = 443,
                        TargetPort = 443,
                        Protocol = "TCP",
                        Name = "https"
                    },
                    new ServicePortArgs {
                        Port = 38888,
                        TargetPort = 38888,
                        Protocol = "TCP",
                        Name = "tcp",
                    },
                },
            },
        }, new() { Parent = this });

        var keystorePassword = new RandomPassword($"{name}-cert-password", new() {
            Length = 26,
            Lower = true,
            Upper = true,
            Number = true,
            Special = false,
        }, new() { Parent = this });

        var keystorePasswordKey = "password";
        var keystorePasswordSecret = new Secret($"{name}-cert-password", new() {
            Metadata = new ObjectMetaArgs {
                Namespace = nsName,
            },
            StringData = new() {
                [keystorePasswordKey] = keystorePassword.Result,
            },
        }, new() { Parent = this });

        var ravenDomain = $"{name}.do.klauser.link";
        var certsSecretName = $"{name}-certs";
        var serverCert = new Certificate($"{name}", new() {
            Metadata = new ObjectMetaArgs {
                Namespace = nsName,
            },
            Spec = new CertificateSpecArgs {
                CommonName = ravenDomain,
                DnsNames = new InputList<string> {
                    ravenDomain,
                },
                SecretName = certsSecretName,
                IssuerRef = new CertificateObjectReference {
                    Kind = "ClusterIssuer",
                    Name = args.Issuer,
                },
                Keystores = new CertificateKeystoresArgs {
                    Pkcs12 = new Pkcs12KeystoreArgs {
                        Create = true,
                        PasswordSecretRef = new CertSecretKeySelectorArgs {
                            Name = keystorePasswordSecret.Metadata.Apply(m => m.Name),
                            Key = keystorePasswordKey,
                        },
                    },
                },
            },
        }, new() { Parent = this });

        var dbCaCertSecretName = $"{name}-ca";
        var dbCaCert = new Certificate($"{name}-ca", new() {
            Metadata = new ObjectMetaArgs {
                Namespace = "infra",
            },
            Spec = new CertificateSpecArgs {
                IsCa = true,
                IssuerRef = new CertificateObjectReference {
                    Kind = "ClusterIssuer",
                    Name = args.SelfSignedIssuer,
                },
                Duration = "131400h",
                CommonName = "ca.raven.do.klauser.link",
                DnsNames = new() { $"ca.{name}.do.klauser.link" }, 
                SecretName = dbCaCertSecretName,
            },
        }, new() { Parent = this });

        var caIssuer = new ClusterIssuer($"{name}-ca", new() {
            Spec = new IssuerSpecArgs {
                Ca = new CaSpecArgs {
                    SecretName = dbCaCertSecretName,
                },
            },
        }, new() { Parent = this, DependsOn = new() { dbCaCert } });

        var adminCert = new RavenCertificate($"{name}-admin", new RavenCertificateArgs {
            IssuerName = caIssuer.Metadata.Apply(m => m.Name),
            NamespaceName = nsName,
        }, kubeConfig, new() { Parent = this });

        var ingress = new Ingress(name, new() {
            Metadata = new ObjectMetaArgs {
                Namespace = nsName,
                Annotations = new() {
                    ["pulumi.com/skipAwait"] = "true",
                    // ["nginx.ingress.kubernetes.io/backend-protocol"] = "HTTPS",
                    // ["nginx.ingress.kubernetes.io/auth-tls-secret"] = dbAdminClientSecretSpec,
                    // ["nginx.ingress.kubernetes.io/auth-tls-verify-client"] = "on",
                    // ["nginx.ingress.kubernetes.io/proxy-ssl-secret"] = dbAdminClientSecretSpec,
                    ["nginx.ingress.kubernetes.io/force-ssl-redirect"] =  "true",
                    ["nginx.ingress.kubernetes.io/ssl-passthrough"] = "true",
                },
            },
            Spec = new IngressSpecArgs {
                Rules = new() {
                    new IngressRuleArgs {
                        Host = ravenDomain,
                        Http = new HTTPIngressRuleValueArgs {
                            Paths = new() {
                                new HTTPIngressPathArgs {
                                    Path = "/",
                                    PathType = "Prefix",
                                    Backend = new IngressBackendArgs {
                                        Service = new IngressServiceBackendArgs {
                                            Name = service.Metadata.Apply(m => m.Name),
                                            Port = new ServiceBackendPortArgs {
                                                Name = "https",
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        }, new() { Parent = this });

        var license = new Secret($"{name}-license", new SecretArgs {
            Metadata = new ObjectMetaArgs {
                Namespace = nsName, 
            },
            Data = new() {
                ["license.txt"] = args.License.Apply(x => Convert.ToBase64String(Encoding.UTF8.GetBytes(x))),
            },
        }, new() { Parent = this });
        
        var certsVolumeName = "ravendb-certs";
        var licenseVolumeName = "license";
        var instance = new StatefulSet(name, new() {
            Metadata = new ObjectMetaArgs {
                Name = name,
                Namespace = nsName,
            },
            Spec = new StatefulSetSpecArgs {
                Replicas = 1,
                ServiceName = service.Metadata.Apply(m => m.Name),
                Selector = new LabelSelectorArgs {
                    MatchLabels = podSelector,
                },
                Template = new PodTemplateSpecArgs {
                    Metadata = new ObjectMetaArgs {
                        Labels = podSelector,
                    },
                    Spec = new PodSpecArgs {
                        Containers = new() {
                            new ContainerArgs {
                                Name = "db",
                                Image = args.Image.Apply(i => args.Version.Apply(v => $"{i}:{v}")),
                                ImagePullPolicy = "IfNotPresent",
                                Ports = new() {
                                    new ContainerPortArgs {
                                        Name = "https",
                                        ContainerPortValue = 443,
                                        Protocol = "TCP"
                                    },
                                    new ContainerPortArgs {
                                        Name = "tcp",
                                        ContainerPortValue = 38888,
                                        Protocol = "TCP"
                                    },
                                },
                                LivenessProbe = new ProbeArgs {
                                    TcpSocket = new TCPSocketActionArgs {
                                        Port = "https"
                                    },
                                    InitialDelaySeconds = 10,
                                    PeriodSeconds = 10,
                                },
                                ReadinessProbe = new ProbeArgs {
                                    TcpSocket = new TCPSocketActionArgs {
                                        Port = "https"
                                    },
                                    InitialDelaySeconds = 10,
                                    PeriodSeconds = 10,
                                },
                                Resources = new ResourceRequirementsArgs {
                                    Limits = new() {
                                        ["cpu"] = "1500m",
                                        ["memory"] = "2Gi",
                                    },
                                    Requests = new() {
                                        ["cpu"] = "500m",
                                        ["memory"] = "1Gi",
                                    },
                                },
                                Env = new() {
                                    new EnvVarArgs {
                                        Name = "POD_IP",
                                        ValueFrom = new EnvVarSourceArgs {
                                            FieldRef = new ObjectFieldSelectorArgs {
                                                FieldPath = "status.podIP"
                                            }
                                        }
                                    },
                                    new EnvVarArgs {
                                        Name = "POD_NAME",
                                        ValueFrom = new EnvVarSourceArgs {
                                            FieldRef = new ObjectFieldSelectorArgs {
                                                FieldPath = "metadata.name"
                                            }
                                        }
                                    },
                                    new EnvVarArgs {
                                        Name = "RAVEN_ARGS",
                                        Value = nsName.Apply(n => string.Join(' ',
                                            "--ServerUrl=https://0.0.0.0/", "--ServerUrl.Tcp=tcp://0.0.0.0:38888/",
                                            $"--PublicServerUrl=https://{ravenDomain}",
                                            $"--PublicServerUrl.Tcp=tcp://$(POD_NAME).{serviceName}.{n}.svc.cluster.local:38888/",
                                            "--log-to-console")),
                                    },
                                    new EnvVarArgs {
                                        Name = "RAVEN_Setup_Mode",
                                        Value = "NONE",
                                    },
                                    new EnvVarArgs {
                                        Name = "RAVEN_License_Eula_Accepted",
                                        Value = "true",
                                    },
                                    new EnvVarArgs {
                                        Name = "RAVEN_Logs_Mode",
                                        Value = "Information",
                                    },
                                    new EnvVarArgs {
                                        Name = "RAVEN_Security_WellKnownCertificates_Admin",
                                        Value = adminCert.CertificateThumbprint,
                                    },
                                    new EnvVarArgs {
                                        Name = "RAVEN_Security_Certificate_Path",
                                        Value = "/config/certs/keystore.p12",
                                    },
                                    new EnvVarArgs {
                                        Name = "RAVEN_Security_Certificate_Password",
                                        ValueFrom = new EnvVarSourceArgs {
                                            SecretKeyRef = new SecretKeySelectorArgs {
                                                Name = keystorePasswordSecret.Metadata.Apply(m => m.Name),
                                                Key = keystorePasswordKey
                                            },
                                        },
                                    },
                                    new EnvVarArgs {
                                        Name = "RAVEN_License_Path",
                                        Value = "/config/license/license.txt",
                                    },
                                    new EnvVarArgs {
                                        Name = "RAVEN_License",
                                        Value = "",
                                    },
                                },
                                VolumeMounts = new() {
                                    new VolumeMountArgs {
                                        Name = name,
                                        MountPath = "/opt/RavenDB/Server/RavenData",
                                    },
                                    new VolumeMountArgs {
                                        Name = certsVolumeName,
                                        MountPath = "/config/certs",
                                        ReadOnly = true,
                                    },
                                    new VolumeMountArgs {
                                        Name = licenseVolumeName,
                                        MountPath = "/config/license",
                                        ReadOnly = true,
                                    },
                                },
                            },
                        },
                        Volumes = new() {
                            new VolumeArgs {
                                Name = certsVolumeName,
                                Secret = new SecretVolumeSourceArgs {
                                    SecretName = certsSecretName,
                                },
                            },
                            new VolumeArgs {
                                Name = licenseVolumeName,
                                Secret = new SecretVolumeSourceArgs() {
                                    SecretName = license.Metadata.Apply(m => m.Name),
                                },
                            },
                        },
                    },
                },
                VolumeClaimTemplates = new() {
                    new PersistentVolumeClaimArgs {
                        Metadata = new ObjectMetaArgs {
                            Name = name,
                        },
                        Spec = new PersistentVolumeClaimSpecArgs {
                            AccessModes = new() { "ReadWriteOnce" },
                            Resources = new ResourceRequirementsArgs {
                                Requests = new() {
                                    ["storage"] = "4G",
                                },
                            },
                        },
                    },
                },
            },
        }, new() { Parent = this, DeleteBeforeReplace = true });

        RegisterOutputs(new Dictionary<string, object?> {
            ["domain"] = Domain = Output.Create(ravenDomain),
            ["service"] = Service = service.Metadata.Apply(m => m.Name),
            ["service-host"] =
                ServiceHost = service.Metadata.Apply(m => $"{m.Name}.{args.Namespace}.svc.cluster.local"),
            ["admin-thumbprint"] = AdminThumbprint = adminCert.CertificateThumbprint,
            ["admin-password"] = adminCert.CertificatePassword,
        });
    }

    [Output]
    public Output<string> AdminThumbprint { get; set; }
    
    [Output]
    public Output<string> Domain { get; set; }

    [Output]
    public Output<string> Service { get; set; }

    [Output]
    public Output<string> ServiceHost { get; set; }
}

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public class RavenDbArgs : ResourceArgs
{
    public Input<string>? Namespace { get; set; }
    public Input<string> Image { get; set; } = "ravendb/ravendb";
    public Input<string> Version { get; set; } = "5.4-ubuntu-latest";
    
    public required Input<string> License { get; set; }
    public required Input<string> Issuer { get; set; }
    public required Input<string> SelfSignedIssuer { get; set; }
}