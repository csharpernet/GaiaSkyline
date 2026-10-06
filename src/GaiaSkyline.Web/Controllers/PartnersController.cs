using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Partners;
using GaiaSkyline.Web.Models;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The public side of the influencer program (Stage 8 Part A): the localized, indexable landing and
/// application pages (content-block-driven, rate-limited, honeypot-guarded) and the single-use onboarding
/// page behind the emailed invite link.
/// </summary>
public sealed class PartnersController(
    IContentService content,
    IPartnerApplyService applications,
    IPartnerOnboardingService onboarding) : PublicController
{
    [HttpGet("{lang:culture}/partners")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var partners = await content.GetSectionAsync("partners", CurrentCulture, cancellationToken);

        await SetMetaAsync(Meta(
            relativePath: "partners",
            title: $"Partner program — {BrandName}",
            description: partners.TextOr("partners.landing.headline", "Earn a share of every booking you drive."),
            breadcrumbs: [new Breadcrumb("Home", string.Empty), new Breadcrumb("Partners", null)]), cancellationToken);

        return View(new PartnersLandingViewModel(partners));
    }

    // The form carries a per-session antiforgery token, so it is never shared via the output cache.
    [HttpGet("{lang:culture}/partners/apply")]
    [Microsoft.AspNetCore.OutputCaching.OutputCache(NoStore = true)]
    public async Task<IActionResult> Apply(bool submitted, CancellationToken cancellationToken)
    {
        var partners = await content.GetSectionAsync("partners", CurrentCulture, cancellationToken);

        await SetMetaAsync(Meta(
            relativePath: "partners/apply",
            title: $"Apply — partner program — {BrandName}",
            description: partners.TextOr("partners.apply.title", "Apply to the Gaia Skyline partner program."),
            breadcrumbs:
            [
                new Breadcrumb("Home", string.Empty),
                new Breadcrumb("Partners", "partners"),
                new Breadcrumb("Apply", null),
            ]), cancellationToken);

        return View(new PartnerApplyViewModel(partners, new PartnerApplyForm(), submitted, Error: null));
    }

    [HttpPost("{lang:culture}/partners/apply")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("apply")]
    public async Task<IActionResult> Apply(PartnerApplyForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);

        // Honeypot: humans never see the field; a bot that filled it gets a quiet "success".
        if (!string.IsNullOrEmpty(form.Website))
        {
            return RedirectToAction(nameof(Apply), new { lang = CurrentSlug, submitted = true });
        }

        string? error = null;
        if (!form.Consent)
        {
            error = "Please tick the consent box so we may store your application.";
        }
        else
        {
            var result = await applications.SubmitAsync(
                new PartnerApplySubmission(
                    form.Name, form.Email, form.SocialLinks, form.AudienceSize, form.Niche, form.Message),
                cancellationToken);
            if (result.Ok)
            {
                return RedirectToAction(nameof(Apply), new { lang = CurrentSlug, submitted = true });
            }

            error = result.Error;
        }

        var partners = await content.GetSectionAsync("partners", CurrentCulture, cancellationToken);
        await SetMetaAsync(Meta(
            relativePath: "partners/apply",
            title: $"Apply — partner program — {BrandName}",
            description: partners.TextOr("partners.apply.title", "Apply to the Gaia Skyline partner program.")), cancellationToken);
        return View(new PartnerApplyViewModel(partners, form, Submitted: false, error));
    }

    // Token-personalised and form-bearing — never cached.
    [HttpGet("{lang:culture}/partners/join")]
    [Microsoft.AspNetCore.OutputCaching.OutputCache(NoStore = true)]
    public async Task<IActionResult> Join(string? token, CancellationToken cancellationToken)
    {
        var invite = string.IsNullOrWhiteSpace(token)
            ? null
            : await onboarding.PreviewAsync(token, cancellationToken);
        var partners = await content.GetSectionAsync("partners", CurrentCulture, cancellationToken);

        SetMeta(Meta(
            relativePath: "partners/join",
            title: $"Partner onboarding — {BrandName}",
            description: "Finish setting up your partner account.",
            noIndex: true));

        return View(new PartnerJoinViewModel(
            invite, token ?? string.Empty, partners.TextOr("partners.terms", string.Empty), Error: null, Completed: false));
    }

    [HttpPost("{lang:culture}/partners/join")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Join(PartnerJoinForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);

        var result = await onboarding.CompleteAsync(
            new PartnerOnboardingSubmission(
                form.Token, form.Password, form.AcceptTerms,
                form.Iban, form.AccountHolder, form.TaxId, form.Country),
            cancellationToken);

        SetMeta(Meta(
            relativePath: "partners/join",
            title: $"Partner onboarding — {BrandName}",
            description: "Finish setting up your partner account.",
            noIndex: true));

        if (result.Ok)
        {
            return View(new PartnerJoinViewModel(
                Invite: null, form.Token, TermsHtml: string.Empty, Error: null, Completed: true));
        }

        var invite = await onboarding.PreviewAsync(form.Token, cancellationToken);
        var partners = await content.GetSectionAsync("partners", CurrentCulture, cancellationToken);
        return View(new PartnerJoinViewModel(
            invite, form.Token, partners.TextOr("partners.terms", string.Empty), result.Error, Completed: false));
    }
}
