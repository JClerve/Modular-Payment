using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests;

public sealed class PaymentLedgerPostingPreparationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
    public Task InitializeAsync() => _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();
    private DbContextOptions<PaymentsDbContext> Options => new DbContextOptionsBuilder<PaymentsDbContext>()
        .UseNpgsql(_postgres.GetConnectionString()).Options;

    [Fact]
    public async Task PreparationRoundTripsWithoutCompletingPaymentOrCreatingDispatchableIntent()
    {
        var request = await Seed();
        await using var writer = new PaymentsDbContext(Options);
        var repository = new EfPaymentLedgerPostingPreparationRepository(writer);
        Assert.Null(await repository.GetByPaymentIdAsync(PaymentId.From(request.PaymentId)));
        await repository.SaveAsync(request);
        await using var reader = new PaymentsDbContext(Options);
        Assert.Equal(request, await new EfPaymentLedgerPostingPreparationRepository(reader)
            .GetByPaymentIdAsync(PaymentId.From(request.PaymentId)));
        Assert.Equal(PaymentStatus.Pending, (await reader.Payments.SingleAsync()).Status);
        Assert.Empty(await reader.PaymentLedgerPostingIntents.ToListAsync());
        Assert.Empty(await reader.PaymentLedgerPostingEvents.ToListAsync());
    }

    [Fact]
    public async Task MatchingReplayIsIdempotentAndDifferentInputsPreserveOriginal()
    {
        var request = await Seed();
        await using var writer = new PaymentsDbContext(Options);
        var repository = new EfPaymentLedgerPostingPreparationRepository(writer);
        await repository.SaveAsync(request);
        await repository.SaveAsync(request);
        var conflicts = new[]
        {
            request with { TransactionId = Guid.NewGuid() }, request with { LedgerId = Guid.NewGuid() },
            request with { ProcessorReceivableAccountId = Guid.NewGuid() },
            request with { MerchantPayableAccountId = Guid.NewGuid() },
            request with { PlatformRevenueAccountId = Guid.NewGuid() }, request with { FeeAmountMinorUnits = 101 },
            request with { Currency = "JPY" }, request with { GrossAmountMinorUnits = 1235 }
        };
        foreach (var conflict in conflicts)
            await Assert.ThrowsAsync<PaymentLedgerPostingIntentConflictException>(() => repository.SaveAsync(conflict));
        Assert.Equal(request, await repository.GetByPaymentIdAsync(PaymentId.From(request.PaymentId)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentPreparationsHaveOneImmutableWinner(bool conflict)
    {
        var request = await Seed();
        var other = conflict ? request with { FeeAmountMinorUnits = 101 } : request;
        async Task<bool> Save(PaymentLedgerPostingRequest candidate)
        {
            await using var context = new PaymentsDbContext(Options);
            try { await new EfPaymentLedgerPostingPreparationRepository(context).SaveAsync(candidate); return true; }
            catch (PaymentLedgerPostingIntentConflictException) { return false; }
        }
        var results = await Task.WhenAll(Save(request), Save(other));
        Assert.Equal(conflict ? 1 : 2, results.Count(x => x));
        await using var reader = new PaymentsDbContext(Options);
        var winner = await new EfPaymentLedgerPostingPreparationRepository(reader).GetByPaymentIdAsync(PaymentId.From(request.PaymentId));
        Assert.True(winner == request || winner == other);
        Assert.Equal(PaymentStatus.Pending, (await reader.Payments.SingleAsync()).Status);
    }

    [Fact]
    public async Task PreparationRequiresAnExistingPayment()
    {
        var request = await Seed();
        await using var context = new PaymentsDbContext(Options);
        var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            new EfPaymentLedgerPostingPreparationRepository(context).SaveAsync(request with { PaymentId = Guid.NewGuid() }));
        Assert.Equal(Npgsql.PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        Assert.Empty(await context.PaymentLedgerPostingPreparations.ToListAsync());
    }

    private async Task<PaymentLedgerPostingRequest> Seed()
    {
        await using var context = new PaymentsDbContext(Options);
        await context.Database.MigrateAsync();
        var payment = Payment.Create(12.34m, "SGD");
        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        return PaymentLedgerPostingRequestFactory.Create(payment, new PaymentLedgerPostingInput(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100));
    }
}
