using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Template.Refresh;

/// <summary>
/// Where a refresh's waiting reaches the runtime's own timer.
/// </summary>
/// <remarks>
/// NOT THE ONLY PLACE IN THIS PLUGIN THAT SPENDS REAL TIME, WHICH THIS SUMMARY
/// CLAIMED. <see cref="Seam.WantHandover"/> bounds a handover with the same
/// timer, and did so before this file existed. <see cref="IPause"/> carries why
/// the two are different shapes and why neither this file nor that one is
/// refused by anything.
///
/// It holds no state and it makes no decision, which is the same shape
/// <see cref="Time.SystemClock"/> has and for the same reason: everything this
/// type could usefully decide is something a test would then have no way to
/// control, so the whole of it is one call forwarded and the judgement about
/// how long to wait lives in <see cref="SourcePace"/>, which a test can drive.
///
/// A duration that is zero or negative completes without touching the runtime's
/// timer at all. The caller is answering "how much longer", nothing is its
/// ordinary answer, and handing a negative span to the runtime would be an
/// exception thrown on the ordinary path.
/// </remarks>
public sealed class SystemPause : IPause
{
    /// <inheritdoc />
    public Task ForAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        duration <= TimeSpan.Zero
            ? Task.CompletedTask
            : Task.Delay(duration, cancellationToken);
}
