using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Application.AttachProviderReference;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests;

public sealed class PaymentProviderReferenceAttachmentTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
    public Task InitializeAsync() => _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();
    private DbContextOptions<PaymentsDbContext> Options => new DbContextOptionsBuilder<PaymentsDbContext>()
        .UseNpgsql(_postgres.GetConnectionString()).Options;

    [Fact]
    public async Task StaleReferenceAttachmentCannotRevertConcurrentSuccess()
    {
        var id = await Seed();
        await using var stale = new PaymentsDbContext(Options);
        var snapshot = await new EfPaymentRepository(stale).GetByIdAsync(id);
        Assert.Equal(PaymentStatus.Pending, snapshot!.Status);
        await using (var success = new PaymentsDbContext(Options))
        {
            var payment = await success.Payments.SingleAsync();
            payment.MarkSucceeded();
            await success.SaveChangesAsync();
        }
        Assert.NotNull(await Handler(stale).HandleAsync(new(id.Value, "stripe", "pi_test")));
        await using var reader = new PaymentsDbContext(Options);
        var persisted = await reader.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.Succeeded, persisted.Status);
        Assert.Equal(ProviderPaymentReference.Create("stripe", "pi_test"), persisted.ProviderReference);
    }

    [Fact]
    public async Task MatchingReferenceReplaySucceedsButReplacementPreservesOriginal()
    {
        var id = await Seed();
        await using var context = new PaymentsDbContext(Options);
        var handler = Handler(context);
        Assert.NotNull(await handler.HandleAsync(new(id.Value, "stripe", "pi_test")));
        Assert.NotNull(await handler.HandleAsync(new(id.Value, "stripe", "pi_test")));
        await Assert.ThrowsAsync<ProviderPaymentReferenceConflictException>(() => handler.HandleAsync(new(id.Value, "stripe", "pi_other")));
        await using var reader = new PaymentsDbContext(Options);
        Assert.Equal(ProviderPaymentReference.Create("stripe", "pi_test"), (await reader.Payments.SingleAsync()).ProviderReference);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentAttachmentHasOneStableReference(bool conflict)
    {
        var id = await Seed();
        async Task<bool> Attach(string reference)
        {
            await using var context = new PaymentsDbContext(Options);
            try { await Handler(context).HandleAsync(new(id.Value, "stripe", reference)); return true; }
            catch (ProviderPaymentReferenceConflictException) { return false; }
        }
        var results = await Task.WhenAll(Attach("pi_first"), Attach(conflict ? "pi_second" : "pi_first"));
        Assert.Equal(conflict ? 1 : 2, results.Count(x => x));
        await using var reader = new PaymentsDbContext(Options);
        var payment = await reader.Payments.SingleAsync();
        Assert.True(payment.ProviderReference!.Value.Reference is "pi_first" or "pi_second");
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    private async Task<PaymentId> Seed()
    {
        await using var context = new PaymentsDbContext(Options);
        await context.Database.MigrateAsync();
        var payment = Payment.Create(12.34m, "SGD");
        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        return payment.Id;
    }

    private static AttachProviderReferenceHandler Handler(PaymentsDbContext context) => new(new EfPaymentRepository(context), new EfPaymentProviderReferencePersistence(context));
}
