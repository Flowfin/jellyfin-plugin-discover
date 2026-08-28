using System;
using System.Linq;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Seam;
using Jellyfin.Plugin.Template.Wants;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The list a server keeps of who asked for what, and what it refuses to become.
/// </summary>
/// <remarks>
/// #97 is what these are for. What is asserted below is that the list is
/// complete rather than a fallback, that it is bounded and says so when the
/// bound is reached, that a withdrawal is not a deletion, and that everything
/// held about a person goes when the person does.
///
/// One of #97's conditions has no subject here and is not pretended at. The
/// operator sees this list on the configuration page, and the page carries no
/// controls, which is #103. Another is unmet rather than absent: a want
/// surviving a restart needs the list to be written down, and nothing writes it,
/// which the register says of itself and which is recorded on the issue.
///
/// Every moment below comes from a clock a test moves. Nothing here sleeps and
/// nothing reads the machine, which is what <c>no-sleep-in-a-test</c> and
/// <c>no-wall-clock</c> hold for the tree.
/// </remarks>
public class LocalWantRegisterTests
{
    /// <summary>
    /// The instant every clock below starts at.
    /// </summary>
    private static readonly DateTimeOffset _start = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid _ada = new Guid("2f1d0f9a-6f66-4a51-9a3f-9f5a6e2c1b74");
    private static readonly Guid _grace = new Guid("6b2a4c18-1f3e-42d9-9c77-0d5b8a3e77f1");

    private static readonly string[] _theTwoThatWereHeld =
    {
        "tmdb:329865:ada",
        "tmdb:157336:ada"
    };

    private static readonly string[] _inTheOrderTheyWereAsked =
    {
        "tmdb:335984:ada",
        "tmdb:329865:ada",
        "tmdb:157336:ada"
    };

    private static readonly string[] _everyFieldARowCarries =
    {
        "AskedAt",
        "AskingUser",
        "Identity",
        "Kind",
        "Name",
        "ReleaseYear",
        "State",
        "WantIdentifier",
        "WithdrawnAt"
    };

    private static Want AWant(string wantIdentifier, Guid askingUser) => new Want
    {
        Identity = new DiscoverTitleIdentity(new[] { new ProviderIdentifier(MetadataSource.Tmdb, "329865") }),
        Kind = DiscoverTitleKind.Movie,
        Name = "Arrival",
        ReleaseYear = 2016,
        AskingUser = askingUser,
        WantIdentifier = wantIdentifier
    };

    /// <summary>
    /// A want that was asked for is held and can be read back.
    /// </summary>
    [Fact]
    public void AWantIsRecordedAndListed()
    {
        var clock = new ClockATestAdvances(_start);
        var register = new LocalWantRegister(bound: 10);

        var outcome = register.Record(AWant("tmdb:329865:ada", _ada), clock.UtcNow);

        Assert.Equal(LocalWantOutcome.Recorded, outcome);

        var row = Assert.Single(register.Wants());
        Assert.Equal("tmdb:329865:ada", row.WantIdentifier);
        Assert.Equal("Arrival", row.Name);
        Assert.Equal(_ada, row.AskingUser);
        Assert.Equal(LocalWantState.Asked, row.State);
        Assert.Null(row.WithdrawnAt);
    }

    /// <summary>
    /// The moment on a row is the one the clock read, not the machine's.
    /// </summary>
    /// <remarks>
    /// The clock is advanced by an amount nothing else in this file uses, so a
    /// row stamped from anywhere but the clock fails rather than passing by
    /// coincidence.
    /// </remarks>
    [Fact]
    public void TheMomentOnARowIsTheOneTheClockRead()
    {
        var clock = new ClockATestAdvances(_start);
        var register = new LocalWantRegister(bound: 10);

        clock.Advance(TimeSpan.FromHours(37));
        register.Record(AWant("tmdb:329865:ada", _ada), clock.UtcNow);

        Assert.Equal(_start + TimeSpan.FromHours(37), Assert.Single(register.Wants()).AskedAt);
    }

