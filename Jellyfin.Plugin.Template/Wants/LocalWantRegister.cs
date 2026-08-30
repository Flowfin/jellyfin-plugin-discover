using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Template.Seam;

namespace Jellyfin.Plugin.Template.Wants;

/// <summary>
/// Who asked for what, and when, on this server.
/// </summary>
/// <remarks>
/// #97 is what this exists for. With no requests plugin installed a want has to
/// go somewhere or the gesture is a lie, and the cheapest honest answer is a
/// list the operator can read. It is not a fallback: it is written whether or
/// not a sink took the want, so the list is complete rather than the record of
/// what failed. Nothing here calls the seam and nothing on the seam calls this,
/// which is what keeps the two independent - a handover that refused and a
/// handover that never happened leave the same row.
///
/// The bound is a property of this storage rather than of a page, because a
/// list with no bound on a busy server is the failure #97's fourth condition
/// names, and a page bounding what it draws would leave the growth in place and
/// hide it. What happens at the bound is that the newest want is refused, which
/// is the answer that can be squared with the list being complete;
/// <see cref="LocalWantOutcome.Refused"/> carries that argument.
///
/// No number is chosen here. The bound is required of whoever builds one, the
/// way <c>Shelf.Cap</c> is required of a shelf, so this type takes a decision
/// and states none of its own. Where that number comes from on a running server
/// is a setting, and settings are validated on save by #105.
///
/// THIS SAID IT DOES NOT SURVIVE A RESTART, and it does where it is given a
/// store. #97's fifth condition is what that is for. The decision the record on
/// that issue held open was where a want list is written, and it is taken in
/// <see cref="WantListStore"/> rather than here: a directory of its own beside
/// the catalogue's, because #72 throws the catalogue away as one directory and
/// #68's retention is about fetched records rather than about what a user asked
/// for.
///
/// A register built without one still holds its rows in memory only, and that
/// is the shape every test that is not about persistence uses. What it costs is
/// stated rather than left to be discovered: nothing warns a caller who omits
/// the store, so a server wired that way loses its list at every restart and
/// nothing says so.
///
/// The write is through rather than at intervals. A list saved on a timer is one
/// that loses whatever arrived after the last tick, and #97's first condition is
/// that the list is complete rather than nearly complete. What it costs is a
/// file write per gesture, on a list the bound already keeps small.
///
/// A write that fails is not swallowed. The caller made a gesture and the row it
/// produced did not reach a disk, and a register that returned
/// <see cref="LocalWantOutcome.Recorded"/> for a row nobody kept would be the
/// lie this whole type exists against. The row stays in memory, because dropping
/// it would lose a want the caller was told about in the same breath.
///
/// Locked rather than left to the caller. The gesture arrives on whichever
/// request thread the server was serving, so two users favouriting at once are
/// two threads in here, and a bound compared outside a lock is a bound two
/// callers can both pass.
/// </remarks>
public sealed class LocalWantRegister
{
    private readonly Dictionary<string, LocalWant> _wants = new Dictionary<string, LocalWant>(StringComparer.Ordinal);
    private readonly object _gate = new object();
    private readonly int _bound;
    private readonly WantListStore? _store;
    private readonly int _dropped;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalWantRegister"/> class.
    /// </summary>
    /// <param name="bound">The most rows this register may hold.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the bound is zero or negative. A register that may hold
    /// nothing refuses every gesture on the server it is installed on, which is
    /// a configuration nobody would choose deliberately and which reads to a
    /// user as a broken button.
    /// </exception>
    public LocalWantRegister(int bound)
        : this(bound, store: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalWantRegister"/> class
    /// that keeps its rows across a restart.
    /// </summary>
    /// <param name="bound">The most rows this register may hold.</param>
    /// <param name="store">Where the rows are kept, or null to hold them in memory only.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the bound is zero or negative, for the reason on the other
    /// constructor.
    /// </exception>
    /// <remarks>
    /// What is already on the disk is loaded here rather than on the first read,
    /// so a gesture that arrives before anybody has looked at the list is
    /// compared against the bound the stored rows already occupy.
    ///
    /// Rows beyond the bound are dropped on the way in, oldest kept first, and
    /// the drop is reported. The alternative is a register that starts over its
    /// own bound and refuses every new gesture until somebody clears rows by
    /// hand, which is a server whose button has stopped working for a reason no
    /// operator can see. It happens when a bound is lowered, which is a setting
    /// somebody changed rather than a fault.
    /// </remarks>
    public LocalWantRegister(int bound, WantListStore? store)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);

