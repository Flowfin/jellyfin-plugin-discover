using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// Shows that each reader in <see cref="ConfigurationPageReader"/> finds the
/// thing it looks for.
/// </summary>
/// <remarks>
/// The assertions in <see cref="ConfigurationPageTests"/> are all of the form
/// "the page names none of these". A reader that always came back empty would
/// satisfy every one of them on any page at all, including one carrying a
/// foreign identifier and a script fetching from somewhere else, so the
/// assertions are worth no more than the proof that the readers bite. Each
/// fixture below is the near miss rather than a caricature: markup somebody
/// would actually write.
/// </remarks>
public static class ConfigurationPageReaderTests
{
    /// <summary>
    /// An identifier belonging to something else is found, which is the case
    /// the page assertion exists for: a page copied from another plugin still
    /// addressing that plugin.
    /// </summary>
    [Fact]
    public static void TheIdentifierReaderFindsAnIdentifierThePageNames()
    {
        const string Markup = """
            <script>
              var pluginUniqueId = "f7f0c6b1-2d7c-4a8e-9b4e-0d1a2b3c4d5e";
            </script>
            """;

        Assert.Equal(["f7f0c6b1-2d7c-4a8e-9b4e-0d1a2b3c4d5e"], ConfigurationPageReader.IdentifiersIn(Markup));
    }

    /// <summary>
    /// A control is found by its id and by its name, since a page using the
    /// other spelling would otherwise read as a page with no controls.
    /// </summary>
    /// <param name="markup">Markup carrying one control called RefreshHour.</param>
    [Theory]
    [InlineData("<input id=\"RefreshHour\" type=\"number\" />")]
    [InlineData("<input name=\"RefreshHour\" type=\"number\" />")]
    [InlineData("<select id=\"RefreshHour\"><option>1</option></select>")]
    [InlineData("<textarea id=\"RefreshHour\"></textarea>")]
    public static void TheControlReaderFindsAControlThePageOffers(string markup)
    {
        Assert.Equal(["RefreshHour"], ConfigurationPageReader.NamedControlsIn(markup));
    }

    /// <summary>
    /// A div is not a control. A reader that took every id on the page would
    /// report the page's own layout as settings and the two assertions about
    /// controls would then be about noise.
    /// </summary>
    [Fact]
    public static void TheControlReaderIgnoresAnElementThatIsNotAControl()
    {
        const string Markup = "<div id=\"PluginConfigPage\" data-role=\"page\"></div>";

        Assert.Empty(ConfigurationPageReader.NamedControlsIn(Markup));
    }

    /// <summary>
    /// A host outside the server is found, whether it is named with a scheme or
    /// left protocol-relative.
    /// </summary>
    /// <param name="markup">Markup reaching for a host that is not the server.</param>
    [Theory]
    [InlineData("<script src=\"https://cdn.example.invalid/chart.js\"></script>")]
    [InlineData("<script src=\"//cdn.example.invalid/chart.js\"></script>")]
    [InlineData("<img src=\"http://cdn.example.invalid/poster.png\" alt=\"\" />")]
    [InlineData("<link rel=\"stylesheet\" href=\"//cdn.example.invalid/page.css\" />")]
    public static void TheHostReaderFindsAHostOutsideTheServer(string markup)
    {
        Assert.Equal(["cdn.example.invalid"], ConfigurationPageReader.HostsOutsideTheServerIn(markup));
    }

    /// <summary>
    /// A reference relative to the server is not a host. Without this the page
    /// assertion would refuse the ordinary way a page loads anything and would
    /// be turned off the first time somebody added a stylesheet.
    /// </summary>
    /// <param name="markup">Markup loading something from the server itself.</param>
    [Theory]
    [InlineData("<script src=\"configurationpage.js\"></script>")]
    [InlineData("<link rel=\"stylesheet\" href=\"/web/css/page.css\" />")]
    public static void TheHostReaderIgnoresAReferenceToTheServerItself(string markup)
    {
        Assert.Empty(ConfigurationPageReader.HostsOutsideTheServerIn(markup));
    }
}
