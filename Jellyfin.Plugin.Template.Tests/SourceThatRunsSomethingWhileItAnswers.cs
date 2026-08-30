using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Sources;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A metadata source that does something of the test's choosing before it
/// answers, so a test can reach the code under test while it is still running.
/// </summary>
/// <remarks>
/// This is how the overlap in #87's third condition is asserted without two
/// threads and without a wait. A second thread would need the first one held
/// somewhere while the assertion is made, and the only ways to hold it are a
/// sleep, which <c>no-sleep-in-a-test</c> refuses, or a gate, which makes the
/// test about the gate. Asking the refresh for a second run from inside the
/// first one's own fetch is the same question with no timing in it at all: the
/// first run demonstrably has not finished, because it is what is calling.
///
/// It is also the shape the condition is written for. #88's manual trigger is a
/// second caller arriving while the scheduled run is in the middle of its
/// fetches, which is exactly the moment this stands in for.
/// </remarks>
internal sealed class SourceThatRunsSomethingWhileItAnswers : IMetadataSource
{
    private readonly Func<Task<SourceAnswer>> _answering;
    private readonly List<SourceQuery> _asked = new List<SourceQuery>();

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceThatRunsSomethingWhileItAnswers"/> class.
    /// </summary>
    /// <param name="source">Which body this fake stands in for.</param>
    /// <param name="answering">
    /// What to do when this source is asked, whose result is the answer. It runs
    /// once per question, so a test that wants it once asks one shelf.
    /// </param>
    public SourceThatRunsSomethingWhileItAnswers(MetadataSource source, Func<Task<SourceAnswer>> answering)
    {
        Source = source;
        _answering = answering;
    }

    /// <inheritdoc />
    public MetadataSource Source { get; }

    /// <inheritdoc />
    public TimeSpan RetentionCeiling => TimeSpan.FromDays(1);

    /// <summary>
    /// Gets every question this source was asked, in the order it was asked them.
    /// </summary>
    /// <remarks>
    /// Recorded before the callback runs and without looking at the token, which
    /// is what makes this fake able to show a caller asking when it should not
    /// have. A fake that refused a cancelled call would leave no trace of the
    /// question, so a caller that had not checked its own token would look
    /// exactly like one that had.
    /// </remarks>
    public IReadOnlyList<SourceQuery> Asked => _asked;

    /// <inheritdoc />
    public Task<SourceAnswer> FetchAsync(SourceQuery query, CancellationToken cancellationToken)
    {
        _asked.Add(query.Validated());

        return _answering();
    }
}
