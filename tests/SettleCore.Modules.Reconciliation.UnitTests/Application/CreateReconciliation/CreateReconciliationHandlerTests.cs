using SettleCore.Modules.Reconciliation.Application;
using SettleCore.Modules.Reconciliation.Application.CreateReconciliation;
using SettleCore.Modules.Reconciliation.Domain;

namespace SettleCore.Modules.Reconciliation.UnitTests.Application.CreateReconciliation;

public sealed class CreateReconciliationHandlerTests
{
    [Fact]
    public async Task HandlePersistsReconciliationResult()
    {
        var repository = new RecordingReconciliationRepository();
        var handler = new CreateReconciliationHandler(repository);

        var command = new CreateReconciliationCommand(
            ExpectedAmount: 100.00m,
            ExpectedCurrency: "SGD",
            ActualAmount: 95.00m,
            ActualCurrency: "SGD");

        var result = await handler.HandleAsync(command);

        Assert.Equal(
            ReconciliationStatus.AmountMismatch,
            result.Status);

        Assert.NotNull(repository.AddedRecord);

        Assert.Equal(
            ReconciliationStatus.AmountMismatch,
            repository.AddedRecord.Status);

        Assert.Equal(100.00m, repository.AddedRecord.ExpectedAmount);
        Assert.Equal(95.00m, repository.AddedRecord.ActualAmount);
        Assert.Equal("SGD", repository.AddedRecord.ExpectedCurrency);
        Assert.Equal("SGD", repository.AddedRecord.ActualCurrency);
    }

    private sealed class RecordingReconciliationRepository
        : IReconciliationRepository
    {
        public ReconciliationRecord? AddedRecord { get; private set; }

        public Task AddAsync(
            ReconciliationRecord record,
            CancellationToken cancellationToken = default)
        {
            AddedRecord = record;

            return Task.CompletedTask;
        }

        public Task<ReconciliationRecord?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AddedRecord?.Id == id ? AddedRecord : null);
        }
    }

    [Fact]
    public async Task HandleReturnsAndPersistsNormalizedCurrencies()
    {
        var repository = new RecordingReconciliationRepository();
        var handler = new CreateReconciliationHandler(repository);

        var command = new CreateReconciliationCommand(
            ExpectedAmount: 100.00m,
            ExpectedCurrency: "sgd",
            ActualAmount: 100.00m,
            ActualCurrency: "sgd");

        var result = await handler.HandleAsync(command);

        Assert.Equal(ReconciliationStatus.Matched, result.Status);
        Assert.Equal("SGD", result.ExpectedCurrency);
        Assert.Equal("SGD", result.ActualCurrency);

        Assert.NotNull(repository.AddedRecord);
        Assert.Equal("SGD", repository.AddedRecord.ExpectedCurrency);
        Assert.Equal("SGD", repository.AddedRecord.ActualCurrency);
    }

    [Fact]
    public async Task HandleRejectsInvalidCurrencyWithoutPersisting()
    {
        var repository = new RecordingReconciliationRepository();
        var handler = new CreateReconciliationHandler(repository);

        var command = new CreateReconciliationCommand(
            ExpectedAmount: 100.00m,
            ExpectedCurrency: " ",
            ActualAmount: 100.00m,
            ActualCurrency: "SGD");

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command));

        Assert.Null(repository.AddedRecord);
    }
}
