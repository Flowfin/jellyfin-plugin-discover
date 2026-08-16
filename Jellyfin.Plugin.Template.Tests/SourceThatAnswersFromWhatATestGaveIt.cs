using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Sources;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A metadata source, answering from what a test put in it and recording every question it was asked.
/// </summary>
/// <remarks>
/// Hand written rather than produced by a mocking framework, for the reason the
/// other fakes here are: the interface has two members, and what a test reads
/// is C# rather than a second vocabulary.
///
/// It records the questions rather than only answering them, because the
/// counting several later issues are written as needs it. #79 asks for proof
/// that nothing was written on a failure, and #78 for proof that a backoff
/// asked again when it was allowed to and not before; neither is provable
/// against a fake that only answers.
///
/// A question it was given no answer for is answered
/// <see cref="SourceAnswer.NotConfigured"/>, which is what a real adapter does
/// with a name it has no question for.
/// </remarks>
internal sealed class SourceThatAnswersFromWhatATestGaveIt : IMetadataSource
{
    private readonly Dictionary<SourceQuery, SourceAnswer> _answers = new Dictionary<SourceQuery, SourceAnswer>();
    private readonly List<SourceQuery> _asked = new List<SourceQuery>();

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceThatAnswersFromWhatATestGaveIt"/> class.
    /// </summary>
    /// <param name="source">Which body this fake stands in for.</param>
    /// <param name="retentionCeiling">
    /// The longest this stand-in's terms allow anything it answered with to be
    /// kept. Defaults to a day, which is short enough that a test relying on
    /// the ceiling has to state its own rather than inherit a plausible one.
    /// </param>
    public SourceThatAnswersFromWhatATestGaveIt(MetadataSource source, TimeSpan? retentionCeiling = null)
    {
        Source = source;
        RetentionCeiling = retentionCeiling ?? TimeSpan.FromDays(1);
    }

    /// <inheritdoc />
    public MetadataSource Source { get; }

    /// <inheritdoc />
    public TimeSpan RetentionCeiling { get; }

    /// <summary>
    /// Gets every question this source was asked, in the order it was asked them.
    /// </summary>
    public IReadOnlyList<SourceQuery> Asked => _asked;

    /// <summary>
    /// Says what this source answers one question with.
    /// </summary>
    /// <param name="query">The question.</param>
    /// <param name="answer">What to answer it with.</param>
    public void Answer(SourceQuery query, SourceAnswer answer) => _answers[query] = answer;

    /// <inheritdoc />
    public Task<SourceAnswer> FetchAsync(SourceQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _asked.Add(query.Validated());

        return Task.FromResult(
            _answers.TryGetValue(query, out var answer) ? answer : SourceAnswer.NotConfigured());
    }
}
