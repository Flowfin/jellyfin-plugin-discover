using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Sources;

namespace Jellyfin.Plugin.Template.Shelves;

/// <summary>
/// One named row on a discover page, as one record.
/// </summary>
/// <remarks>
/// #85 is what this exists for. A shelf is the unit everything else in this
/// plugin is parameterised by, and the failure it is written against is a shelf
/// that is code: a case per row in the surface, which the second source does not
/// fit and which makes adding a shelf a release rather than an edit to data.
///
/// So this is data and holds no behaviour beyond refusing a shape that cannot be
/// asked and composing the one question it stands for. Nothing here fetches,
/// stores or draws anything.
///
/// Two of its fields are borrowed rather than invented, and that is the point of
/// them being here. <see cref="Cap"/> is the bound #58 decides, and
/// <see cref="Order"/> is the order #91 built. Both live on this record so that
/// no other component holds a second copy: a surface that capped a list itself,
/// or a refresh that sorted with its own comparer, would be a second answer to a
/// question this record already answers.
///
/// What is deliberately not here is anything that changes without the shelf's
/// definition changing. When a shelf was last refreshed, whether that refresh
/// failed, and whether it has ever run separate a shelf that came back empty
/// from one nothing has asked yet, which is #63's third condition. All three are
/// state of a stored catalogue rather than properties of a shelf, and a
/// definition record carrying them would be a record that differs between two
/// servers running the same build. #65 and #67 are where they belong.
/// </remarks>
public sealed record Shelf
{
    private readonly string _displayName = null!;
    private readonly ShelfQuestion _question;
    private readonly DiscoverTitleKind _kind;
    private readonly MetadataSource _source;
    private readonly int _cap;

