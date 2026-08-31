using System;

namespace Jellyfin.Plugin.Template.Refresh;

/// <summary>
/// How long a source was left alone after it refused, and whether this plugin
/// has stopped taking it at its word.
/// </summary>
/// <remarks>
/// Returned by <see cref="SourceRest.Refused"/> rather than logged there,
/// because what an operator is told is a run's business and
/// <see cref="SourceRest"/> holds no logger. #78's fourth condition asks that
/// the operator be told when a source is left alone after a threshold, and
/// <see cref="GaveUp"/> is the flag that says which of the two kinds of rest
/// this is: an ordinary backoff nobody needs to read about, or the one where
/// this plugin has stopped asking.
/// </remarks>
/// <param name="Rest">How long the source is left alone for.</param>
/// <param name="Until">The instant the source may be asked again.</param>
/// <param name="Refusals">
/// How many refusals in a row the source has now given, capped at
/// <see cref="SourceRest.Tries"/> so that a source refusing for a week does not
/// carry a number that grows without a bound.
/// </param>
/// <param name="GaveUp">
/// Whether the threshold has been reached, so the rest is
/// <see cref="SourceRest.LongestRest"/> rather than the backoff and whatever
/// wait the source stated was not taken.
/// </param>
public readonly record struct SourceRestTaken(
    TimeSpan Rest,
    DateTimeOffset Until,
    int Refusals,
    bool GaveUp);
