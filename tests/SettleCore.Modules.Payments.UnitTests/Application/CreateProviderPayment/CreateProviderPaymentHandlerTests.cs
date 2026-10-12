using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.CreateProviderPayment;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Application.CreateProviderPayment;

public sealed class CreateProviderPaymentHandlerTests
{
    [Fact]
    public async Task PreparedPaymentCreatesNeutralProviderRequestPersistsReferenceAndStaysPending()
    {
        var state = new State();
        using var cancellation = new CancellationTokenSource();
        var result = await state.Handler.HandleAsync(state.Payment!.Id.Value, cancellation.Token);
        Assert.Same(state.Result, result);
        Assert.Equal(state.Payment.Id, state.Request!.PaymentId);
        Assert.Equal(1234, state.Request.AmountMinorUnits);
        Assert.Equal("SGD", state.Request.Currency);
        Assert.Equal($"settlecore:payment:{state.Payment.Id.Value:N}:create", state.Request.IdempotencyKey);
        Assert.Equal(state.Result.ProviderReference, state.Attached);
        Assert.Equal(cancellation.Token, state.Token);
        Assert.Equal(PaymentStatus.Pending, state.Payment.Status);
    }

    [Fact]
    public async Task MissingPaymentReturnsNullWithoutCallingProvider()
    {
        var state = new State { Payment = null };
        Assert.Null(await state.Handler.HandleAsync(Guid.NewGuid()));
        Assert.Null(state.Request);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("identity")]
    public async Task MissingOrMismatchedPreparationPreventsProviderCall(string mismatch)
    {
        var state = new State();
        state.Preparation = mismatch switch
        {
            "missing" => null,
            "amount" => state.Preparation! with { GrossAmountMinorUnits = 1235 },
            "currency" => state.Preparation! with { Currency = "USD" },
            _ => state.Preparation! with { PaymentId = Guid.NewGuid() }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => state.Handler.HandleAsync(state.Payment!.Id.Value));
        Assert.Null(state.Request);
        Assert.Null(state.Attached);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedOrAlreadyLinkedPaymentPreventsCreation(bool linked)
    {
        var state = new State();
        if (linked) state.Payment!.AttachProviderReference(state.Result.ProviderReference);
        else state.Payment!.MarkSucceeded();
        await Assert.ThrowsAsync<InvalidOperationException>(() => state.Handler.HandleAsync(state.Payment.Id.Value));
        Assert.Null(state.Request);
    }

    [Fact]
    public async Task ProviderFailureDoesNotAttachReferenceOrCompletePayment()
    {
        var state = new State { FailProvider = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => state.Handler.HandleAsync(state.Payment!.Id.Value));
        Assert.Null(state.Attached);
        Assert.Equal(PaymentStatus.Pending, state.Payment!.Status);
    }

    [Fact]
    public async Task ReferencePersistenceFailureDoesNotReturnSuccessfulCreation()
    {
        var state = new State { AttachSucceeds = false };
        await Assert.ThrowsAsync<InvalidOperationException>(() => state.Handler.HandleAsync(state.Payment!.Id.Value));
        Assert.Equal(PaymentStatus.Pending, state.Payment!.Status);
    }

    [Theory]
    [InlineData(23)]
    [InlineData(24)]
    [InlineData(-1)]
    public async Task ExpiredOrFutureAttemptPreventsProviderCall(int ageHours)
    {
        var state = new State();
        state.FirstAttemptAt = state.Now.AddHours(-ageHours);
        await Assert.ThrowsAsync<InvalidOperationException>(() => state.Handler.HandleAsync(state.Payment!.Id.Value));
        Assert.Null(state.Request);
        Assert.Null(state.Attached);
    }

    [Fact]
    public async Task RetryInsideWindowMayReuseStableIdentity()
    {
        var state = new State();
        state.FirstAttemptAt = state.Now.AddHours(-22);
        Assert.Same(state.Result, await state.Handler.HandleAsync(state.Payment!.Id.Value));
        Assert.Equal(1, state.RecordedAttempts);
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class State : IPaymentRepository, IPaymentLedgerPostingPreparationRepository,
        IPaymentProvider, IPaymentProviderReferencePersistence, IPaymentProviderCreationAttempts
    {
        public State()
        {
            FirstAttemptAt = Now;
            Preparation = new(Payment!.Id.Value, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), Guid.NewGuid(), "SGD", 1234, 34);
        }
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset FirstAttemptAt { get; set; }
        public int RecordedAttempts { get; private set; }
        public Task<DateTimeOffset> GetOrRecordAsync(PaymentId id, CancellationToken cancellationToken = default)
        {
            RecordedAttempts++;
            return Task.FromResult(FirstAttemptAt);
        }
        public Payment? Payment { get; set; } = Payment.Create(12.34m, "SGD");
        public PaymentLedgerPostingRequest? Preparation { get; set; }
        public CreateProviderPaymentResult Result { get; } = new(ProviderPaymentReference.Create("test", "pi_test"),
            ProviderPaymentStatus.Succeeded, "client-secret");
        public CreateProviderPaymentRequest? Request { get; private set; }
        public ProviderPaymentReference? Attached { get; private set; }
        public CancellationToken Token { get; private set; }
        public bool FailProvider { get; set; }
        public bool AttachSucceeds { get; set; } = true;
        public CreateProviderPaymentHandler Handler => new(this, this, this, this, this, new Clock(Now), new(TimeSpan.FromHours(23)));
        public Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken cancellationToken = default) => Task.FromResult(Payment);
        public Task<PaymentLedgerPostingRequest?> GetByPaymentIdAsync(PaymentId id, CancellationToken cancellationToken = default) => Task.FromResult(Preparation);
        public Task<CreateProviderPaymentResult> CreatePaymentAsync(CreateProviderPaymentRequest request, CancellationToken cancellationToken = default)
        {
            Assert.True(RecordedAttempts > 0);
            Request = request;
            Token = cancellationToken;
            if (FailProvider) throw new InvalidOperationException("Provider unavailable.");
            return Task.FromResult(Result);
        }
        public Task<bool> AttachAsync(PaymentId id, ProviderPaymentReference reference, CancellationToken cancellationToken = default)
        {
            Attached = reference;
            Token = cancellationToken;
            return Task.FromResult(AttachSucceeds);
        }
        public Task AddAsync(Payment payment, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Payment?> GetByProviderReferenceAsync(ProviderPaymentReference reference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveAsync(PaymentLedgerPostingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
