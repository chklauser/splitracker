using System;
using Pulumi;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Apps.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using System.Collections.Generic;
using System.Text;
using Klauser.Do.Infrastructure.CertManager;
using Pulumi.Kubernetes;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Networking.V1;
using Pulumi.Kubernetes.Types.Inputs.Networking.V1;
using Splitracker.Deploy;
using Config = Pulumi.Config;

return await Deployment.RunAsync(() =>
{
    var appLabels = new InputMap<string> {
        { "app", "splitracker" }
    };
    var config = new Config();
    var ravenClientCertBase64 = config.RequireSecret("raven-cert");
    var ravenClientCertPassword = config.RequireSecret("raven-cert-password");
    var version = config.Require("version");
    var infra = new StackReference("chklauser/do.klauser.link/prod");

    var kubeConfig = infra.GetOutput("kubeconfig").Apply(x => (string)x!);
    var clusterProvider = new Provider("k8s-infra", new() {
        KubeConfig = kubeConfig,
    });
    var plannedNamespaceName = $"splitracker-{Deployment.Instance.StackName}";
    var ns = new Namespace(plannedNamespaceName,
        new() { Metadata = new ObjectMetaArgs { Name = plannedNamespaceName } },
        new() { Provider = clusterProvider });
    var nsName = ns.Metadata.Apply(x => x.Name);

    var selector = new InputMap<string> {
        ["app"] = "splitracker"
    };

    var service = new Service("splitracker", new() {
        Metadata = new ObjectMetaArgs {
            Namespace = nsName,
            Name = "splitracker",
            Annotations = new() {
                ["pulumi.com/skipAwait"] = "true",
            },
        },
        Spec = new ServiceSpecArgs {
            Type = "ClusterIP",
            Selector = selector,
            Ports = new ServicePortArgs {
                Port = 80,
                Name = "http",
            },
        },
    }, new() { Provider = clusterProvider });

    var publicDomain = Deployment.Instance.StackName switch {
        "prod" => "splitracker.klauser.link",
        var stackName => $"{stackName}-splitracker.do.klauser.link",
    };
    const string certSecretName = "splitracker-cert";
    var cert = new Certificate("cert", new() {
        Metadata = new ObjectMetaArgs {
            Namespace = nsName,
        },
        Spec = new CertificateSpecArgs {
            IssuerRef = new CertificateObjectReference {
                Kind = "ClusterIssuer",
                Name = "le-prod",
            },
            CommonName = publicDomain,
            DnsNames = new() { publicDomain },
            SecretName = certSecretName,
        },
    }, new() { Provider = clusterProvider });

    var ingress = new Ingress("splitracker", new() {
        Metadata = new ObjectMetaArgs {
            Namespace = nsName,
            Annotations = new() {
                ["pulumi.com/skipAwait"] = "true",
                ["nginx.ingress.kubernetes.io/proxy-buffers-number"] = "8",
                ["nginx.ingress.kubernetes.io/proxy-buffer-size"] = "16k",
            },
        },
        Spec = new IngressSpecArgs {
            IngressClassName = "nginx",
            Tls = new() {
                new IngressTLSArgs {
                    Hosts = new() { publicDomain },
                    SecretName = certSecretName,
                },
            },
            Rules = new() {
                new IngressRuleArgs {
                    Host = publicDomain,
                    Http = new HTTPIngressRuleValueArgs {
                        Paths = new() {
                            new HTTPIngressPathArgs {
                                Path = "/",
                                PathType = "Prefix",
                                Backend = new IngressBackendArgs {
                                    Service = new IngressServiceBackendArgs {
                                        Name = service.Metadata.Apply(x => x.Name),
                                        Port = new ServiceBackendPortArgs {
                                            Name = "http",
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        },
    }, new() { Provider = clusterProvider });

    var ravenCertSecret = new Secret("raven-client-cert", new() {
        Metadata = new ObjectMetaArgs {
            Namespace = nsName,
        },
        Data = new() {
            ["keystore.p12"] = ravenClientCertBase64,
            ["password"] = ravenClientCertPassword.Apply(p => Convert.ToBase64String(Encoding.UTF8.GetBytes(p))),
        },
    }, new() { Provider = clusterProvider });

    var clientSecretKey = "client-secret";
    var oidcSecret = new Secret("oidc", new() {
        Metadata = new ObjectMetaArgs() {
            Namespace = nsName,
        },
        Data = new() {
          [clientSecretKey] = config.RequireSecret("oauth-client-secret"),  
        },
    }, new(){Provider = clusterProvider});
    
    var healthEndpoint = new HTTPGetActionArgs {
        Port = "http",
        Path = "/healthz",
    };
    const string ravenClientCertVolumeName = "raven-client-cert";
    var deployment =
        new Pulumi.Kubernetes.Apps.V1.Deployment("splitracker", new() {
            Metadata = new ObjectMetaArgs {
                Namespace = nsName,
            },
            Spec = new DeploymentSpecArgs {
                Replicas = 1,
                Selector = new LabelSelectorArgs {
                    MatchLabels = selector,
                },
                Template = new PodTemplateSpecArgs {
                    Metadata = new ObjectMetaArgs {
                        Labels = selector,
                    },
                    Spec = new PodSpecArgs {
                        ImagePullSecrets = new() {
                            new LocalObjectReferenceArgs {
                                Name = "klauser",
                            },
                        },
                        Containers = new() {
                            new ContainerArgs {
                                Name = $"splitracker-{Deployment.Instance.StackName}",
                                Image = $"registry.digitalocean.com/klauser/splitracker:{version}",
                                ImagePullPolicy = "IfNotPresent",
                                Ports = new() {
                                  new ContainerPortArgs {
                                      Protocol = "TCP",
                                      Name = "http",
                                      ContainerPortValue = 80,
                                  },
                                },
                                ReadinessProbe = new ProbeArgs {
                                    HttpGet = healthEndpoint,
                                    InitialDelaySeconds = 2,
                                    PeriodSeconds = 10,
                                },
                                LivenessProbe = new ProbeArgs {
                                    HttpGet = healthEndpoint,
                                    InitialDelaySeconds = 10,
                                    PeriodSeconds = 10,
                                },
                                StartupProbe = new ProbeArgs {
                                    HttpGet = healthEndpoint,
                                    InitialDelaySeconds = 2,
                                    PeriodSeconds = 2,
                                },
                                VolumeMounts = new() {
                                    new VolumeMountArgs {
                                        Name = ravenClientCertVolumeName,
                                        MountPath = "/etc/raven-client-cert",
                                        ReadOnly = true,
                                    },
                                },
                                Env = new() {
                                    ("DOTNET_ENVIRONMENT", Deployment.Instance.StackName switch {
                                        "dev" => "Development",
                                        "prod" => "Production",
                                        _ => throw new($"Unknown stack name {Deployment.Instance.StackName}"),
                                    }).ToEnvVarArgs(),
                                    ("Raven__CertificatePath", "/etc/raven-client-cert/keystore.p12").ToEnvVarArgs(),
                                    ("Raven__CertificatePasswordFile", "/etc/raven-client-cert/password").ToEnvVarArgs(),
                                    new EnvVarArgs {
                                        Name = "AzureAdB2C__ClientSecret",
                                        ValueFrom = new EnvVarSourceArgs {
                                            SecretKeyRef = new Pulumi.Kubernetes.Types.Inputs.Core.V1.SecretKeySelectorArgs {
                                                Name = oidcSecret.Metadata.Apply(m => m.Name),
                                                Key = clientSecretKey,
                                            },
                                        },
                                    },
                                },
                                Resources = new ResourceRequirementsArgs {
                                    Limits = new() {
                                        ["cpu"] = "1000m",
                                        ["memory"] = "512Mi",
                                    },
                                    Requests = new() {
                                        ["cpu"] = "128m",
                                        ["memory"] = "256Mi",
                                    },
                                },
                            },
                        },
                        Volumes = new() {
                            new VolumeArgs {
                                Name = ravenClientCertVolumeName,
                                Secret = new SecretVolumeSourceArgs {
                                    SecretName = ravenCertSecret.Metadata.Apply(m => m.Name),
                                },
                            },
                        },
                    },
                },  
            },
        }, new() { Provider = clusterProvider });

    // export the deployment name
    return new Dictionary<string, object?> {
    };
});

