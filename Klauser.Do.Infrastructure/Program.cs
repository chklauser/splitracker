using System;
using System.Collections.Generic;
using System.Linq;
using Klauser.Do.Infrastructure;
using Klauser.Do.Infrastructure.CertManager;
using Pulumi;
using Pulumi.DigitalOcean;
using Pulumi.DigitalOcean.Inputs;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Helm.V3;
using Pulumi.Kubernetes.Types.Inputs.Helm.V3;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Config = Pulumi.Config;
using K8sCustomResource = Pulumi.Kubernetes.ApiExtensions.CustomResource;
using Provider = Pulumi.Kubernetes.Provider;

return await Deployment.RunAsync(() =>
{
    var region = Region.AMS3;
    
    var doConfig = new Config("digitalocean");
    var config = new Config();
    var email = config.Require("maintaineremail");

    // Add your resources here
    // e.g. var resource = new Resource("name", new ResourceArgs { });
    var project = new Project("do.klauser.link",
        new() {
            Name = "do.klauser.link",
            Description = "Shared k8s cluster for personal projects.",
            Environment = "Production",
            Purpose = "Web Application",
        });
    var vpc = new Vpc("cluster", new() {
        Region = region.ToString(),
        IpRange = "10.5.0.0/16",
    });
    var domain = new Domain("apex", new() {
        Name = "do.klauser.link",
    });

    var cluster = new KubernetesCluster("klauser", new() {
        Region = region,
        Version = "1.24.4-do.0",
        AutoUpgrade = true,
        Ha = false,
        SurgeUpgrade = false,
        MaintenancePolicy = new KubernetesClusterMaintenancePolicyArgs {
            Day = "tuesday",
            StartTime = "16:00",
        },
        VpcUuid = vpc.Id,
        NodePool = new KubernetesClusterNodePoolArgs {
            Name = "primary",
            Size = "s-2vcpu-4gb",
            NodeCount = 1,
            AutoScale = false,
        },
    });

    var containerRegistry = new ContainerRegistry("klauser", new() {
        Name = "klauser",
        Region = region.ToString(),
        SubscriptionTierSlug = "starter",
    });

    var projectResourceAssignment = new ProjectResources("do.klauser.link", new() {
        Project = project.Id,
        Resources = new() { domain.DomainUrn, cluster.ClusterUrn },
    });

    var kubeConfig = cluster.KubeConfigs.Apply(cs => cs.First().RawConfig ??
        throw new ArgumentException("Somehow the cluster didn't return a kubeconfig"));
    var clusterProvider = new Provider("k8s-infra", new() {
        KubeConfig = kubeConfig
    });
    
    var wildcardClusterDomain = new DnsRecord("cluster_wildcard", new() {
        Domain = domain.Id,
        Type = "A",
        Name = "*",
        Value = cluster.NodePool.Apply(p => GetDroplet.Invoke(new GetDropletInvokeArgs {
            Name = p.Nodes.First().Name ?? throw new ArgumentException("Cluster node has no name"),
        }).Apply(d => d.Ipv4Address)),
    });

    var infrastructureNamespace = "infra";
    var ingressController = new Release("ingress", new() {
        Name = "ingress",
        Atomic = true,
        CreateNamespace = true,
        Namespace = infrastructureNamespace,
        RepositoryOpts = new RepositoryOptsArgs {
            Repo = "https://kubernetes.github.io/ingress-nginx"
        },
        Chart = "ingress-nginx",
        Version = "4.4.0",
        Values = new Dictionary<string, object> {
            ["controller"] = new Dictionary<string, object> {
                ["service"] = new Dictionary<string, object> {
                    ["enabled"] = false,
                },
                ["dnsPolicy"] = "ClusterFirstWithHostNet",
                ["hostNetwork"] = true,
                ["extraArgs"] = new Dictionary<string, object> {
                    ["enable-ssl-passthrough"] = "",
                },
                // Can't do a rolling update because we can only listen on the host ports once
                ["updateStrategy"] = new Dictionary<string, object> { 
                    ["type"] = "Recreate",
                    },
            },
        },
    }, new() {Provider = clusterProvider});

    var firewall = new Firewall("allow-ingress", new() {
        DropletIds = cluster.NodePool
            .Apply(p => p.Nodes.Select(n => 
                int.Parse(n.DropletId!)).ToList()),
        InboundRules = new() {
            new FirewallInboundRuleArgs() {
                Protocol = "tcp",
                PortRange = "80",
                SourceAddresses = new() {
                    "0.0.0.0/0",
                    "::/0",
                    },
            },
            new FirewallInboundRuleArgs() {
                Protocol = "tcp",
                PortRange = "443",
                SourceAddresses = new() {
                    "0.0.0.0/0",
                    "::/0",
                    },
            },
        },
    });

    var certManager = new Release("cm", new() {
        Name = "cm",
        RepositoryOpts = new RepositoryOptsArgs {
            Repo = "https://charts.jetstack.io"
        },
        Chart = "cert-manager",
        Version = "1.10.0",
        Namespace = infrastructureNamespace,
        Values = new Dictionary<string, object> {
            ["installCRDs"] = true,
        },
    }, new() { Provider = clusterProvider });

    var kubeStateMetrics = new Release("kube-state-metrics", new() {
        Name = "advanced",
        Namespace = "kube-system",
        CreateNamespace = true,
        CleanupOnFail = true,
        RepositoryOpts = new RepositoryOptsArgs {
            Repo = "https://prometheus-community.github.io/helm-charts",
        },
        Chart = "kube-state-metrics",
        Version = "4.24.0",
        Values = new() {
            ["resources"] = new InputMap<object?>() {
                ["limits"] = new InputMap<object?>() {
                    ["cpu"] = "100m",
                    ["memory"] = "64Mi",
                },
                ["requests"] = new InputMap<object?>() {
                    ["cpu"] = "10m",
                    ["memory"] = "32Mi",
                },
            },
        },
    }, new() {Provider = clusterProvider});
    
    var digitalOceanTokenSecret = new Secret("do-token", new() {
        Metadata = new ObjectMetaArgs() {
          Namespace  = infrastructureNamespace
        },
        StringData = new() {
            ["token"] = doConfig.RequireSecret("token")
        },
    }, new() {Provider = clusterProvider});

    var letsEncryptSolver = new InputList<AcmeChallengeSolverArgs>() {
        new AcmeChallengeSolverArgs {
            Selector = new CertificateDnsNameSelectorArgs {
                DnsZones = new() {
                    "do.klauser.link",
                },
            },
            Dns01 = new AcmeChallengeSolverDns01Args {
                DigitalOcean = new AcmeIssuerDns01ProviderDigitalOceanArgs {
                    TokenSecretRef = new SecretKeySelectorArgs {
                        Name = digitalOceanTokenSecret.Metadata.Apply(m => m.Name),
                        Key = "token",
                    },
                },
            },
        },
        new AcmeChallengeSolverArgs {
            Http01 = new AcmeChallengeSolverHttp01Args {
                Ingress = new AcmeChallengeSolverHttp01IngressArgs {
                    ServiceType = "ClusterIP",
                    Class = "nginx",
                },
            },
        },
    };
    var letsEncryptStagingIssuer = new ClusterIssuer("le-staging", new() {
            Metadata = new ObjectMetaArgs() {
                Name = "le-staging",
            },
            Spec = new IssuerSpecArgs {
                Acme = new AcmeSpecArgs {
                    Email = email,
                    Server = "https://acme-staging-v02.api.letsencrypt.org/directory",
                    PrivateKeySecretRef = new SecretKeySelectorArgs {
                        Name = "letsencrypt-staging",
                    },
                    Solvers = letsEncryptSolver,
                },
            },
        },
        new() { Provider = clusterProvider, DependsOn = new() { certManager } , DeleteBeforeReplace = true});
    var letsEncryptProdIssuer = new ClusterIssuer("le-prod", new() {
            Metadata = new ObjectMetaArgs() {
                Name = "le-prod",
            },
            Spec = new IssuerSpecArgs {
                Acme = new AcmeSpecArgs {
                    Email = email,
                    Server = "https://acme-v02.api.letsencrypt.org/directory",
                    PrivateKeySecretRef = new SecretKeySelectorArgs {
                        Name = "letsencrypt-prod",
                    },
                    Solvers = letsEncryptSolver,
                },
            },
        },
        new() { Provider = clusterProvider, DependsOn = new() { certManager } , DeleteBeforeReplace = true});
    var selfSignedIssuer = new ClusterIssuer("self-signed", new() {
            Metadata = new ObjectMetaArgs {
                Name = "self-signed",
            },
            Spec = new IssuerSpecArgs {
                SelfSigned = new SelfSignedSpecArgs(),
            },
        },
        new() { Provider = clusterProvider, DependsOn = new() { certManager }, DeleteBeforeReplace = true });

    var raven = new RavenDb("raven", new() {
        Issuer = letsEncryptProdIssuer.Metadata.Apply(m => m.Name),
        SelfSignedIssuer = selfSignedIssuer.Metadata.Apply(m => m.Name),
        Namespace = "raven",
        License = config.Require("raven-license"),
    }, kubeConfig, new() {
        Provider = clusterProvider,
    });

    // Export outputs here
    return new Dictionary<string, object?> {
        ["vpc-cluster-name"] = vpc.Name,
        ["vpc-cluster-urn"] = vpc.VpcUrn,
        ["domain-apex-urn2"] = domain.DomainUrn,
        ["kubeconfig"] = Output.CreateSecret(cluster.KubeConfigs.First().Apply(k => k.RawConfig)),
        ["clusterissuer-staging-name"] = letsEncryptStagingIssuer.Metadata.Apply(m => m.Name),
        ["clusterissuer-prod-name"] = letsEncryptProdIssuer.Metadata.Apply(m => m.Name),
        ["raven-admin-thumbprint"] = raven.AdminThumbprint,
    };
});

