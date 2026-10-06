using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.ReferenceCode).HasMaxLength(16).IsRequired();
        builder.HasIndex(b => b.ReferenceCode).IsUnique();

        builder.Property(b => b.CheckIn).IsRequired();
        builder.Property(b => b.CheckOut).IsRequired();
        builder.Property(b => b.Adults).IsRequired();
        builder.Property(b => b.Children).IsRequired();
        builder.Property(b => b.Infants).IsRequired();

        builder.Property(b => b.GuestName).HasMaxLength(200).IsRequired();
        builder.Property(b => b.GuestEmail).HasMaxLength(256).IsRequired();
        builder.Property(b => b.GuestPhone).HasMaxLength(40).IsRequired();
        builder.Property(b => b.GuestCountry).HasMaxLength(100).IsRequired();
        builder.Property(b => b.GuestLanguage).HasMaxLength(16).IsRequired();
        builder.Property(b => b.GuestUserId);

        // Money columns persist as bigint cents via the global MoneyToCentsConverter.
        builder.Property(b => b.NightlyRateSnapshot).IsRequired();
        builder.Property(b => b.Subtotal).IsRequired();
        builder.Property(b => b.DiscountAmount).IsRequired();
        builder.Property(b => b.CleaningFee).IsRequired();
        builder.Property(b => b.TouristTax).IsRequired();
        builder.Property(b => b.Total).IsRequired();
        builder.Property(b => b.RefundedAmount).IsRequired();

        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(b => b.StripeCustomerId).HasMaxLength(255);
        builder.Property(b => b.StripePaymentIntentId).HasMaxLength(255);
        builder.Property(b => b.PaymentMethodType).HasMaxLength(50);
        builder.Property(b => b.MultibancoEntity).HasMaxLength(16);
        builder.Property(b => b.MultibancoReference).HasMaxLength(32);
        builder.Property(b => b.PaymentExpiresAtUtc);

        builder.Property(b => b.ArrivalEstimateLocal);
        builder.Property(b => b.SpecialRequests).HasMaxLength(2000);
        builder.Property(b => b.AccountCreationRequested).IsRequired();

        builder.Property(b => b.CreatedAtUtc).IsRequired();
        builder.Property(b => b.ConfirmedAtUtc);
        builder.Property(b => b.CancelledAtUtc);
        builder.Property(b => b.CancellationReason).HasMaxLength(500);
        builder.Property(b => b.Notes).HasMaxLength(2000);
        builder.Property(b => b.ExternalChannelSyncedAtUtc);
        builder.Property(b => b.ExternalChannelSyncNote).HasMaxLength(500);

        builder.HasOne<PromoCode>()
            .WithMany()
            .HasForeignKey(b => b.PromoCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        // The expiry job scans by status; webhooks look up by PaymentIntent.
        builder.HasIndex(b => b.Status);
        builder.HasIndex(b => b.StripePaymentIntentId);
    }
}
