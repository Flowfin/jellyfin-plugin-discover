using System;

namespace Jellyfin.Plugin.Template.Surface;

/// <summary>
/// What the surface calls itself, as one record.
/// </summary>
/// <remarks>
/// One record rather than four properties spread across whatever implements the
/// surface, because #53 asks that the name, the description and the images come
/// from one place instead of being the plugin's name repeated. The values
/// themselves are #53's; this is the shape they arrive in.
///
/// The name is load bearing beyond display. The server hashes an item's
/// identity out of the external identifier and the surface's name together, so
/// changing the name orphans every item a user marked anything on. That is #60,
/// and it is the reason this record exists as something a reader can find.
/// </remarks>
public sealed class SurfaceDescription
{
    private readonly string _name = null!;
    private readonly string _summary = null!;
    private readonly string _catalogueVersion = null!;
    private readonly SurfaceAudience _audience;
    private readonly Uri? _homePage;

    /// <summary>
    /// Gets what the surface is called, which a user sees as the name of a library. Never absent.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is null, empty or whitespace.</exception>
    public required string Name
    {
        get => _name;
        init => _name = NotBlank(
            value,
            "A surface with no name is a library tile a user cannot read.");
    }

    /// <summary>
    /// Gets what the surface says about itself, which some clients draw and others do not. Never absent.
    /// </summary>
    /// <remarks>
    /// Where the attribution a source requires is rendered is #76, and this is
    /// one of the two places named there, so a blank one is refused rather than
    /// left to be filled in later.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when the value is null, empty or whitespace.</exception>
    public required string Summary
    {
        get => _summary;
        init => _summary = NotBlank(
            value,
            "A surface with no description drops one of the two places the source's notice is rendered, which is #76.");
    }

    /// <summary>
    /// Gets the token that changes when what the surface would answer has changed. Never absent.
    /// </summary>
    /// <remarks>
    /// The server keeps what a surface returned and reads this to decide
    /// whether to keep keeping it, so a token that never moves is a surface
    /// that never updates and a token that moves on every call is a refresh on
    /// every browse. What it is derived from is the catalogue, which is #65,
    /// and how long the server holds the answer regardless is #61.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when the value is null, empty or whitespace.</exception>
    public required string CatalogueVersion
    {
        get => _catalogueVersion;
        init => _catalogueVersion = NotBlank(
            value,
            "A blank version token is one the server cannot tell from the last blank one, so nothing it kept would ever be reconsidered.");
    }

    /// <summary>
    /// Gets who the surface is drawn for. Never absent.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is <see cref="SurfaceAudience.None"/>, which is
    /// what an unset field reads as.
    /// </exception>
    public required SurfaceAudience Audience
    {
        get => _audience;
        init
        {
            if (value is not (SurfaceAudience.General or SurfaceAudience.Adult))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A surface states who it is drawn for. None is what an unset field reads as, and the server reads the unset value as the most permissive band.");
            }

            _audience = value;
        }
    }

    /// <summary>
    /// Gets where a user is sent to read more, or null where there is nowhere.
    /// </summary>
    /// <remarks>
    /// May be absent, and absent is the honest answer until there is a page
    /// saying something a user of this surface wants. A location at the source
    /// is not this: the surface is this plugin's rather than the source's.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when the value is a relative location.</exception>
    public Uri? HomePage
    {
        get => _homePage;
        init
        {
            if (value is not null && !value.IsAbsoluteUri)
            {
                throw new ArgumentException(
                    $"A home page is somewhere a client opens, so it has to say which host. '{value}' does not.",
                    nameof(value));
            }

            _homePage = value;
        }
    }

    private static string NotBlank(string value, string why)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(why, nameof(value));
        }

        return value;
    }
}