    /// <summary>
    /// One person asking twice is one row, and the register says nothing changed.
    /// </summary>
    [Fact]
    public void OnePersonAskingTwiceIsOneRow()
    {
        var clock = new ClockATestAdvances(_start);
        var register = new LocalWantRegister(bound: 10);
        var want = AWant("tmdb:329865:ada", _ada);

        Assert.Equal(LocalWantOutcome.Recorded, register.Record(want, clock.UtcNow));

        clock.Advance(TimeSpan.FromDays(2));

        Assert.Equal(LocalWantOutcome.AlreadyStanding, register.Record(want, clock.UtcNow));
        Assert.Equal(1, register.Count);
        Assert.Equal(_start, Assert.Single(register.Wants()).AskedAt);
    }

    /// <summary>
    /// Two people wanting one title are two rows.
    /// </summary>
    /// <remarks>
    /// The register decides none of this. The user is inside the want
    /// identifier, by #99, so two people asking for one film arrive here as two
    /// keys; what is asserted is that nothing in the register collapses them
    /// back on the title.
    /// </remarks>
    [Fact]
    public void TwoPeopleWantingOneTitleAreTwoRows()
    {
        var clock = new ClockATestAdvances(_start);
        var register = new LocalWantRegister(bound: 10);

        register.Record(AWant("tmdb:329865:ada", _ada), clock.UtcNow);
        register.Record(AWant("tmdb:329865:grace", _grace), clock.UtcNow);

        Assert.Equal(2, register.Count);
        Assert.Equal(
            new[] { _ada, _grace }.OrderBy(user => user).ToArray(),
            register.Wants().Select(row => row.AskingUser).OrderBy(user => user).ToArray());
    }

    /// <summary>
    /// A withdrawal moves the row rather than removing it.
    /// </summary>
    /// <remarks>
    /// This is the assertion #97's first condition rests on. A list that deleted
    /// the row on un-favourite would be shorter than what happened on the
    /// server, and the operator would have no way of knowing that anybody had
    /// ever asked.
    /// </remarks>
    [Fact]
    public void AWithdrawalMovesTheRowRatherThanRemovingIt()
    {
        var clock = new ClockATestAdvances(_start);
        var register = new LocalWantRegister(bound: 10);
        register.Record(AWant("tmdb:329865:ada", _ada), clock.UtcNow);

        clock.Advance(TimeSpan.FromDays(1));

        Assert.True(register.Withdraw("tmdb:329865:ada", clock.UtcNow));

        var row = Assert.Single(register.Wants());
        Assert.Equal(LocalWantState.Withdrawn, row.State);
        Assert.Equal(_start, row.AskedAt);
        Assert.Equal(_start + TimeSpan.FromDays(1), row.WithdrawnAt);
    }

    /// <summary>
    /// Withdrawing what is not standing changes nothing and says so.
    /// </summary>
    /// <param name="identifier">
    /// A want the register holds and has already withdrawn, and one it never
    /// held at all.
    /// </param>
    [Theory]
    [InlineData("tmdb:329865:ada")]
    [InlineData("tmdb:000000:nobody")]
    public void WithdrawingWhatIsNotStandingChangesNothing(string identifier)
    {
        var clock = new ClockATestAdvances(_start);
        var register = new LocalWantRegister(bound: 10);
        register.Record(AWant("tmdb:329865:ada", _ada), clock.UtcNow);
        register.Withdraw("tmdb:329865:ada", clock.UtcNow);

        Assert.False(register.Withdraw(identifier, clock.UtcNow));
        Assert.Equal(1, register.Count);
    }

    /// <summary>
    /// A want asked for again stands again, dated from the first asking.
    /// </summary>
    [Fact]
    public void AWantAskedForAgainStandsAgainDatedFromTheFirstAsking()
    {
        var clock = new ClockATestAdvances(_start);
        var register = new LocalWantRegister(bound: 10);
        var want = AWant("tmdb:329865:ada", _ada);

        register.Record(want, clock.UtcNow);
        clock.Advance(TimeSpan.FromDays(1));
        register.Withdraw("tmdb:329865:ada", clock.UtcNow);
        clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal(LocalWantOutcome.Reasked, register.Record(want, clock.UtcNow));

        var row = Assert.Single(register.Wants());
        Assert.Equal(LocalWantState.Asked, row.State);
        Assert.Null(row.WithdrawnAt);
        Assert.Equal(_start, row.AskedAt);
    }

