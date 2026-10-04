using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Web.Localization;

namespace GaiaSkyline.Web.Tests;

/// <summary>
/// The content layer keeps its own language list (<see cref="ContentLanguages"/>) so it does not depend on
/// the web layer, but the two must describe the same five languages. This guards against drift.
/// </summary>
public class ContentLanguagesTests
{
    [Fact]
    public void Content_languages_match_the_web_supported_cultures()
    {
        ContentLanguages.All.Should().Equal(SupportedCultures.AllCultures);
    }

    [Fact]
    public void English_is_the_default_content_language()
    {
        ContentLanguages.Default.Should().Be(SupportedCultures.DefaultCulture);
    }
}
