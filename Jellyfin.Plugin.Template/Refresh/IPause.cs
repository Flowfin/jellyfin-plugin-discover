using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Template.Refresh;

/// <summary>
/// How a refresh lets real time pass.
/// </summary>
/// <remarks>
/// <para>
/// #78's second condition needs a refresh to hold off before a request, and
/// holding off is the one thing <see cref="Time.IClock"/> cannot express: a
/// clock answers what time it is, and a run that has to spread its requests has
/// to spend time rather than read it. Both are the same rule pointed at
/// different halves of the same hazard, which is code that reaches the machine
/// where it stands and is then testable only by being slow.
/// </para>
/// <para>
/// It is here rather than beside the clock because the pacing this exists for
/// lives with whatever drives a refresh, which is where #2 put it on
/// 2026-08-24.
/// </para>
/// <para>
/// IT IS NOT THE ONLY WAITER IN THIS PLUGIN AND THIS FILE SAID IT WAS. The
/// summary read "the only thing in this plugin that lets real time pass" and a
/// second one was already in the tree when that was written:
/// <see cref="Seam.WantHandover"/> bounds how long a handover waits for the
/// siblings it offered a want to, and that bound is served by the runtime's own
/// timer. It is a deadline on somebody else's work rather than a pace of this
/// plugin's own requests, so the two are different shapes, and moving it behind
/// this interface is a change to the seam that #96 and #97 own rather than
/// something #78 may take. What is corrected here is the claim, not the seam.
/// </para>
/// <para>
/// NOTHING REFUSES A SECOND <c>Task.Delay</c> IN THIS PLUGIN. <c>no-sleep-in-a-test</c>
/// refuses one in the test project and reaches no further, so where the waiting
/// lives is a convention rather than a guard. What would make it one is a rule
/// in <c>tools/invariants/rules/</c> naming the files that may wait, which is
/// two files rather than one, and that directory is outside this issue's
/// declared scope. It is named here so a reader meeting this interface is not
/// left taking it for enforced.
/// </para>
/// </remarks>
public interface IPause
{
    /// <summary>
    /// Lets the given amount of time pass.
    /// </summary>
    /// <param name="duration">
    /// How long to hold off. A duration that is zero or negative passes at
    /// once, because the caller computing it is answering "how much longer" and
    /// nothing is the ordinary answer.
    /// </param>
    /// <param name="cancellationToken">Cuts the wait short.</param>
    /// <returns>A task that completes when the wait is over.</returns>
    Task ForAsync(TimeSpan duration, CancellationToken cancellationToken);
}
