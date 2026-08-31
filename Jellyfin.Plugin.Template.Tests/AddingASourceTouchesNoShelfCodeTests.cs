using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Refresh;
using Jellyfin.Plugin.Template.Shelves;
using Jellyfin.Plugin.Template.Sources;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A second source is served by the code that serves the first, and nothing
/// that decides a shelf knows either of them by name.
/// </summary>
/// <remarks>
/// #73's fourth condition. It asks that adding a source be adding an adapter
/// and a registration, with no edit to the shelf code, proven by a second
/// adapter in the test project that exists only for that purpose.
/// <see cref="ASourceThisPluginHasNoAdapterFor"/> is that adapter and this file
/// is the proof.
///
/// Two assertions, because the condition has two halves and the behavioural one
/// alone is weaker than it looks. A refresh serving a second source shows that
/// this arrangement of shelves worked; it does not show that no shelf code
/// names a source, which is what makes the next source cheap. The second
/// assertion is over the types rather than over a run, so a field of the
/// adapter's own type added to the refresh tomorrow fails here rather than
/// being noticed when a third source arrives.
/// </remarks>
public class AddingASourceTouchesNoShelfCodeTests
{
    private const string TestFolders = "jellyfin-plugin-discover-tests";

    private static readonly DateTimeOffset _fetchedAt =
        new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A shelf naming a source this plugin ships no adapter for is refreshed by
    /// the same refresh, and its titles reach its own document.
    /// </summary>
    /// <remarks>
    /// The shelf is built the way any shelf is built, the refresh is handed
    /// both sources the way a container would hand it whatever it holds, and
    /// nothing in `Shelves/` or `Refresh/` was edited to make this pass. The
    /// first source is in the set as well, because a run with only the new one
    /// would not show that the two are told apart.
    /// </remarks>
    /// <returns>A <see cref="Task"/> that completes when the assertion has been made.</returns>
    [Fact]
    public async Task ASecondSourceIsServedByTheSameRefresh()
    {
        var folder = Folder("second-source");
        Remove(folder);
        try
        {
            var theirs = Row(MetadataSource.Tvdb, ShelfQuestion.Popular);
            var ours = Row(MetadataSource.Tmdb, ShelfQuestion.Trending);

            var second = new ASourceThisPluginHasNoAdapterFor();
            second.Answers("popular", new[] { Title("A series the other source has never heard of", "series-1") });

            var first = new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb);
            first.Answer(ours.Ask(), SourceAnswer.Answered(new[] { Title("A film", "film-1") }, totalCount: 1));

            var store = Store(folder);

            var run = await new CatalogueRefresh(
                new IMetadataSource[] { first, second },
                store,
                null,
                new ClockATestAdvances(_fetchedAt),
                new LoggerThatRecordsWhatIsWritten<CatalogueRefresh>())
                .RunAsync(new[] { ours, theirs }, progress: null, CancellationToken.None);

            Assert.All(run.Shelves, result => Assert.Equal(ShelfRefreshOutcome.Refreshed, result.Outcome));

            Assert.Equal(
                "A series the other source has never heard of",
                CatalogueDocumentBody.Read(store.Read(CatalogueLayout.DocumentName(theirs))!).Single().Name,
                StringComparer.Ordinal);

            // Each source was asked its own shelf's question and no other, which
            // is what tells a refresh serving two sources from one serving one
            // twice.
            Assert.Equal("popular", Assert.Single(second.Asked).Name, StringComparer.Ordinal);
            Assert.Equal("trending", Assert.Single(first.Asked).Name, StringComparer.Ordinal);
        }
        finally
        {
            Remove(folder);
        }
    }

    /// <summary>
    /// Nothing that decides a shelf or runs a refresh names a source
    /// implementation.
    /// </summary>
    /// <remarks>
    /// The half the run cannot show. What makes adding a source cheap is that
    /// the code between a shelf and a document knows the interface and no
    /// implementation of it, so a constructor parameter, a field or a return of
    /// a concrete adapter's type is the edit this condition exists against.
    ///
    /// Derived rather than listed: what is refused is every type in this
    /// plugin's assembly that implements the interface, so an adapter added
    /// tomorrow is in the set without anybody adding it here. The interface
    /// itself is allowed, which is the whole point of it.
    ///
    /// It reads the two namespaces the condition names as the shelf code, which
    /// is where a shelf is decided and where a refresh runs. The adapter's own
    /// namespace is outside it, because a source naming itself is not the
    /// coupling this is about.
    /// </remarks>
    [Fact]
    public void NoShelfOrRefreshTypeNamesASourceImplementation()
    {
        var plugin = typeof(CatalogueRefresh).Assembly;

        var implementations = plugin.GetTypes()
            .Where(type => typeof(IMetadataSource).IsAssignableFrom(type) && type != typeof(IMetadataSource))
            .ToArray();

        Assert.NotEmpty(implementations);

        var shelfCode = plugin.GetTypes()
            .Where(type => type.Namespace is "Jellyfin.Plugin.Template.Shelves" or "Jellyfin.Plugin.Template.Refresh")
            .ToArray();

        Assert.NotEmpty(shelfCode);

        var named = new List<string>();

        foreach (var type in shelfCode)
        {
            foreach (var mentioned in Mentions(type))
            {
                if (implementations.Contains(mentioned))
                {
                    named.Add(type.Name + " names " + mentioned.Name);
                }
            }
        }

        Assert.True(
            named.Count == 0,
            "Adding a source has to be adding an adapter and a registration, and the code that decides a shelf has to know the "
            + "interface and no implementation of it. These name one: " + string.Join(", ", named)
            + ". That is #73's fourth condition, and the repair is to take the concrete type out rather than to widen this test.");
    }

    /// <summary>
    /// Every type a type's own surface mentions.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The types its fields, properties, constructors and methods name.</returns>
    /// <remarks>
    /// Non-public members are read too, because a private field holding an
    /// adapter is exactly the coupling this is about and is the one a public
    /// walk would miss.
    /// </remarks>
    private static IEnumerable<Type> Mentions(Type type)
    {
        const BindingFlags Everything =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        foreach (var field in type.GetFields(Everything))
        {
            yield return field.FieldType;
        }

        foreach (var property in type.GetProperties(Everything))
        {
            yield return property.PropertyType;
        }

        foreach (var constructor in type.GetConstructors(Everything))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }

        foreach (var method in type.GetMethods(Everything))
        {
            yield return method.ReturnType;

            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }
    }

    private static Shelf Row(MetadataSource source, ShelfQuestion question) => new Shelf
    {
        DisplayName = "A row from " + source,
        Question = question,
        Kind = DiscoverTitleKind.Movie,
        Source = source,
        Cap = 3
    };

    private static DiscoverTitle Title(string name, string identifier) => new DiscoverTitle
    {
        Kind = DiscoverTitleKind.Movie,
        Name = name,
        FetchedAt = _fetchedAt,
        Identity = new DiscoverTitleIdentity(new[]
        {
            new ProviderIdentifier(MetadataSource.Tmdb, identifier)
        })
    };

    private static CatalogueDocumentStore Store(string folder) =>
        new CatalogueDocumentStore(
            new CatalogueDirectory(folder),
            new LoggerThatRecordsWhatIsWritten<CatalogueDocumentStore>());

    private static string Folder(string name) => Path.Combine(Path.GetTempPath(), TestFolders, name);

    private static void Remove(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }
}
