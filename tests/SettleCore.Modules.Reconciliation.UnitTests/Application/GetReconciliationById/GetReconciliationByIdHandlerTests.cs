using SettleCore.Modules.Reconciliation.Application;
using SettleCore.Modules.Reconciliation.Application.GetReconciliationById;
using SettleCore.Modules.Reconciliation.Domain;

namespace SettleCore.Modules.Reconciliation.UnitTests.Application.GetReconciliationById;

public sealed class GetReconciliationByIdHandlerTests
{
    [Fact]
    public async Task HandleReturnsRecordWhenFound()
    {
        var record = new ReconciliationRecord(
            Guid.NewGuid(),
            ReconciliationStatus.CurrencyMismatch,
            100.00m,
            100.00m,
            "SGD",
            "USD");
        var handler = new GetReconciliationByIdHandler(
            new StubRepository(record));

        var result = await handler.HandleAsync(
            new GetReconciliationByIdQuery(record.Id));

        var found = Assert.IsType<GetReconciliationByIdResult>(result);
        Assert.Equal(record.Id, found.ReconciliationId);
        Assert.Equal(record.Status, found.Status);
        Assert.Equal(record.ExpectedAmount, found.ExpectedAmount);
        Assert.Equal(record.ActualAmount, found.ActualAmount);
        Assert.Equal(record.ExpectedCurrency, found.ExpectedCurrency);
        Assert.Equal(record.ActualCurrency, found.ActualCurrency);
    }

    [Fact]
    public async Task HandleReturnsNullWhenRecordIsMissing()
    {
        var handler = new GetReconciliationByIdHandler(
            new StubRepository(null));

        var result = await handler.HandleAsync(
            new GetReconciliationByIdQuery(Guid.NewGuid()));

        Assert.Null(result);
    }

    private sealed class StubRepository(ReconciliationRecord? record)
        : IReconciliationRepository
    {
        public Task AddAsync(
            ReconciliationRecord record,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ReconciliationRecord?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(record?.Id == id ? record : null);
        }
    }
}