    /// <summary>
    /// The operator clearing an entry removes it.
    /// </summary>
    [Fact]
    public void ClearingAnEntryRemovesIt()
    {
        var clock = new ClockATestAdvances(_start);
        var register = new LocalWantRegister(bound: 10);
        register.Record(AWant("tmdb:329865:ada", _ada), clock.UtcNow);

        Assert.True(register.Clear("tmdb:329865:ada"));
        Assert.Empty(register.Wants());
        Assert.False(register.Clear("tmdb:329865:ada"));
    }

    /// <summary>
    /// Removing a user removes their rows and nobody else's.
    /// </summary>
    /// <remarks>
    /// #97's third condition and #70's, and the reason the row holds an
    /// identifier rather than a name: this sweep is possible because the key is
    /// the server's own and does not move under a rename.
    /// </remarks>
    [Fact]
    public void RemovingAUserRemovesTheirRowsAndNobodyElses()
    {
        var clock = new ClockATestAdvances(_start);
        var register = new LocalWantRegister(bound: 10);
        register.Record(AWant("tmdb:329865:ada", _ada), clock.UtcNow);
        register.Record(AWant("tmdb:157336:ada", _ada), clock.UtcNow);
        register.Record(AWant("tmdb:329865:grace", _grace), clock.UtcNow);

        Assert.Equal(2, register.Forget(_ada));

        var row = Assert.Single(register.Wants());
        Assert.Equal(_grace, row.AskingUser);
        Assert.Equal(0, register.Forget(_ada));
    }

    /// <summary>
    /// A sweep cannot be aimed at nobody.
    /// </summary>
    [Fact]
    public void ASweepCannotBeAimedAtNobody()
    {
        var register = new LocalWantRegister(bound: 10);

        Assert.Throws<ArgumentException>(() => register.Forget(Guid.Empty));
    }

    /// <summary>
    /// At the bound the newest want is refused and nothing already held is lost.
    /// </summary>
    /// <remarks>
    /// The half worth watching is the second one. A register that made room by
    /// dropping its oldest row would answer <see cref="LocalWantOutcome.Recorded"/>
    /// here and still hold two rows, so a test asserting only the count would
    /// pass on the behaviour #97's first condition refuses.
    /// </remarks>
    [Fact]
    public void AtTheBoundTheNewestIsRefusedAndNothingHeldIsLost()
    {
        var clock = new ClockATestAdvances(_start);
        var register = new LocalWantRegister(bound: 2);
        register.Record(AWant("tmdb:329865:ada", _ada), clock.UtcNow);
        clock.Advance(TimeSpan.FromMinutes(1));
        register.Record(AWant("tmdb:157336:ada", _ada), clock.UtcNow);
        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(LocalWantOutcome.Refused, register.Record(AWant("tmdb:335984:ada", _ada), clock.UtcNow));

        Assert.Equal(
            _theTwoThatWereHeld,
            register.Wants().Select(row => row.WantIdentifier).ToArray());
    }

    /// <summary>
    /// A withdrawn row still occupies the bound.
    /// </summary>
    /// <remarks>
    /// It is part of the list the operator asked to see, so it is part of what
    /// the bound is about. A bound that counted only standing wants would bound
    /// nothing: a server whose users withdrew everything could hold rows without
    /// limit.
    /// </remarks>
    [Fact]
    public void AWithdrawnRowStillOccupiesTheBound()
    {
        var clock = new ClockATestAdvances(_start);
        var register = new LocalWantRegister(bound: 1);
        register.Record(AWant("tmdb:329865:ada", _ada), clock.UtcNow);
        register.Withdraw("tmdb:329865:ada", clock.UtcNow);

        Assert.Equal(LocalWantOutcome.Refused, register.Record(AWant("tmdb:157336:ada", _ada), clock.UtcNow));
    }