        _bound = bound;
        _store = store;

        if (store is null)
        {
            return;
        }

        foreach (var kept in store.Read())
        {
            if (_wants.Count >= _bound)
            {
                _dropped++;
                continue;
            }

            _wants[kept.WantIdentifier] = kept;
        }
    }

    /// <summary>
    /// Gets the most rows this register may hold.
    /// </summary>
    public int Bound => _bound;

    /// <summary>
    /// Gets how many stored rows did not fit the bound when this register was
    /// built.
    /// </summary>
    /// <remarks>
    /// Nonzero only where a bound was lowered under a list that was already
    /// longer. It is a count rather than a log line because the operator's page
    /// is where it belongs, which is #103, and a number nobody reads is still a
    /// number somebody can.
    /// </remarks>
    public int DroppedOnLoad => _dropped;

    /// <summary>
    /// Gets how many rows it holds.
    /// </summary>
    /// <remarks>
    /// Withdrawn rows are counted. They are part of the list the operator asked
    /// to see, so they occupy the bound; a bound that counted only standing
    /// wants would be a bound on nothing, because a server whose users withdrew
    /// everything could hold rows without limit.
    /// </remarks>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _wants.Count;
            }
        }
    }

    /// <summary>
    /// Every row, oldest first.
    /// </summary>
    /// <returns>A copy of what the register holds.</returns>
    /// <remarks>
    /// A copy rather than a view, so a caller drawing a page is not reading a
    /// collection another thread is writing.
    ///
    /// Ordered by when the want was first asked for and then by its identifier,
    /// ordinal, so two calls on one register answer the same way. Dictionary
    /// order is not specified, and a page whose rows moved between two draws
    /// would look to an operator like a list that is changing under them.
    /// </remarks>
    public IReadOnlyList<LocalWant> Wants()
    {
        lock (_gate)
        {
            return InOrder();
        }
    }

    /// <summary>
    /// Records that somebody asked for a title.
    /// </summary>
    /// <param name="want">The want as it crosses the seam.</param>
    /// <param name="at">The moment, from the clock this plugin was given.</param>
    /// <returns>What the register did with it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="want"/> is null.</exception>
    /// <remarks>
    /// Keyed on the want identifier, which #99 derives from the title identity
    /// and the asking user, so the same person asking twice is one row and two
    /// people asking for one title are two. Neither of those is decided here.
    ///
    /// The bound is only reached by a want the register does not already hold. A
    /// re-ask of a row that is already there changes a state and adds nothing,
    /// so refusing it at a full register would refuse a user the ability to undo
    /// their own withdrawal.
    /// </remarks>
    public LocalWantOutcome Record(Want want, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(want);

        lock (_gate)
        {
            if (_wants.TryGetValue(want.WantIdentifier, out var standing))
            {
                if (standing.State is LocalWantState.Asked)
                {
                    return LocalWantOutcome.AlreadyStanding;
                }

                _wants[want.WantIdentifier] = standing.Reasked();
                Keep();
                return LocalWantOutcome.Reasked;
            }

            if (_wants.Count >= _bound)
            {
                return LocalWantOutcome.Refused;
            }

            _wants[want.WantIdentifier] = LocalWant.From(want, at);
            Keep();
            return LocalWantOutcome.Recorded;
        }
    }

    /// <summary>
    /// Records that somebody took a want back.
    /// </summary>
    /// <param name="wantIdentifier">This plugin's identifier for the want.</param>
    /// <param name="at">The moment, from the clock this plugin was given.</param>
    /// <returns>
    /// True where a standing row was moved, false where the register held no
    /// such row or it was withdrawn already.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the identifier is null, empty or whitespace.
    /// </exception>
    /// <remarks>
    /// The row stays. A withdrawal is not a deletion, which is
    /// <see cref="LocalWantState"/>'s argument and is the difference between
    /// this and <see cref="Clear"/>: one is what a user did and the other is
    /// what an operator did, and only the second is an administrative action.
    /// </remarks>
    public bool Withdraw(string wantIdentifier, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wantIdentifier);

        lock (_gate)
        {
            if (!_wants.TryGetValue(wantIdentifier, out var standing)
                || standing.State is not LocalWantState.Asked)
            {
                return false;
            }

            _wants[wantIdentifier] = standing.Withdrawn(at);
            Keep();
            return true;
        }
    }

    /// <summary>
    /// Removes one row, which is the operator clearing an entry.
    /// </summary>
    /// <param name="wantIdentifier">This plugin's identifier for the want.</param>
    /// <returns>True where a row was removed.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the identifier is null, empty or whitespace.
    /// </exception>
    /// <remarks>
    /// The only removal a person performs, and it is the operator's rather than
    /// the user's, which is why it is a different method from
    /// <see cref="Withdraw"/> rather than a flag on it. #97's second condition
    /// is where it is drawn, and that condition waits on the configuration page
    /// in #103.
    /// </remarks>
    public bool Clear(string wantIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wantIdentifier);

        lock (_gate)
        {
            if (!_wants.Remove(wantIdentifier))
            {
                return false;
            }

            Keep();
            return true;
        }
    }

    /// <summary>
    /// Removes everything this register holds about one user.
    /// </summary>
    /// <param name="user">The server's identifier for the user.</param>
    /// <returns>How many rows were removed.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the identifier is <see cref="Guid.Empty"/>. That is what an
    /// unset field reads as, and a sweep taking it would either remove nothing
    /// or, on a register that had somehow stored one, remove rows belonging to
    /// nobody in particular.
    /// </exception>
    /// <remarks>
    /// #97's third condition and #70's third: what is held about a person is
    /// removed with the person. It is keyed on the server's identifier rather
    /// than on anything this plugin minted, which is what makes the removal
    /// possible at all - a list keyed on a user name would leave rows behind
    /// after a rename and remove somebody else's after a reuse.
    ///
    /// What calls it is not here. Whether the server offers an event when a user
    /// is deleted or whether a sweep is needed is #70's question and it has no
    /// answer, so this is the operation that answer will use rather than a
    /// caller invented to give it a reference.
    /// </remarks>
    public int Forget(Guid user)
    {
        if (user == Guid.Empty)
        {
            throw new ArgumentException(
                "A sweep removes what is held about one user. Guid.Empty is what an unset field reads as, and a sweep taking it is a sweep aimed at nobody.",
                nameof(user));
        }

        lock (_gate)
        {
            var doomed = new List<string>();

            foreach (var row in _wants)
            {
                if (row.Value.AskingUser == user)
                {
                    doomed.Add(row.Key);
                }
            }

            foreach (var key in doomed)
            {
                _wants.Remove(key);
            }

            if (doomed.Count > 0)
            {
                Keep();
            }

            return doomed.Count;
        }
    }

    /// <summary>
    /// The rows in the order the operator's page and the file both use.
    /// </summary>
    /// <returns>The rows, oldest asking first.</returns>
    /// <remarks>
    /// One order for the two readers rather than one each. A file written in a
    /// different order from the page would make a restart look like the list had
    /// been rearranged, and the register is the only thing that could say it had
    /// not.
    ///
    /// Called with the lock held, by both of its callers.
    /// </remarks>
    private List<LocalWant> InOrder()
    {
        var rows = new List<LocalWant>(_wants.Values);

        rows.Sort(static (left, right) =>
        {
            var byDate = left.AskedAt.CompareTo(right.AskedAt);

            return byDate != 0
                ? byDate
                : string.CompareOrdinal(left.WantIdentifier, right.WantIdentifier);
        });

        return rows;
    }

    /// <summary>
    /// Writes what is held, where this register was given somewhere to write it.
    /// </summary>
    /// <remarks>
    /// Called with the lock held, so the file is written in the same order the
    /// changes were made and two gestures cannot interleave into a list that
    /// holds neither.
    ///
    /// A failure is not caught here. What it means is that a row the caller was
    /// told about did not reach a disk, and swallowing it would leave the caller
    /// believing a thing this type exists to make true.
    /// </remarks>
    private void Keep() => _store?.Write(InOrder());
}
