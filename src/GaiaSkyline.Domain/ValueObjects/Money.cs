using GaiaSkyline.Domain.Common;

namespace GaiaSkyline.Domain.ValueObjects;

/// <summary>
/// An immutable monetary amount in a specific ISO 4217 currency.
/// Two <see cref="Money"/> values are equal only when both <see cref="Amount"/> and
/// <see cref="Currency"/> match. Arithmetic across different currencies is rejected.
/// </summary>
public readonly record struct Money
{
    public Money(decimal amount, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || !normalized.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException(
                $"'{currency}' is not a valid ISO 4217 alphabetic currency code.", nameof(currency));
        }

        Amount = amount;
        Currency = normalized;
    }

    /// <summary>The numeric amount. May be negative (e.g. a refund or adjustment).</summary>
    public decimal Amount { get; }

    /// <summary>The ISO 4217 alphabetic currency code, always upper-cased (e.g. "EUR").</summary>
    public string Currency { get; }

    /// <summary>A zero amount in the given currency.</summary>
    public static Money Zero(string currency) => new(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money value, decimal factor) =>
        new(value.Amount * factor, value.Currency);

    // Named alternates for the operators (CLS-friendly, satisfies CA2225).
    public static Money Add(Money left, Money right) => left + right;

    public static Money Subtract(Money left, Money right) => left - right;

    public static Money Multiply(Money value, decimal factor) => value * factor;

    public override string ToString() => $"{Amount} {Currency}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.Ordinal))
        {
            throw new CurrencyMismatchException(left.Currency, right.Currency);
        }
    }
}
