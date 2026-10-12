using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests;

public sealed class PaymentProviderCreationAttemptTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
    public Task InitializeAsync() => _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();
    private DbContextOptions<PaymentsDbContext> Options => new DbContextOptionsBuilder<PaymentsDbContext>()
        .UseNpgsql(_postgres.GetConnectionString()).Options;

    [Fact]
    public async Task FirstAttemptRemainsImmutableAcrossRetryAndRestart()
    {
        var id = await Seed();
        var first = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
        await using (var writer = new PaymentsDbContext(Options))
            Assert.Equal(first, await new EfPaymentProviderCreationAttempts(writer, new Clock(first)).GetOrRecordAsync(id));
        await using var retry = new PaymentsDbContext(Options);
        Assert.Equal(first, await new EfPaymentProviderCreationAttempts(retry, new Clock(first.AddDays(2))).GetOrRecordAsync(id));
        Assert.Equal(PaymentStatus.Pending, (await retry.Payments.SingleAsync()).Status);
        Assert.Single(await retry.PaymentProviderCreationAttempts.ToListAsync());
        Assert.Empty(await retry.PaymentLedgerPostingIntents.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentAttemptsObserveOneDurableStartTime()
    {
        var id = await Seed();
        var first = DateTimeOffset.UtcNow;
        async Task<DateTimeOffset> Record(DateTimeOffset time)
        {
            await using var context = new PaymentsDbContext(Options);
            return await new EfPaymentProviderCreationAttempts(context, new Clock(time)).GetOrRecordAsync(id);
        }
        var times = await Task.WhenAll(Record(first), Record(first.AddMinutes(1)));
        Assert.Equal(times[0], times[1]);
    }

    [Fact]
    public async Task MissingPaymentCannotAcquireCreationAttempt()
    {
        await Seed();
        await using var context = new PaymentsDbContext(Options);
        var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            new EfPaymentProviderCreationAttempts(context, TimeProvider.System).GetOrRecordAsync(PaymentId.New()));
        Assert.Equal(Npgsql.PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        Assert.Empty(await context.PaymentProviderCreationAttempts.ToListAsync());
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
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
