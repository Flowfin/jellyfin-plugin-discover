using System;

namespace Jellyfin.Plugin.Template.Randomness;

/// <summary>
/// The one place in this plugin that asks the runtime for an unpredictable value.
/// </summary>
/// <remarks>
/// The shared generator rather than an instance of this type's own, because a
/// per-instance generator would have to answer when it is seeded and whether it
/// is safe to call from two threads, and the shared one already answers both.
/// It is not a cryptographic generator and nothing here treats it as one.
///
/// This file is the single exception <c>no-random</c> carries, for the reason
/// written on <see cref="Time.SystemClock"/>.
/// </remarks>
public sealed class SystemRandomSource : IRandomSource
{
    /// <inheritdoc />
    public int Next(int exclusiveUpperBound) => Random.Shared.Next(exclusiveUpperBound);

    /// <inheritdoc />
    public double NextDouble() => Random.Shared.NextDouble();

    /// <inheritdoc />
    public Guid NewIdentifier() => Guid.NewGuid();
}
