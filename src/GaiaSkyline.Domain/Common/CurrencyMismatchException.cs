namespace GaiaSkyline.Domain.Common;

/// <summary>
/// Thrown when an arithmetic operation is attempted on two
/// <see cref="ValueObjects.Money"/> values that are denominated in different currencies.
/// </summary>
public sealed class CurrencyMismatchException : DomainException
{
    public CurrencyMismatchException()
    {
    }

    public CurrencyMismatchException(string message)
        : base(message)
    {
    }

    public CurrencyMismatchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public CurrencyMismatchException(string leftCurrency, string rightCurrency)
        : base($"Cannot operate on Money values with different currencies: '{leftCurrency}' and '{rightCurrency}'.")
    {
        LeftCurrency = leftCurrency;
        RightCurrency = rightCurrency;
    }

    /// <summary>The currency of the left operand, when known.</summary>
    public string? LeftCurrency { get; }

    /// <summary>The currency of the right operand, when known.</summary>
    public string? RightCurrency { get; }
}
