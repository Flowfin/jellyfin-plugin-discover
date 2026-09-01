using System;
using Jellyfin.Plugin.Template.Sources;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// Which language and region a source may be asked for, and what a bad one costs.
/// </summary>
/// <remarks>
/// This is the one place a value that reaches a query string is judged. The
/// adapter interpolates what it is given, so the shape admitted here and the
/// escaping the adapter does not do are one property rather than two that can
/// disagree, and the assertions below are what holds it.
/// </remarks>
public class SourceLocaleTests
{
    /// <summary>
    /// Both spellings of a language tag are admitted and come back as they were given.
    /// </summary>
    /// <param name="language">The tag.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("en-US")]
    [InlineData("pt-BR")]
    public void ALanguageTagOfEitherShapeIsAdmitted(string language)
    {
        Assert.Equal(language, SourceLocale.Of(language, null).Language);
    }

    /// <summary>
    /// A value that is not a language tag is refused rather than dropped.
    /// </summary>
    /// <remarks>
    /// Refused rather than dropped, which is the opposite of how this plugin
    /// treats a source's own bytes and is deliberate. A tag arrives from this
    /// server's own configuration, so ignoring a bad one would put every title
    /// back in the source's default language with nothing saying a setting had
    /// been ignored.
    ///
    /// The near-miss is the underscore. It is what a reader who knows .NET
    /// culture names writes first, it differs from the accepted spelling by one
    /// character, and a check written as "two letters, a separator, two
    /// letters" admits it.
    /// </remarks>
    /// <param name="language">What was given.</param>
    [Theory]
    [InlineData("en_US")]
    [InlineData("EN")]
    [InlineData("en-us")]
    [InlineData("eng")]
    [InlineData("e")]
    [InlineData("en-USA")]
    [InlineData("en US")]
    [InlineData("en-US&region=XX")]
    public void AValueThatIsNotALanguageTagIsRefused(string language)
    {
        Assert.Throws<ArgumentException>(() => SourceLocale.Of(language, null));
    }

    /// <summary>
    /// A value that is not a country code is refused.
    /// </summary>
    /// <param name="region">What was given.</param>
    [Theory]
    [InlineData("at")]
    [InlineData("AUT")]
    [InlineData("A")]
    [InlineData("A T")]
    [InlineData("AT&language=xx")]
    public void AValueThatIsNotACountryCodeIsRefused(string region)
    {
        Assert.Throws<ArgumentException>(() => SourceLocale.Of("de-DE", region));
    }

    /// <summary>
    /// Nothing given is the value that states neither, and blank counts as nothing.
    /// </summary>
    /// <remarks>
    /// A configuration property nobody has filled in arrives as an empty string
    /// rather than as a null, so both spellings and whitespace have to mean the
    /// same thing. The alternative is a save that refuses every fresh install.
    /// </remarks>
    /// <param name="language">What was given for the language.</param>
    /// <param name="region">What was given for the region.</param>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", null)]
    public void NothingGivenIsTheValueThatStatesNeither(string? language, string? region)
    {
        Assert.Same(SourceLocale.Unstated, SourceLocale.Of(language, region));
    }

    /// <summary>
    /// The value that states neither carries neither.
    /// </summary>
    [Fact]
    public void TheValueThatStatesNeitherCarriesNeither()
    {
        Assert.Null(SourceLocale.Unstated.Language);
        Assert.Null(SourceLocale.Unstated.Region);
    }

    /// <summary>
    /// A region with no language is admitted.
    /// </summary>
    /// <remarks>
    /// The two are separate parameters at the source and a region narrows what
    /// is popular rather than what it is called, so an operator asking for the
    /// source's own language and their own country is asking for something the
    /// source offers.
    /// </remarks>
    [Fact]
    public void ARegionWithNoLanguageIsAdmitted()
    {
        var locale = SourceLocale.Of(null, "AT");

        Assert.Null(locale.Language);
        Assert.Equal("AT", locale.Region);
    }
}
