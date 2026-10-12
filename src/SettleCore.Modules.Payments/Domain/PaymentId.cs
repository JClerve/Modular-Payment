namespace SettleCore.Modules.Payments.Domain;

public readonly record struct PaymentId
{
    private PaymentId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static PaymentId Empty => default;

    public static PaymentId New()
    {
        return new PaymentId(Guid.NewGuid());
    }

    public static PaymentId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Payment ID must not be empty.",
                nameof(value));
        }

        return new PaymentId(value);
    }
}
