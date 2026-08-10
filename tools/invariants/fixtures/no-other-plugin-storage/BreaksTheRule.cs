// Reads a neighbouring plugin's files, and then its types, without ever naming
// the neighbour in a project file. The neighbour here is not the sibling this
// plan has a seam to: that name belongs to no-sibling-plugin-reference's own
// fixture, and a fixture breaking two rules would redden both.
namespace Fixture;

using System;
using System.IO;
using System.Reflection;
using MediaBrowser.Common.Configuration;

public sealed class BreaksTheRule
{
    private readonly IApplicationPaths _paths;

    public BreaksTheRule(IApplicationPaths paths)
    {
        _paths = paths;
    }

    public string NeighboursData()
        => Path.Combine(_paths.PluginsPath, "Jellyfin.Plugin.Neighbour", "catalogue");

    public string NeighboursConfiguration()
        => Path.Combine(_paths.PluginConfigurationsPath, "Neighbour.xml");

    public Type? NeighboursRecord()
    {
        var assembly = Assembly.LoadFrom(Path.Combine(NeighboursData(), "Jellyfin.Plugin.Neighbour.dll"));
        return assembly.GetType("Jellyfin.Plugin.Neighbour.Record");
    }
}
