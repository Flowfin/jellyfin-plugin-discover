using System;

namespace Jellyfin.Plugin.Template.Time;

/// <summary>
/// The one place in this plugin that reads the machine's clock.
/// </summary>
/// <remarks>
/// It holds no state and it makes no decision. That is deliberate: everything
/// this type could usefully do is something a test would then have no way to
/// control, so the whole of it is one property forwarding one call, and the
/// judgement built on top of it lives in code a test can drive.
///
/// This file is the single exception <c>no-wall-clock</c> carries. Widening
/// that exception is how the invariant stops meaning anything, so a second
/// reader belongs behind this interface rather than beside it.
/// </remarks>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