    /// <summary>
    /// Gets what an operator and a client see this row called.
    /// </summary>
    /// <remarks>
    /// Carried rather than composed from the question and the kind. "Trending"
    /// and "Series" concatenated is a string a later reader has to take apart
    /// again, which is the defect <see cref="DiscoverTitle"/> refuses for the
    /// same reason, and it fixes an English word order on a name that is not
    /// this plugin's to translate.
    ///
    /// It is not what a setting names. What tells one shipped shelf from
    /// another is the question and the kind, which is why there is no separate
    /// key field here: a second identifier for one thing is one more thing that
    /// can disagree with itself. Value equality on this record is over every
    /// field, which is what a record gives and is a different question from
    /// which row an operator turned off.
    /// </remarks>
    public required string DisplayName
    {
        get => _displayName;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _displayName = value;
        }
    }

    /// <summary>
    /// Gets what this shelf asks for.
    /// </summary>
    public required ShelfQuestion Question
    {
        get => _question;
        init => _question = value;
    }

    /// <summary>
    /// Gets which sort of title this shelf holds.
    /// </summary>
    /// <remarks>
    /// One shelf is one kind. A row mixing films and series is a row whose
    /// order has to rank two things nobody ranks against each other, and
    /// <see cref="SourceQuery"/> already carries the kind for the same reason:
    /// most sources ask for the two through different questions.
    /// </remarks>
    public required DiscoverTitleKind Kind
    {
        get => _kind;
        init => _kind = value;
    }

    /// <summary>
    /// Gets which source answers this shelf.
    /// </summary>
    /// <remarks>
    /// Named on the shelf rather than resolved by whoever is refreshing,
    /// because a shelf whose source is chosen at fetch time is a shelf whose
    /// contents change when an operator adds a second source, without anybody
    /// asking for that.
    /// </remarks>
    public required MetadataSource Source
    {
        get => _source;
        init => _source = value;
    }

    /// <summary>
    /// Gets the most titles this shelf may hold.
    /// </summary>
    /// <remarks>
    /// The bound #58 decides, carried here rather than applied by whatever
    /// produces items. #58's own first condition makes it a configured value
    /// with a default, so this record takes a number and states none of its
    /// own: a constant here would be this issue quietly answering that one.
    ///
    /// Required rather than nullable, so a shelf cannot exist without a bound.
    /// An absent cap is what an unbounded plugin looks like from the inside,
    /// and it is the concrete way this plugin damages a server.
    /// </remarks>
    public required int Cap
    {
        get => _cap;
        init => _cap = value;
    }

    /// <summary>
    /// Gets the order this shelf's titles are drawn in.
    /// </summary>
    /// <remarks>
    /// The order #91 built, defaulted to the one it ships rather than left for
    /// a caller to supply, because a shelf that arrived without one would fall
    /// back to the sequence a source answered in, which is exactly what #91
    /// exists against. A shelf that wants another order says so here, and
    /// nothing else in this plugin holds a comparer.
    /// </remarks>
    public IComparer<DiscoverTitle> Order { get; init; } = DiscoverTitleOrder.ByStanding;

    /// <summary>
    /// Gets a value indicating whether this shelf is on.
    /// </summary>
    /// <remarks>
    /// True by default, because a shelf that ships is a shelf somebody argued
    /// for on the shelves page, and an operator turning one off is the setting
    /// #86's fourth condition asks for rather than the state a fresh install
    /// starts in.
    ///
    /// What reads it is not here, and this paragraph said nothing in this tree
    /// read it at all. A shelf that is off is not fetched, not stored and not
    /// shown, which is #85's fourth condition, and the reason given for the
    /// field being a flag three future readers would take was that nothing in
    /// this tree fetches. Something does: the refresh landed under #87 on
    /// 2026-08-30 and reads this field per shelf, answering an off shelf with
    /// <see cref="Refresh.ShelfRefreshOutcome.TurnedOff"/>, asking its source
    /// nothing and writing its document nowhere. So two of the three have a
    /// reader now and it is not this record. Not shown is the one that still
    /// has none, because the surface takes no shelf set and so consults no
    /// flag. The record implements none of the three either way, which is the
    /// half of the sentence that was never about the refresh.
    /// </remarks>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// The question this shelf puts to its source, in the spelling an adapter reads.
    /// </summary>
    /// <param name="startIndex">How many titles to skip, or null for the beginning.</param>
    /// <returns>The query, already validated.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the start index is negative, or when this shelf's fields
    /// cannot make a question at all.
    /// </exception>
    /// <remarks>
    /// The one place a query is composed out of a shelf. A refresh that built
    /// its own would be the second copy of the cap and of the question
    /// spelling, and the drift would show as a shelf fetching more titles than
    /// it may hold.
    ///
    /// The limit is this shelf's cap rather than the source's page. Asking for
    /// more than may be stored spends a request on titles that are discarded,
    /// and asking for fewer is a shelf that never fills.
    /// </remarks>
    public SourceQuery Ask(int? startIndex = null)
    {
        Validated();

        return new SourceQuery(Spelling(Question), Kind, startIndex, Cap).Validated();
    }

    /// <summary>
    /// Refuses a shelf that could not be asked for anything.
    /// </summary>
    /// <returns>The same shelf, so a caller can validate and use it in one step.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the question, the kind or the source is the unset member, or
    /// when the cap is zero or negative.
    /// </exception>
    /// <remarks>
    /// What this can decide is a shelf's own shape. What it cannot decide is
    /// whether the named source answers this question for this kind: nothing on
    /// <see cref="IMetadataSource"/> asks that, and finding out means issuing a
    /// real fetch, which needs a credential and spends a source's budget on a
    /// question about configuration. That half of #85's second condition is
    /// recorded on the issue rather than pretended to here.
    /// </remarks>
    public Shelf Validated()
    {
        if (Question is ShelfQuestion.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Question),
                Question,
                "A shelf asks a source for something in particular. None is what an unset field reads as, and a source handed it would have to choose the question.");
        }

        if (Kind is not (DiscoverTitleKind.Movie or DiscoverTitleKind.Series))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Kind),
                Kind,
                "A shelf holds one kind of title. None is what an unset field reads as, and most sources ask for films and series through different questions.");
        }

        if (Source is MetadataSource.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Source),
                Source,
                "A shelf names the source that answers it. None is what an unset field reads as, and a shelf whose source is chosen later changes contents when an operator adds a second one.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_cap, nameof(Cap));

        return this;
    }

    /// <summary>
    /// Refuses a shelf no source a server has set up can answer.
    /// </summary>
    /// <param name="activeSources">The sources this server is configured to ask.</param>
    /// <returns>The same shelf.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="activeSources"/> is null, or holds a null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown by <see cref="Validated"/>, or when no active source speaks for
    /// this shelf's <see cref="Source"/>. The message names the source that is
    /// missing, because "no source" leaves an operator with two configured
    /// sources guessing which shelf is the orphan.
    /// </exception>
    /// <remarks>
    /// Half of #85's second condition, and the half that is decidable without
    /// asking anybody. It is the same shape as <see cref="CatalogueRetention"/>
    /// takes for the retention ceiling: a rule expressed once, taking what a
    /// server actually has, so that the save path calls it rather than each
    /// subsystem checking at its own moment.
    ///
    /// It is not called from anywhere yet, and the reason is no longer the one
    /// this remark gave. There is a save path. <c>Plugin.UpdateConfiguration</c>
    /// refuses a schema this build does not know, an unreadable entry on the
    /// list of users who may not ask, and a bound the shipped shelves do not fit
    /// inside. What it cannot do is supply the argument: the server constructs a
    /// plugin with an application-paths and an XML-serializer instance and
    /// nothing else, so that route has no way of asking the container what it
    /// holds, and nothing registers an <see cref="IMetadataSource"/> there to be
    /// asked about, which <c>PluginServiceRegistrator</c> says of itself. A call
    /// added at the save today would judge every shipped shelf against an empty
    /// set and refuse every save on every install. So what is missing is a
    /// configured source rather than a save, and that is recorded on #85 and
    /// #105 rather than worked around with a caller invented to give this a
    /// reference.
    /// </remarks>
    public Shelf ValidatedAgainst(IReadOnlyCollection<IMetadataSource> activeSources)
    {
        ArgumentNullException.ThrowIfNull(activeSources);

        Validated();

        foreach (var source in activeSources)
        {
            ArgumentNullException.ThrowIfNull(source, nameof(activeSources));

            if (source.Source == Source)
            {
                return this;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(activeSources),
            FormattableString.Invariant(
                $"The shelf {DisplayName} names {Source}, which is not among the sources this server is set up to ask. Either that source is configured or the shelf is turned off; a shelf left naming a source nobody asks is a row that is empty for a reason no operator can see."));
    }

    /// <summary>
    /// How a question is spelled to an adapter.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <returns>The name a <see cref="SourceQuery"/> carries.</returns>
    /// <remarks>
    /// The one place the vocabulary crosses from this plugin's own set into the
    /// string <see cref="SourceQuery"/> carries, which is what every adapter
    /// keys on. That the three spellings here are the three the shipped adapter
    /// answers is asserted by the suite rather than left to whoever adds the
    /// fourth: a member added here and not there is a shelf that is empty on
    /// every server.
    /// </remarks>
    private static string Spelling(ShelfQuestion question) => question switch
    {
        ShelfQuestion.Trending => "trending",
        ShelfQuestion.Popular => "popular",
        ShelfQuestion.TopRated => "top-rated",
        _ => throw new ArgumentOutOfRangeException(
            nameof(question),
            question,
            "A shelf question has a spelling every adapter reads. None is what an unset field reads as.")
    };
}
