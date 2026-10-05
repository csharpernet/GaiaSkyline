using GaiaSkyline.Domain.Partners;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class PartnerApplicationConfiguration : IEntityTypeConfiguration<PartnerApplication>
{
    public void Configure(EntityTypeBuilder<PartnerApplication> builder)
    {
        builder.ToTable("PartnerApplications");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Email).HasMaxLength(320).IsRequired();
        builder.Property(a => a.SocialLinks).HasMaxLength(1000);
        builder.Property(a => a.Niche).HasMaxLength(200);
        builder.Property(a => a.Message).HasMaxLength(4000);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.DecisionNote).HasMaxLength(1000);

        builder.HasIndex(a => new { a.Status, a.SubmittedAtUtc });
    }
}
