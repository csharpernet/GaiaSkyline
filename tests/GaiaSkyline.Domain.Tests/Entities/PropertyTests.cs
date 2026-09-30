using FluentAssertions;
using GaiaSkyline.Domain.Entities;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Tests.Entities;

public class PropertyTests
{
    private static Property CreateValid(
        string? name = "Gaia Skyline",
        string? registrationCode = "AL/12345",
        string? address = "Rua do Ouro, Vila Nova de Gaia",
        double lat = 41.13,
        double lng = -8.61,
        string? currency = "eur",
        string? timezone = "Europe/Lisbon") =>
        new(
            PropertyId.New(),
            name!,
            registrationCode!,
            address!,
            lat,
            lng,
            currency!,
            timezone!,
            new TimeOnly(15, 0),
            new TimeOnly(11, 0));

    [Fact]
    public void Valid_property_is_constructed_and_trimmed()
    {
        var id = PropertyId.New();

        var property = new Property(
            id,
            "  Gaia Skyline  ",
            " AL/12345 ",
            " Rua do Ouro ",
            41.13,
            -8.61,
            "eur",
            " Europe/Lisbon ",
            new TimeOnly(15, 0),
            new TimeOnly(11, 0));

        property.Id.Should().Be(id);
        property.Name.Should().Be("Gaia Skyline");
        property.RegistrationCode.Should().Be("AL/12345");
        property.Address.Should().Be("Rua do Ouro");
        property.DefaultCurrency.Should().Be("EUR");
        property.Timezone.Should().Be("Europe/Lisbon");
        property.CheckInFromLocal.Should().Be(new TimeOnly(15, 0));
        property.CheckOutByLocal.Should().Be(new TimeOnly(11, 0));
        property.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_name(string? name)
    {
        var act = () => CreateValid(name: name);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Rejects_blank_registration_code(string? code)
    {
        var act = () => CreateValid(registrationCode: code);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Rejects_blank_address(string? address)
    {
        var act = () => CreateValid(address: address);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-90.1)]
    [InlineData(90.1)]
    public void Rejects_out_of_range_latitude(double lat)
    {
        var act = () => CreateValid(lat: lat);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-180.1)]
    [InlineData(180.1)]
    public void Rejects_out_of_range_longitude(double lng)
    {
        var act = () => CreateValid(lng: lng);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    public void Rejects_invalid_currency(string currency)
    {
        var act = () => CreateValid(currency: currency);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Rejects_blank_timezone(string? timezone)
    {
        var act = () => CreateValid(timezone: timezone);

        act.Should().Throw<ArgumentException>();
    }
}
