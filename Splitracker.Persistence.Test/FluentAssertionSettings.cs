using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Splitracker.Domain;

namespace Splitracker.Persistence.Test;

[SuppressMessage("Usage", "CA2255:The \'ModuleInitializer\' attribute should not be used in libraries",
    Justification = "Tests are basically an application.")]
static class FluentAssertionSettings
{
    [ModuleInitializer]
    public static void Configure()
    {
        AssertionOptions.AssertEquivalencyUsing(opts =>
            // It never makes sense to assert equivalence of PointsVec.Normalized. It's a pure function.
            opts.Excluding(m =>
                m.DeclaringType == typeof(PointsVec) && m.Name == nameof(PointsVec.Normalized)
                )
            );
    }
}