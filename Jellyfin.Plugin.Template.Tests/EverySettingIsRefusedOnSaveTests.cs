using System;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Template.Configuration;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// Every setting on the configuration is placed against the save path: either a
/// refusal covers it, or no value of it can be invalid and that is written down.
/// </summary>
/// <remarks>
/// #105's second condition says the bounds, the retention ceiling of #68 and the
/// shelf definitions of #85 are validated at the save rather than each in its own
/// place, and the decision recorded there on 2026-09-04 argues that the two that
/// do not exist yet come under the same rule on the day they land, by
/// construction, since the rule is on the type. That argument was true of nothing
/// that ran. <c>Plugin.UpdateConfiguration</c> names its refusals one at a time,
/// so a property added tomorrow is validated when somebody adds a refusal for it
/// and not before, and nothing said so.
///
/// This is that claim made refusable, in the shape
/// <see cref="ConfigurationPageTests"/> already uses for the page: two lists,
/// every property in exactly one of them, and both lists naming only properties
/// that exist. A setting added with neither a refusal nor a reason reddens here,
/// which is the moment somebody is deciding what it may hold.
///
/// WHAT THIS CANNOT DO, and it is the larger half. It reads that a property is
/// PLACED, never that the refusal named beside it bites, and never that the
/// refusal is the right one. What proves each refusal is the test written for it:
/// <see cref="ConfigurationSchemaTests"/> for the schema version,
/// <see cref="CatalogueBoundsTests"/> for the two numbers, and the tests over
/// <c>WhoMayAsk</c> for the refused-ask list. So a property moved into the
/// unfalsifiable list to silence this passes, exactly as a setting added to
/// <c>HiddenFromThePage</c> silences the page check, and the remark beside the
/// entry is what a reader has instead.
///
/// It also says nothing about the load path. That is #106's, decided on
/// 2026-09-05, and the clause about a previous valid configuration went with it.
/// </remarks>
public class EverySettingIsRefusedOnSaveTests
{
    /// <summary>
    /// Settings a refusal on the save path covers, and which refusal covers
    /// each.
    /// </summary>
    /// <remarks>
    /// SchemaVersion is refused by ConfigurationSchema.ThrowIfUnknown, which
    /// takes a document declaring any version this build does not know.
    ///
    /// MaximumTitlesPerShelf and MaximumTitlesAcrossAllShelves are refused by
    /// CatalogueBounds.Of, reached through PluginConfiguration.Bounds, which
    /// takes a number outside its range and a pair that contradicts itself. The
    /// same call also refuses a pair the shipped shelves do not fit inside,
    /// through ThrowIfShelvesDoNotFit.
    ///
    /// UsersRefusedTheAsk is refused by WhoMayAsk.ThrowIfAnEntryIsUnreadable,
    /// which takes an entry that is not a user identifier.
    /// </remarks>
    private static readonly string[] RefusedOnSave =
    [
        "MaximumTitlesAcrossAllShelves",
        "MaximumTitlesPerShelf",
        "SchemaVersion",
        "UsersRefusedTheAsk"
    ];

    /// <summary>
    /// Settings no value of which can be invalid, so there is nothing for a
    /// refusal to take.
    /// </summary>
    /// <remarks>
    /// Both are booleans, and both of their values are meaningful: Enabled off
    /// is a plugin that fetches nothing, and IncludeAdultTitles off is the
    /// exclusion that is the default. A refusal here would have to refuse one of
    /// the two answers the type offers, which is a different rule from
    /// validation.
    ///
    /// This list is the one to be suspicious of. It is where a setting goes when
    /// somebody does not want to write a refusal, and nothing here can tell that
    /// apart from a type with no invalid value. Adding to it is a claim about the
    /// type, so it carries the type's name in the entry's reason and not a
    /// judgement about the setting's importance.
    /// </remarks>
    private static readonly string[] NoValueOfItIsInvalid =
    [
        "Enabled",
        "IncludeAdultTitles"
    ];

    /// <summary>
    /// Every setting is in exactly one of the two lists.
    /// </summary>
    /// <remarks>
    /// This is the direction that bites as the plugin grows. #68's retention
    /// ceiling and #85's shelf definitions are settings that do not exist yet,
    /// and each one arrives as a property here: on the day it lands, this fails
    /// until somebody places it.
    /// </remarks>
    [Fact]
    public void EverySettingIsRefusedOnSaveOrIsRecordedAsHavingNoInvalidValue()
    {
        foreach (var property in ConfigurationPropertyNames())
        {
            var refused = RefusedOnSave.Contains(property, StringComparer.Ordinal);
            var unfalsifiable = NoValueOfItIsInvalid.Contains(property, StringComparer.Ordinal);

            Assert.True(
                refused || unfalsifiable,
                $"{property} is a setting this build stores and neither list places it. Either a refusal on the save path covers it, and it goes in RefusedOnSave with the refusal named, or no value of it can be invalid, and it goes in NoValueOfItIsInvalid with the reason.");

            Assert.False(
                refused && unfalsifiable,
                $"{property} is in both lists. A setting a refusal covers is not a setting with no invalid value, and the second list is what silences the first.");
        }
    }

    /// <summary>
    /// Both lists name only settings that exist.
    /// </summary>
    /// <remarks>
    /// Without this, a setting removed from the configuration leaves its name
    /// behind and the lists slowly become a place where the check above can be
    /// silenced by accident. It is the same failure the page's own pair guards
    /// against, one register over.
    /// </remarks>
    [Fact]
    public void NeitherListNamesASettingThatIsGone()
    {
        var properties = ConfigurationPropertyNames();

        foreach (var named in RefusedOnSave.Concat(NoValueOfItIsInvalid))
        {
            Assert.Contains(named, properties, StringComparer.Ordinal);
        }
    }

    private static string[] ConfigurationPropertyNames()
    {
        return typeof(PluginConfiguration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .ToArray();
    }
}
