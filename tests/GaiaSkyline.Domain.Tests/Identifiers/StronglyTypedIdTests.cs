using FluentAssertions;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Tests.Identifiers;

public class StronglyTypedIdTests
{
    [Fact]
    public void New_produces_distinct_non_empty_ids()
    {
        var a = PropertyId.New();
        var b = PropertyId.New();

        a.Value.Should().NotBe(Guid.Empty);
        a.Should().NotBe(b);
    }

    [Fact]
    public void From_wraps_an_existing_guid()
    {
        var guid = Guid.NewGuid();

        PropertyId.From(guid).Value.Should().Be(guid);
    }

    [Fact]
    public void Equality_is_by_value()
    {
        var guid = Guid.NewGuid();

        PropertyId.From(guid).Should().Be(PropertyId.From(guid));
    }

    [Fact]
    public void ToString_returns_the_underlying_guid()
    {
        var guid = Guid.NewGuid();

        PropertyId.From(guid).ToString().Should().Be(guid.ToString());
    }

    [Fact]
    public void Distinct_id_types_are_independent()
    {
        var guid = Guid.NewGuid();

        // Same backing guid, different identity types — a compile-time safety net.
        BookingId.From(guid).Value.Should().Be(PartnerId.From(guid).Value);
        BookingId.From(guid).GetType().Should().NotBe(PartnerId.From(guid).GetType());
    }
}
