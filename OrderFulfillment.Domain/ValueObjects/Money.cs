using OrderFulfillment.Domain.Common;

namespace OrderFulfillment.Domain.ValueObjects;

public class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency must be specified.", nameof(currency));

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    protected Money() { Currency = "USD"; }

    public static Money Zero(string currency = "USD") => new Money(0, currency);

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot add {other.Currency} to {Currency}");

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot subtract {other.Currency} from {Currency}");

        if (Amount < other.Amount)
            throw new InvalidOperationException("Resulting money amount cannot be negative.");

        return new Money(Amount - other.Amount, Currency);
    }

    public Money ApplyDiscount(decimal percentage)
    {
        if (percentage < 0 || percentage > 100)
            throw new ArgumentException("Discount percentage must be between 0 and 100.", nameof(percentage));

        decimal discountAmount = Amount * (percentage / 100m);
        return new Money(Amount - discountAmount, Currency);
    }

    public static Money operator +(Money left, Money right) => left.Add(right);
    public static Money operator -(Money left, Money right) => left.Subtract(right);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
