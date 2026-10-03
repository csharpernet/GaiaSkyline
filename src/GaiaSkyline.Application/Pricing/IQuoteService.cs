namespace GaiaSkyline.Application.Pricing;

/// <summary>Loads the current pricing context and returns a quote. Used by the quote API and booking creation.</summary>
public interface IQuoteService
{
    Task<QuoteBreakdown> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken);
}
