using System.Text.Json;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A caller that reads a source's own field names instead of asking the
/// interface in front of that source. It is here to be refused and is not
/// compiled by anything.
/// </summary>
internal static class BreaksTheRule
{
    public static string? Artwork(JsonElement entry) =>
        entry.GetProperty("poster_path").GetString();

    public static int Total(JsonElement root) =>
        root.GetProperty("total_results").GetInt32();
}
