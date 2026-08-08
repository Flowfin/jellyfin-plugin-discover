using System.Collections.Generic;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The calls one fake received, in the order it received them.
/// </summary>
/// <remarks>
/// A fake that only answers lets a test say what came back. It cannot say how
/// many times the plugin asked, or in what order, so a change that writes the
/// configuration twice instead of once passes such a test and only costs time.
/// This is what the fakes write into so a test can assert the whole sequence
/// rather than the outcome.
///
/// One log is shared by every fake in a run, on purpose. The order that matters
/// is the order across the interfaces, not within one of them: a write that now
/// happens before the path is read is a different run even though each fake saw
/// the same calls it saw before.
///
/// A path is recorded by its file name and never in full. The directories come
/// from the temporary directory, so they differ between machines and between
/// runs, and asserting one would be asserting something about the runner.
/// Whether the plugin reached for the right directory is a separate question and
/// the fake answering that member is where it is asked.
/// </remarks>
internal sealed class CallLog
{
    private readonly List<string> _calls = new List<string>();

    /// <summary>
    /// Gets the calls recorded so far, oldest first.
    /// </summary>
    public IReadOnlyList<string> Calls => _calls;

    /// <summary>
    /// Records one call.
    /// </summary>
    /// <param name="call">What was called, spelled the way a test asserts it.</param>
    public void Record(string call) => _calls.Add(call);
}
