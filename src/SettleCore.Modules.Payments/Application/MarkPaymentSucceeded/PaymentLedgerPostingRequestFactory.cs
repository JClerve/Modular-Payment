using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;

public static class PaymentLedgerPostingRequestFactory
{
    public static PaymentLedgerPostingRequest Create(
        Payment payment,
        PaymentLedgerPostingInput input)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(input);

        ValidateTransactionId(input.TransactionId);
        ValidateLedgerId(input.LedgerId);
        ValidateProcessorReceivableAccountId(
            input.ProcessorReceivableAccountId);
        ValidateMerchantPayableAccountId(
            input.MerchantPayableAccountId);
        ValidatePlatformRevenueAccountId(
            input.PlatformRevenueAccountId);

        var grossAmountMinorUnits =
            PaymentAmountConversion.ToMinorUnits(
                payment.Amount,
                payment.Currency);

        ValidateFeeAmountMinorUnits(
            input.FeeAmountMinorUnits,
            grossAmountMinorUnits);

        return new PaymentLedgerPostingRequest(
            PaymentId: payment.Id.Value,
            TransactionId: input.TransactionId,
            LedgerId: input.LedgerId,
            ProcessorReceivableAccountId:
                input.ProcessorReceivableAccountId,
            MerchantPayableAccountId:
                input.MerchantPayableAccountId,
            PlatformRevenueAccountId:
                input.PlatformRevenueAccountId,
            Currency: payment.Currency,
            GrossAmountMinorUnits:
                grossAmountMinorUnits,
            FeeAmountMinorUnits:
                input.FeeAmountMinorUnits);
    }

    private static void ValidateTransactionId(
        Guid transactionId)
    {
        if (transactionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Transaction ID must not be empty.",
                nameof(transactionId));
        }
    }

    private static void ValidateLedgerId(
        Guid ledgerId)
    {
        if (ledgerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Ledger ID must not be empty.",
                nameof(ledgerId));
        }
    }

    private static void ValidateProcessorReceivableAccountId(
        Guid processorReceivableAccountId)
    {
        if (processorReceivableAccountId == Guid.Empty)
        {
            throw new ArgumentException(
                "Processor receivable account ID must not be empty.",
                nameof(processorReceivableAccountId));
        }
    }

    private static void ValidateMerchantPayableAccountId(
        Guid merchantPayableAccountId)
    {
        if (merchantPayableAccountId == Guid.Empty)
        {
            throw new ArgumentException(
                "Merchant payable account ID must not be empty.",
                nameof(merchantPayableAccountId));
        }
    }

    private static void ValidatePlatformRevenueAccountId(
        Guid platformRevenueAccountId)
    {
        if (platformRevenueAccountId == Guid.Empty)
        {
            throw new ArgumentException(
                "Platform revenue account ID must not be empty.",
                nameof(platformRevenueAccountId));
        }
    }

    private static void ValidateFeeAmountMinorUnits(
        long feeAmountMinorUnits,
        long grossAmountMinorUnits)
    {
        if (feeAmountMinorUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(feeAmountMinorUnits),
                "Fee amount must be positive.");
        }

        if (feeAmountMinorUnits >= grossAmountMinorUnits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(feeAmountMinorUnits),
                "Fee amount must be less than gross amount.");
        }
    }
}