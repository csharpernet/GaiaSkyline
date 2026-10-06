using System.Globalization;
using System.Security.Cryptography;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Partners;
using GaiaSkyline.Application.Settings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Identity;
using GaiaSkyline.Domain.Partners;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Infrastructure.Identity;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Partners;

/// <summary>
/// Partner onboarding (Stage 8 Part A). Approving an application creates the partner with the program
/// defaults and a FIRSTNAME+2-digits promo code, and emails a 7-day single-use invite link (the same
/// DataProtection-wrapped-row-id pattern as guest magic links). Completing the link creates the Partner-role
/// Identity user (password through the normal validators, HIBP included), records the accepted terms
/// version (the terms content block's update stamp) and payout details, and activates the code.
/// </summary>
internal sealed class PartnerOnboardingService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IDataProtectionProvider dataProtectionProvider,
    IEmailSender emailSender,
    ISiteSettings settings,
    IOptions<EmailOptions> emailOptions,
    TimeProvider clock) : IPartnerOnboardingService
{
    private const string TermsBlockKey = "partners.terms";

    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("GaiaSkyline.PartnerInvite.v1");

    public async Task<PartnerInviteResult> InviteAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var appId = PartnerApplicationId.From(applicationId);
        var application = await dbContext.PartnerApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == appId, cancellationToken);
        if (application is null)
        {
            return PartnerInviteResult.Fail("Application not found.");
        }

        if (application.Status != PartnerApplicationStatus.Approved)
        {
            return PartnerInviteResult.Fail("Only an approved application can be invited.");
        }

        var existing = await dbContext.Partners.FirstOrDefaultAsync(p => p.ApplicationId == appId, cancellationToken);
        if (existing is not null)
        {
            return existing.Status == PartnerStatus.Invited
                ? await ResendInviteAsync(existing.Id.Value, cancellationToken)
                : PartnerInviteResult.Fail("This applicant is already onboarded.");
        }

        var email = application.Email.Trim().ToLowerInvariant();
        if (await dbContext.Partners.AnyAsync(p => p.Email == email, cancellationToken))
        {
            return PartnerInviteResult.Fail("A partner with this email already exists.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var partner = new Partner(
            PartnerId.New(), appId, application.Name, application.Email,
            ReadIntSetting(SettingKeys.PartnerDefaultDiscountPct, 5),
            ReadIntSetting(SettingKeys.PartnerDefaultCommissionPct, 10),
            now);

        // FIRSTNAME + 2 digits, uppercase, unique across all promo codes; inactive until activation.
        var code = await GenerateCodeAsync(application.Name, cancellationToken);
        var promo = new PromoCode(
            PromoCodeId.New(), code, partner.GuestDiscountPct, isActive: false, partnerId: partner.Id);
        partner.SetPromoCode(promo.Id);

        dbContext.Partners.Add(partner);
        dbContext.PromoCodes.Add(promo);
        var invite = new PartnerInvite(PartnerInviteId.New(), partner.Id, now);
        dbContext.PartnerInvites.Add(invite);
        await dbContext.SaveChangesAsync(cancellationToken);

        await SendInviteEmailAsync(partner, invite, cancellationToken);
        return PartnerInviteResult.Success(partner.Id.Value);
    }

    public async Task<PartnerInviteResult> ResendInviteAsync(Guid partnerId, CancellationToken cancellationToken)
    {
        var id = PartnerId.From(partnerId);
        var partner = await dbContext.Partners.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (partner is null)
        {
            return PartnerInviteResult.Fail("Partner not found.");
        }

        if (partner.Status != PartnerStatus.Invited)
        {
            return PartnerInviteResult.Fail("This partner has already completed onboarding.");
        }

        var invite = new PartnerInvite(PartnerInviteId.New(), partner.Id, clock.GetUtcNow().UtcDateTime);
        dbContext.PartnerInvites.Add(invite);
        await dbContext.SaveChangesAsync(cancellationToken);

        await SendInviteEmailAsync(partner, invite, cancellationToken);
        return PartnerInviteResult.Success(partner.Id.Value);
    }

    public async Task<PartnerInvitePreview?> PreviewAsync(string token, CancellationToken cancellationToken)
    {
        var invite = await FindUsableInviteAsync(token, cancellationToken);
        if (invite is null)
        {
            return null;
        }

        var invitePartnerId = invite.PartnerId;
        var partner = await dbContext.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == invitePartnerId, cancellationToken);
        if (partner is null || partner.Status != PartnerStatus.Invited)
        {
            return null;
        }

        var code = await PromoCodeOfAsync(partner, cancellationToken);
        return new PartnerInvitePreview(partner.Id.Value, partner.Name, partner.Email, code);
    }

    public async Task<PartnerOnboardingResult> CompleteAsync(
        PartnerOnboardingSubmission submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (!submission.TermsAccepted)
        {
            return PartnerOnboardingResult.Fail("Please accept the partner terms to continue.");
        }

        var invite = await FindUsableInviteAsync(submission.Token, cancellationToken);
        if (invite is null)
        {
            return PartnerOnboardingResult.Fail("This invite link has expired or was already used — ask for a new one.");
        }

        var invitePartnerId = invite.PartnerId;
        var partner = await dbContext.Partners.FirstOrDefaultAsync(p => p.Id == invitePartnerId, cancellationToken);
        if (partner is null || partner.Status != PartnerStatus.Invited)
        {
            return PartnerOnboardingResult.Fail("This invite is no longer valid.");
        }

        try
        {
            partner.SetPayoutDetails(
                submission.Iban, submission.AccountHolder, submission.TaxId, submission.Country);
        }
        catch (ArgumentException ex)
        {
            return PartnerOnboardingResult.Fail(ex.Message);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        partner.AcceptTerms(await TermsVersionAsync(cancellationToken), now);

        // The invite arrived at the partner's mailbox, which is the email proof — the account starts
        // confirmed. The password runs through the normal validators (length 12 + HIBP).
        var user = new ApplicationUser
        {
            UserName = partner.Email,
            Email = partner.Email,
            EmailConfirmed = true,
            PreferredLanguage = "en",
            CreatedAtUtc = now,
        };
        var createResult = await userManager.CreateAsync(user, submission.Password);
        if (!createResult.Succeeded)
        {
            return PartnerOnboardingResult.Fail(string.Join(" ", createResult.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, UserRoles.Partner);

        partner.Activate(user.Id, now);
        invite.Consume(now);

        // The code starts discounting only now that the partner is live.
        var promoId = partner.PromoCodeId!.Value;
        var promo = await dbContext.PromoCodes.FirstAsync(p => p.Id == promoId, cancellationToken);
        promo.Update(partner.GuestDiscountPct, isActive: true, promo.ValidFrom, promo.ValidUntil);

        await dbContext.SaveChangesAsync(cancellationToken);
        return PartnerOnboardingResult.Success();
    }

    /// <summary>Wraps an invite row id as the emailed token (tamper-proof, reveals nothing).</summary>
    internal string ProtectToken(PartnerInviteId id) => _protector.Protect(id.Value.ToString());

    private async Task<PartnerInvite?> FindUsableInviteAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        Guid id;
        try
        {
            id = Guid.Parse(_protector.Unprotect(token));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }

        var inviteId = PartnerInviteId.From(id);
        var invite = await dbContext.PartnerInvites.FirstOrDefaultAsync(i => i.Id == inviteId, cancellationToken);
        return invite is not null && invite.IsUsable(clock.GetUtcNow().UtcDateTime) ? invite : null;
    }

    private async Task SendInviteEmailAsync(Partner partner, PartnerInvite invite, CancellationToken cancellationToken)
    {
        var token = ProtectToken(invite.Id);
        var baseUrl = emailOptions.Value.SiteBaseUrl.TrimEnd('/');
        var link = $"{baseUrl}/en/partners/join?token={Uri.EscapeDataString(token)}";
        var code = await PromoCodeOfAsync(partner, cancellationToken);

        await emailSender.SendAsync(
            new EmailMessage(
                partner.Email,
                partner.Name,
                "You're in — set up your Gaia Skyline partner account",
                $"<p>Olá {partner.Name},</p>"
                + "<p>Your application to the Gaia Skyline partner program was approved. Your personal promo "
                + $"code is <strong>{code}</strong> — it gives your audience {partner.GuestDiscountPct}% off "
                + $"and earns you {partner.CommissionPct}% of every booking you drive.</p>"
                + $"<p><a href=\"{link}\">Finish setting up your account</a> (the link works once and expires "
                + "in 7 days): choose a password, accept the partner terms and add your payout details.</p>"
                + "<p>See you on the Douro!</p>"),
            cancellationToken);
    }

    private async Task<string> PromoCodeOfAsync(Partner partner, CancellationToken cancellationToken)
    {
        if (partner.PromoCodeId is not { } promoId)
        {
            return string.Empty;
        }

        return await dbContext.PromoCodes.AsNoTracking()
            .Where(p => p.Id == promoId)
            .Select(p => p.Code)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
    }

    // FIRSTNAME (ascii letters of the first word, capped at 12) + 2 digits, unique across promo codes.
    private async Task<string> GenerateCodeAsync(string name, CancellationToken cancellationToken)
    {
        var first = new string(name.Trim().Split(' ')[0]
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(char.IsAsciiLetter)
            .ToArray()).ToUpperInvariant();
        if (first.Length == 0)
        {
            first = "PARTNER";
        }
        else if (first.Length > 12)
        {
            first = first[..12];
        }

        var taken = await dbContext.PromoCodes.AsNoTracking()
            .Where(p => p.Code.StartsWith(first))
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);
        var takenSet = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var attempt = 0; attempt < 100; attempt++)
        {
            var candidate = $"{first}{RandomNumberGenerator.GetInt32(10, 100)}";
            if (!takenSet.Contains(candidate))
            {
                return candidate;
            }
        }

        // All 90 two-digit suffixes taken for this name (unrealistic) — widen deterministically.
        return $"{first}{RandomNumberGenerator.GetInt32(100, 1000)}";
    }

    private async Task<string> TermsVersionAsync(CancellationToken cancellationToken)
    {
        // The accepted version is the terms content block's English update stamp — editing the terms in the
        // admin automatically produces a new version for future acceptances.
        var stamp = await (
            from b in dbContext.ContentBlocks
            where b.Key == TermsBlockKey
            from t in b.Translations
            where t.LanguageCode == "en"
            select (DateTime?)t.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return (stamp ?? clock.GetUtcNow().UtcDateTime).ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
    }

    private int ReadIntSetting(string key, int fallback)
    {
        var raw = settings.Get(key);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value is >= 0 and <= 100
            ? value
            : fallback;
    }
}
