using GaiaSkyline.Domain.Entities;
using GaiaSkyline.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties");

        builder.HasKey(p => p.Id);

        // Strongly-typed id <-> Guid. The value is assigned in the domain, never by the store.
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => PropertyId.From(value))
            .ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.RegistrationCode).HasMaxLength(64).IsRequired();
        builder.Property(p => p.Address).HasMaxLength(400).IsRequired();
        builder.Property(p => p.Lat).IsRequired();
        builder.Property(p => p.Lng).IsRequired();
        builder.Property(p => p.DefaultCurrency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(p => p.Timezone).HasMaxLength(64).IsRequired();
        builder.Property(p => p.CheckInFromLocal).IsRequired();
        builder.Property(p => p.CheckOutByLocal).IsRequired();

        // The registration (AL) code is a real-world unique licence identifier.
        builder.HasIndex(p => p.RegistrationCode).IsUnique();

        // Domain events are behaviour, not persisted state.
        builder.Ignore(p => p.DomainEvents);
    }
}
