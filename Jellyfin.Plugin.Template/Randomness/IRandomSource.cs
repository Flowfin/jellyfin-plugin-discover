using System;

namespace Jellyfin.Plugin.Template.Randomness;

/// <summary>
/// The only thing in this plugin that answers with a number nobody chose.
/// </summary>
/// <remarks>
/// The same argument as the clock, for the other source of an answer a test
/// cannot predict. A shuffled shelf, a jittered backoff and a generated
/// identifier are all things a test has to be able to fix, and it can only fix
/// them if they arrive from somewhere it supplies.
///
/// Nothing here is for cryptography. The three members exist for shelf order,
/// backoff jitter and local identity, and a key or a token needs a generator
/// chosen for that job rather than this one.
///
/// The invariant <c>no-random</c> refuses a direct call anywhere but the one
/// implementation that supplies this.
/// </remarks>
public interface IRandomSource
{
    /// <summary>
    /// Returns a non-negative number below <paramref name="exclusiveUpperBound"/>.
    /// </summary>
    /// <param name="exclusiveUpperBound">The bound the result stays below. Must not be negative.</param>
    /// <returns>A number in the half-open range from zero to the bound.</returns>
    int Next(int exclusiveUpperBound);

    /// <summary>
    /// Returns a number in the half-open range from zero to one.
    /// </summary>
    /// <returns>A number that is at least zero and below one.</returns>
    /// <remarks>
    /// This is the shape a backoff jitter wants: a fraction of a delay the
    /// caller already computed, rather than a delay of its own.
    /// </remarks>
    double NextDouble();

    /// <summary>
    /// Returns a new identifier.
    /// </summary>
    /// <returns>An identifier not returned before.</returns>
    /// <remarks>
    /// Here rather than called where it is needed, because an identifier
    /// generated inline is the most common reason a record written by this
    /// plugin cannot be compared against an expected one in a test.
    /// </remarks>
    Guid NewIdentifier();
}
