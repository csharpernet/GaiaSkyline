using GaiaSkyline.Domain.Partners;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

// Stage 8 Part A — the influencer program tables (partners, invites, clicks, attribution, commissions,
// payouts). Money columns persist as bigint cents via the global MoneyToCentsConverter.

internal sealed class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.ToTable("Partners");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.ApplicationId).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Email).HasMaxLength(320).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.TermsVersion).HasMaxLength(100);
        builder.Property(p => p.PayoutIban).HasMaxLength(34);
        builder.Property(p => p.PayoutAccountHolder).HasMaxLength(200);
        builder.Property(p => p.PayoutTaxId).HasMaxLength(50);
        builder.Property(p => p.PayoutCountry).HasMaxLength(2);

        // One partner per application and per contact email; UserId is unique once linked.
        builder.HasIndex(p => p.ApplicationId).IsUnique();
        builder.HasIndex(p => p.Email).IsUnique();
        builder.HasIndex(p => p.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
    }
}

internal sealed class PartnerInviteConfiguration : IEntityTypeConfiguration<PartnerInvite>
{
    public void Configure(EntityTypeBuilder<PartnerInvite> builder)
    {
        builder.ToTable("PartnerInvites");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.PartnerId).IsRequired();

        builder.HasIndex(i => i.PartnerId);
    }
}

internal sealed class PartnerClickConfiguration : IEntityTypeConfiguration<PartnerClick>
{
    public void Configure(EntityTypeBuilder<PartnerClick> builder)
    {
        builder.ToTable("PartnerClicks");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.PartnerId).IsRequired();
        builder.Property(c => c.LandingPath).HasMaxLength(400).IsRequired();

        builder.HasIndex(c => new { c.PartnerId, c.UtcAt });
    }
}

internal sealed class PartnerAttributionConfiguration : IEntityTypeConfiguration<PartnerAttribution>
{
    public void Configure(EntityTypeBuilder<PartnerAttribution> builder)
    {
        builder.ToTable("PartnerAttributions");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.PartnerId).IsRequired();
        builder.Property(a => a.BookingId).IsRequired();
        builder.Property(a => a.Source).HasConversion<string>().HasMaxLength(10).IsRequired();

        // A booking is attributed to at most one partner (ADR 0019).
        builder.HasIndex(a => a.BookingId).IsUnique();
        builder.HasIndex(a => a.PartnerId);
    }
}

internal sealed class CommissionConfiguration : IEntityTypeConfiguration<Commission>
{
    public void Configure(EntityTypeBuilder<Commission> builder)
    {
        builder.ToTable("Commissions");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.PartnerId).IsRequired();
        builder.Property(c => c.BookingId).IsRequired();
        builder.Property(c => c.BasisAmount).IsRequired();
        builder.Property(c => c.Amount).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(10).IsRequired();

        // One commission per booking; the lifecycle job and payout runs filter by partner + status.
        builder.HasIndex(c => c.BookingId).IsUnique();
        builder.HasIndex(c => new { c.PartnerId, c.Status });
        builder.HasIndex(c => c.PayoutId);
    }
}

internal sealed class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.ToTable("Payouts");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.PartnerId).IsRequired();
        builder.Property(p => p.PeriodLabel).HasMaxLength(10).IsRequired();
        builder.Property(p => p.Amount).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(10).IsRequired();

        // One payout per partner per period (the monthly run is idempotent).
        builder.HasIndex(p => new { p.PartnerId, p.PeriodLabel }).IsUnique();
    }
}