    /// <summary>
    /// A full register still lets somebody undo their own withdrawal.
    /// </summary>
    [Fact]
    public void AFullRegisterStillLetsSomebodyUndoTheirOwnWithdrawal()
    {
        var clock = new ClockATestAdvances(_start);
        var register = new LocalWantRegister(bound: 1);
        var want = AWant("tmdb:329865:ada", _ada);
        register.Record(want, clock.UtcNow);
        register.Withdraw("tmdb:329865:ada", clock.UtcNow);

        Assert.Equal(LocalWantOutcome.Reasked, register.Record(want, clock.UtcNow));
    }

    /// <summary>
    /// A register that may hold nothing is refused rather than built.
    /// </summary>
    /// <param name="bound">A bound no want could fit under.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ARegisterThatMayHoldNothingIsRefused(int bound)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LocalWantRegister(bound));
    }

    /// <summary>
    /// Two readings of one register answer in the same order.
    /// </summary>
    /// <remarks>
    /// A row is cleared and asked for again in the middle of the run, and that
    /// is the whole point of the shape rather than decoration. A register
    /// handing back whatever its dictionary held would answer in insertion
    /// order, which is the order asserted here for as long as nothing is ever
    /// removed; a removal frees a slot that the next insertion takes, so the two
    /// orders part company and the sort is what the answer rests on.
    /// </remarks>
    [Fact]
    public void TwoReadingsOfOneRegisterAnswerInTheSameOrder()
    {
        var clock = new ClockATestAdvances(_start);
        var register = new LocalWantRegister(bound: 10);

        clock.Advance(TimeSpan.FromMinutes(5));
        register.Record(AWant("tmdb:335984:ada", _ada), clock.UtcNow);
        clock.Advance(TimeSpan.FromMinutes(5));
        register.Record(AWant("tmdb:157336:ada", _ada), clock.UtcNow);
        clock.Advance(TimeSpan.FromMinutes(5));
        register.Record(AWant("tmdb:329865:ada", _ada), clock.UtcNow);

        register.Clear("tmdb:157336:ada");
        clock.Advance(TimeSpan.FromMinutes(5));
        register.Record(AWant("tmdb:157336:ada", _ada), clock.UtcNow);

        var first = register.Wants().Select(row => row.WantIdentifier).ToArray();
        var second = register.Wants().Select(row => row.WantIdentifier).ToArray();

        Assert.Equal(first, second);
        Assert.Equal(_inTheOrderTheyWereAsked, first);
    }

    /// <summary>
    /// The one thing a row says about a person is the server's identifier for them.
    /// </summary>
    /// <remarks>
    /// Counted off the type rather than read by eye, so a field naming a person
    /// added here fails until somebody has decided it belongs in #70's register.
    /// The assertion is the whole property set rather than the absence of a
    /// name, because an absence is passed by every spelling nobody thought of.
    /// </remarks>
    [Fact]
    public void TheOneThingARowSaysAboutAPersonIsTheServersIdentifierForThem()
    {
        var carried = typeof(LocalWant)
            .GetProperties()
            .Select(property => property.Name)
            .Where(name => !string.Equals(name, "EqualityContract", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(_everyFieldARowCarries, carried);

        Assert.Equal(typeof(Guid), typeof(LocalWant).GetProperty("AskingUser")!.PropertyType);
    }

    /// <summary>
    /// A withdrawal that preceded the asking is refused rather than stored.
    /// </summary>
    [Fact]
    public void AWithdrawalThatPrecededTheAskingIsRefused()
    {
        var clock = new ClockATestAdvances(_start);
        var register = new LocalWantRegister(bound: 10);
        register.Record(AWant("tmdb:329865:ada", _ada), clock.UtcNow);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => register.Withdraw("tmdb:329865:ada", _start - TimeSpan.FromSeconds(1)));
    }
}
