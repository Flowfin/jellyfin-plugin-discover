using System;

namespace Jellyfin.Plugin.Template.Catalogue;

/// <summary>
/// One source's identifier for one title.
/// </summary>
/// <remarks>
/// The pair is one value rather than two fields side by side, because an
/// identifier without the body that issued it is a number that means different
/// titles at different sources, and every place that held them apart would have
/// to keep them together by care.
/// </remarks>
/// <param name="Source">The body that issued the identifier.</param>
/// <param name="Value">
/// The identifier exactly as the source spells it, including any prefix the
/// source's own form carries. Not parsed and not normalised: a value this
/// plugin rewrote is a value that no longer round-trips back to the source it
/// came from.
/// </param>
public readonly record struct ProviderIdentifier(MetadataSource Source, string Value)
{
    /// <summary>
    /// Refuses a pair that could not identify anything.
    /// </summary>
    /// <returns>The same pair, so a caller can validate and assign in one step.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the source is <see cref="MetadataSource.None"/>, which is
    /// what an unset field reads as, or when the value is null, empty or
    /// whitespace. Absent is representable by leaving the pair out of an
    /// identity, so a blank value is a fault rather than an absence.
    /// </exception>
    public ProviderIdentifier Validated()
    {
        if (Source == MetadataSource.None)
        {
            throw new ArgumentException(
                "A provider identifier names no source. MetadataSource.None is what an unset field reads as, and an identity holding it would compare equal to every other identity that forgot the same thing.",
                nameof(Source));
        }

        if (string.IsNullOrWhiteSpace(Value))
        {
            throw new ArgumentException(
                $"The identifier {Source} supplied is empty. An absent identifier is left out of the identity rather than stored blank.",
                nameof(Value));
        }

        return this;
    }
}
