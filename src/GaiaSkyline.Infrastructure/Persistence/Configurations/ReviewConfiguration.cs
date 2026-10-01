using GaiaSkyline.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Rating).IsRequired();
        builder.Property(r => r.GuestFirstName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.GuestLocation).HasMaxLength(200);
        builder.Property(r => r.Body).IsRequired(); // nvarchar(max)
        builder.Property(r => r.Source).HasMaxLength(50).IsRequired();
        builder.Property(r => r.StayedOn).IsRequired();
        builder.Property(r => r.IsPublished).IsRequired();
    }
}
