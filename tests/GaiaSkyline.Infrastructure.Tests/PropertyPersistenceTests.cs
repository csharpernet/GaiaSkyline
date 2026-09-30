using FluentAssertions;
using GaiaSkyline.Domain.Entities;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Tests;

public class PropertyPersistenceTests(SqlServerContainerFixture fixture)
    : IClassFixture<SqlServerContainerFixture>
{
    private readonly SqlServerContainerFixture _fixture = fixture;

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_fixture.ConnectionString!)
            .Options;
        return new AppDbContext(options);
    }

    private static Property NewProperty(string registrationCode) =>
        new(
            PropertyId.New(),
            "Gaia Skyline",
            registrationCode,
            "Rua do Ouro, Vila Nova de Gaia",
            41.13,
            -8.61,
            "EUR",
            "Europe/Lisbon",
            new TimeOnly(15, 0),
            new TimeOnly(11, 0));

    [SkippableFact]
    public async Task Migration_applies_and_property_round_trips_through_sql_server()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        var property = NewProperty("AL/ROUNDTRIP");

        await using (var write = CreateContext())
        {
            write.Properties.Add(property);
            await write.SaveChangesAsync();
        }

        await using (var read = CreateContext())
        {
            var loaded = await read.Properties.SingleAsync(p => p.Id == property.Id);

            loaded.Id.Should().Be(property.Id);
            loaded.Name.Should().Be("Gaia Skyline");
            loaded.RegistrationCode.Should().Be("AL/ROUNDTRIP");
            loaded.Address.Should().Be("Rua do Ouro, Vila Nova de Gaia");
            loaded.Lat.Should().Be(41.13);
            loaded.Lng.Should().Be(-8.61);
            loaded.DefaultCurrency.Should().Be("EUR");
            loaded.Timezone.Should().Be("Europe/Lisbon");
            loaded.CheckInFromLocal.Should().Be(new TimeOnly(15, 0));
            loaded.CheckOutByLocal.Should().Be(new TimeOnly(11, 0));
        }
    }

    [SkippableFact]
    public async Task Unique_registration_code_constraint_is_enforced()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        await using (var first = CreateContext())
        {
            first.Properties.Add(NewProperty("AL/DUPLICATE"));
            await first.SaveChangesAsync();
        }

        await using var second = CreateContext();
        second.Properties.Add(NewProperty("AL/DUPLICATE"));

        var act = async () => await second.SaveChangesAsync();

        // A real unique index rejects the duplicate — behaviour the in-memory provider would miss.
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
