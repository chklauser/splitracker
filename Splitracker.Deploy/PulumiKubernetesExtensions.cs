using Pulumi.Kubernetes.Types.Inputs.Core.V1;

namespace Splitracker.Deploy;

public static class PulumiKubernetesExtensions
{
    public static EnvVarArgs ToEnvVarArgs(this (string Name, Pulumi.Input<string> Value) envVar)
    {
        return new()
        {
            Name = envVar.Name,
            Value = envVar.Value,
        };
    }
    public static EnvVarArgs ToEnvVarArgs(this (string Name, string Value) envVar)
    {
        return new()
        {
            Name = envVar.Name,
            Value = envVar.Value,
        };
    }
}