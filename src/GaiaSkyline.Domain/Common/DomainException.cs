namespace GaiaSkyline.Domain.Common;

/// <summary>
/// Base type for exceptions that represent a broken domain invariant or rule.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException()
    {
    }

    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
