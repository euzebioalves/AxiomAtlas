using Axiom.Atlas.Domain.Entities.ServiceDesk;
using Axiom.Atlas.Infrastructure.Services.ServiceDesk;
using Axiom.Atlas.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;

namespace Axiom.Atlas.Tests.Services;

public class GlpiImprovementTicketSynchronizationQueueTests
{
    [Fact]
    public async Task RequestSynchronizationAsync_DeduplicatesAnActiveRefreshJob()
    {
        await using var provider = CreateProvider();
        var queue = CreateQueue(provider);

        var first = await queue.RequestSynchronizationAsync("operator-1");
        var second = await queue.RequestSynchronizationAsync("operator-2");

        Assert.Equal(first.Id, second.Id);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await context.IntegrationSynchronizationJobs.ToListAsync());
    }

    [Fact]
    public async Task ClaimAndMarkSucceededAsync_PersistsTheCompletedState()
    {
        await using var provider = CreateProvider();
        var queue = CreateQueue(provider);
        var requested = await queue.RequestSynchronizationAsync();

        var claimed = await queue.ClaimNextAsync(CancellationToken.None);
        await queue.MarkSucceededAsync(claimed!.Id, CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await context.IntegrationSynchronizationJobs.SingleAsync(x => x.Id == requested.Id);
        Assert.Equal(IntegrationSynchronizationJobStatus.Succeeded, persisted.Status);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.NotNull(persisted.CompletedAt);
        Assert.Null(persisted.LastError);
    }

    [Fact]
    public async Task RetryAsync_CreatesANewJobAndKeepsTheFailedJobAsHistory()
    {
        await using var provider = CreateProvider();
        var queue = CreateQueue(provider);
        var original = await queue.RequestSynchronizationAsync("operator-1");

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var failed = await context.IntegrationSynchronizationJobs.SingleAsync(x => x.Id == original.Id);
            failed.Status = IntegrationSynchronizationJobStatus.Failed;
            failed.LastError = "Falha remota";
            failed.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        var retry = await queue.RetryAsync(original.Id, "operator-2");

        Assert.NotNull(retry);
        Assert.NotEqual(original.Id, retry!.Id);
        Assert.Equal(IntegrationSynchronizationJobStatus.Pending, retry.Status);
        Assert.Equal("operator-2", retry.RequestedByUserId);
        await using var verificationScope = provider.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await verificationContext.IntegrationSynchronizationJobs.CountAsync());
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        services.AddSingleton(connection);
        services.AddLogging();
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            options.UseSqlite(serviceProvider.GetRequiredService<SqliteConnection>()));

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return provider;
    }

    private static GlpiImprovementTicketSynchronizationQueue CreateQueue(ServiceProvider provider) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<GlpiImprovementTicketSynchronizationQueue>>());
}
