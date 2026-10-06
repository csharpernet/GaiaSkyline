using System.Net;
using FluentAssertions;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Partners;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaiaSkyline.Web.Tests;

/// <summary>
/// The referral middleware (Stage 8 Part A, ADR 0019): <c>?ref=CODE</c> on a public URL records a click,
/// sets the 30-day gs_ref cookie and 301s to the same URL without the parameter; unknown codes just strip.
/// </summary>
public sealed class PartnerRefMiddlewareTests(PublicSiteFactory factory) : IClassFixture<PublicSiteFactory>
{
    [Fact]
    public async Task Ref_sets_the_cookie_records_the_click_and_301s_to_the_clean_url()
    {
        var code = $"REF{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        Guid partnerId;
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var partner = new Partner(
                PartnerId.New(), PartnerApplicationId.New(), "Ref Partner",
                $"{Guid.NewGuid():N}@partner.example", 5, 10, DateTime.UtcNow);
            partner.AcceptTerms("test", DateTime.UtcNow);
            partner.SetPayoutDetails("PT50000201231234567890154", "Ref Partner", "123456789", "PT");
            partner.Activate(Guid.NewGuid(), DateTime.UtcNow);
            var promo = new PromoCode(PromoCodeId.New(), code, 5, isActive: true, partnerId: partner.Id);
            partner.SetPromoCode(promo.Id);
            ctx.Partners.Add(partner);
            ctx.PromoCodes.Add(promo);
            await ctx.SaveChangesAsync();
            partnerId = partner.Id.Value;
        }

        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(new Uri($"/en/gallery?ref={code}&utm_source=x", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently, "the ref parameter must never be indexable");
        response.Headers.Location!.ToString().Should().Be("/en/gallery?utm_source=x", "only ref is stripped");
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToList() : [];
        cookies.Should().Contain(c => c.StartsWith("gs_ref=") && c.Contains(code) && c.Contains("samesite=lax"));

        using var scope2 = factory.Services.CreateScope();
        var verify = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PartnerId.From(partnerId);
        var click = await verify.PartnerClicks.AsNoTracking().SingleAsync(c => c.PartnerId == id);
        click.LandingPath.Should().Be("/en/gallery");
    }

    [Fact]
    public async Task An_unknown_code_still_strips_but_sets_no_cookie_and_records_nothing()
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(new Uri("/en?ref=NOPE123", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.ToString().Should().Be("/en");
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToList() : [];
        cookies.Should().NotContain(c => c.StartsWith("gs_ref="));
    }
}
