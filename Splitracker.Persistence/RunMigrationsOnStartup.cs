using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Raven.Migrations;

namespace Splitracker.Persistence;

/// <summary>
/// Should be registered before repositories so that data has the new shape before new indexes are created.
/// </summary>
public class RunMigrationsOnStartup : IHostedService
{
    readonly MigrationRunner runner;

    public RunMigrationsOnStartup(MigrationRunner runner)
    {
        this.runner = runner;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.Run(runner.Run, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}