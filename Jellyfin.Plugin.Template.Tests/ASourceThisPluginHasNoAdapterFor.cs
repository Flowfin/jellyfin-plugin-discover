using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Sources;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A metadata source for a body this plugin ships no adapter for, written from
/// nothing but the interface.
/// </summary>
/// <remarks>
/// #73's fourth condition, which asks that adding a source be adding an adapter
/// and a registration with no edit to the shelf code, proven by a second
/// adapter in the test project that exists only for that purpose. This is that
/// second one. It answers for <see cref="MetadataSource.Tvdb"/>, which the
/// vocabulary declares and which nothing in the plugin speaks for.
///
/// IT IS NOT A HALF-ADAPTER FOR THAT SOURCE AND #83 IS UNTOUCHED BY IT. That
/// issue asks that nothing be built for TheTVDB before the decision to take it
/// as a source, and nothing here is: this type opens no connection, holds no
/// credential, reads no wire format, names no host, and has no terms page
/// because it has no terms. It is a stand-in for the shape of a second source
/// rather than an implementation of one, and it lives in the test project
/// where an adapter cannot ship from.
///
/// It is deliberately not named with the <c>SourceAdapter</c> suffix.
/// `no-network-outside-source-adapter` excepts that suffix by name, so a test
/// type carrying it would widen a live exception for the convenience of a
/// fixture.
///
/// It is also deliberately not
/// <see cref="SourceThatAnswersFromWhatATestGaveIt"/> with another argument.
/// What this condition is about is a second implementation arriving, and a
/// second instance of the first one would prove that a field can hold two
/// values.
/// </remarks>
internal sealed class ASourceThisPluginHasNoAdapterFor : IMetadataSource
{
    private readonly Dictionary<string, IReadOnlyList<DiscoverTitle>> _answers =
        new Dictionary<string, IReadOnlyList<DiscoverTitle>>(StringComparer.Ordinal);

    private readonly List<SourceQuery> _asked = new List<SourceQuery>();

    /// <inheritdoc />
    public MetadataSource Source => MetadataSource.Tvdb;

    /// <inheritdoc />
    /// <remarks>
    /// A week, chosen so that it is not the day the other fake answers with. A
    /// test that relied on a ceiling and got the same number from either source
    /// would not be able to say which one it read.
    /// </remarks>
    public TimeSpan RetentionCeiling => TimeSpan.FromDays(7);

    /// <summary>
    /// Gets every question this source was asked, in the order it was asked them.
    /// </summary>
    public IReadOnlyList<SourceQuery> Asked => _asked;

    /// <summary>
    /// Says what this source answers a named question with, for either kind of title.
    /// </summary>
    /// <param name="question">The question's name, in the shelf's vocabulary.</param>
    /// <param name="titles">What to answer it with.</param>
    /// <remarks>
    /// Keyed on the question's name alone rather than on the whole query, which
    /// is a different arrangement from the other fake's on purpose: an adapter
    /// decides for itself what part of a query it keys on, and a second one
    /// that had to be set up exactly like the first would be evidence that the
    /// interface admits one shape.
    /// </remarks>
    public void Answers(string question, IReadOnlyList<DiscoverTitle> titles) => _answers[question] = titles;

    /// <inheritdoc />
    public Task<SourceAnswer> FetchAsync(SourceQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _asked.Add(query.Validated());

        return Task.FromResult(
            _answers.TryGetValue(query.Name, out var titles)
                ? SourceAnswer.Answered(titles, titles.Count)
                : SourceAnswer.NotConfigured());
    }
}
