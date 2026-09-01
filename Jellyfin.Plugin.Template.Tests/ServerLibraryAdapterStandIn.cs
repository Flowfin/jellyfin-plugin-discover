using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A server library that answers the two questions this plugin asks and refuses every other member.
/// </summary>
/// <remarks>
/// <para>
/// NOT HAND WRITTEN, AND THAT IS THE DEPARTURE THIS FILE EXISTS TO ARGUE FOR.
/// Every other stand-in here is written out, because the interface behind it
/// has one or two members and what a reader meets is C# rather than a second
/// vocabulary. The server's library interface is not that shape:
/// </para>
/// <code>
/// git show v10.11.11:MediaBrowser.Controller/Library/ILibraryManager.cs | grep -cE '^[[:space:]]+(public )?[A-Za-z].*\(.*\);'
/// 81
/// </code>
/// <para>
/// A written-out version would be eighty-one members that throw and two that
/// answer, naming several dozen server types the rest of this suite is kept
/// away from on purpose, and a reader would learn nothing from it. It would
/// also go stale in the one direction that matters: a member added to that
/// interface on a later line breaks the file rather than being refused by it.
/// </para>
/// <para>
/// What replaces it is the runtime's own proxy rather than a mocking package.
/// <see cref="DispatchProxy"/> ships in the runtime the server already runs on,
/// it adds no package to this suite, and what it buys is exactly the sentence
/// this type is for: two members answer and everything else is a call no test
/// set up. That is the same shape as
/// <c>ServerApplicationHostThatRefusesEveryCall</c> and
/// <c>ATransportThatRefusesWhatNoTestSetUp</c>, which is the convention here,
/// and it is why this file is not a mocking framework arriving by the side
/// door: nothing is arranged on it by name and no expectation is verified
/// through it. The arrangement is two delegates and the assertions are made on
/// what the adapter did with what they returned.
/// </para>
/// <para>
/// The file is named for the seam rather than for what it does, which is the
/// one departure from this suite's naming convention and is deliberate.
/// `no-channel-type-outside-surface` has every C# file as its subject, so a
/// stand-in for a server interface has to be excepted by name rather than left
/// outside a subject the way `no-server-type-in-a-test` leaves its fakes. One
/// pathspec reaching the adapter, its tests and this file is a rule about a
/// seam; three entries growing by one per stand-in is the list of today's
/// files that the neighbouring rule already argues against.
/// </para>
/// <para>
/// The two members are named through <see langword="nameof"/> rather than as
/// strings, so a rename on the server's interface stops this file compiling
/// instead of turning every question into a refusal at run time.
/// </para>
/// <para>
/// Not sealed, which every other type here is. <see cref="DispatchProxy"/>
/// generates a type deriving from this one and refuses a sealed base, so the
/// word is left off for a reason the runtime gives rather than by oversight.
/// </para>
/// </remarks>
#pragma warning disable CA1852 // Seal internal types - DispatchProxy derives from this one and refuses a sealed base.
internal class ServerLibraryAdapterStandIn : DispatchProxy
#pragma warning restore CA1852
{
    private readonly List<InternalItemsQuery> _asked = new List<InternalItemsQuery>();

    private Func<InternalItemsQuery, int>? _count;

    private Func<InternalItemsQuery, IReadOnlyList<Guid>>? _identifiers;

    /// <summary>
    /// Gets every query this library was handed, in the order it was handed them.
    /// </summary>
    /// <remarks>
    /// The questions rather than the answers, because #89's second condition is
    /// about what was asked. A test proving the lookup never goes by title text
    /// has to be able to read the query that was built.
    /// </remarks>
    public IReadOnlyList<InternalItemsQuery> Asked => _asked;

    /// <summary>
    /// Builds a library that answers the two questions with what a test supplies.
    /// </summary>
    /// <param name="count">What <c>GetCount</c> answers.</param>
    /// <param name="identifiers">What <c>GetItemIds</c> answers.</param>
    /// <param name="recorder">The proxy itself, so a test can read what it was asked.</param>
    /// <returns>The library, as the server's own interface.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either delegate is null.</exception>
    public static ILibraryManager Answering(
        Func<InternalItemsQuery, int> count,
        Func<InternalItemsQuery, IReadOnlyList<Guid>> identifiers,
        out ServerLibraryAdapterStandIn recorder)
    {
        ArgumentNullException.ThrowIfNull(count);
        ArgumentNullException.ThrowIfNull(identifiers);

        var library = DispatchProxy.Create<ILibraryManager, ServerLibraryAdapterStandIn>();

        recorder = (ServerLibraryAdapterStandIn)(object)library;
        recorder._count = count;
        recorder._identifiers = identifiers;

        return library;
    }

    /// <summary>
    /// Builds a library that refuses every member, including the two above.
    /// </summary>
    /// <returns>The library, as the server's own interface.</returns>
    /// <remarks>
    /// What the container tests hold something with. A registration that is
    /// merely constructed must ask the library nothing, and a stand-in that
    /// answered would let a construction that asked pass.
    /// </remarks>
    public static ILibraryManager RefusingEveryCall() =>
        DispatchProxy.Create<ILibraryManager, ServerLibraryAdapterStandIn>();

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        if (args is { Length: 1 } && args[0] is InternalItemsQuery query)
        {
            if (_count is { } count && string.Equals(targetMethod.Name, nameof(ILibraryManager.GetCount), StringComparison.Ordinal))
            {
                _asked.Add(query);

                return count(query);
            }

            if (_identifiers is { } identifiers && string.Equals(targetMethod.Name, nameof(ILibraryManager.GetItemIds), StringComparison.Ordinal))
            {
                _asked.Add(query);

                return identifiers(query);
            }
        }

        throw new NotSupportedException(string.Format(
            CultureInfo.InvariantCulture,
            "The server's library was asked {0}, which no test set up. A stand-in that answered a member nobody arranged would let a change reaching further into the server pass unnoticed.",
            targetMethod.Name));
    }
}
