using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Template.Refresh;

/// <summary>
/// The only thing in this plugin that lets real time pass.
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
/// 2026-08-24, and because nothing else in this plugin waits for anything. A
/// second waiter belongs behind this interface rather than beside
/// <see cref="SystemPause"/>, for the reason <c>no-wall-clock</c> gives about
/// its own exception.
/// </para>
/// <para>
/// NOTHING REFUSES A SECOND <c>Task.Delay</c> IN THIS PLUGIN. <c>no-sleep-in-a-test</c>
/// refuses one in the test project and reaches no further, so the sentence
/// above is a convention rather than a guard. What would make it one is a rule
/// in <c>tools/invariants/rules/</c> excepting <see cref="SystemPause"/> by
/// file, exactly as <c>no-wall-clock</c> excepts <c>SystemClock.cs</c>, and
/// that directory is outside this issue's declared scope. It is named here so a
/// reader meeting this interface is not left taking it for enforced.
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
