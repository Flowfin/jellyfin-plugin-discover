using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// Pins the plugin identifier, in the assembly and in the packaging metadata.
/// </summary>
/// <remarks>
/// The identifier is the one value in this plugin that can never be corrected.
/// A server keys an installed plugin, its configuration file and its dashboard
/// entry on it, so moving it after anybody has installed the plugin does not
/// rename that install, it produces a second plugin beside it and orphans the
/// first. That is the duplication hazard in #107, and unlike the other two
/// there is no upgrade path that repairs it.
///
/// Nothing refused a change to it before this file. The value appears twice in
/// the tree, once as the assembly's identity and once in the packaging
/// metadata, and neither was compared with the other or with anything fixed, so
/// an edit to either one compiled, packaged and installed. What it produced on
/// a server is the failure above, at the moment a user upgraded rather than at
/// the moment somebody typed it.
/// </remarks>
public class PluginIdentifierTests
{
    /// <summary>
    /// The identifier minted for this plugin, written here as a literal on
    /// purpose.
    /// </summary>
    /// <remarks>
    /// A test deriving the expected value from the same place the code reads it
    /// asserts that a value equals itself. The literal is the whole point: it
    /// is a second, independent record of what the identifier is, so a change
    /// to the plugin has to change this file too and cannot be made absently.
    /// </remarks>
    private const string MintedIdentifier = "8227de33-0101-48a3-951d-2bf921709e48";

    /// <summary>
    /// The assembly the server loads carries the minted identifier.
    /// </summary>
    [Fact]
    public void ThePluginCarriesTheIdentifierItWasMintedWith()
    {
        var plugin = new Plugin(
            new ApplicationPathsThatRefuseEveryCallButThePluginDirectories(),
            new XmlSerializerThatRefusesEveryCall());

        Assert.Equal(
            MintedIdentifier,
            plugin.Id.ToString("D", CultureInfo.InvariantCulture),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The packaging metadata declares the same identifier the assembly carries.
    /// </summary>
    /// <remarks>
    /// These two are read by different things at different times. The server
    /// reads the assembly's identifier when it loads the plugin; the manifest a
    /// user installs from carries the one in build.yaml. A difference between
    /// them installs cleanly and then presents as a plugin the catalogue
    /// believes is installed and the server has never heard of, which is why
    /// this is compared rather than assumed from the two values having been
    /// typed at the same time.
    /// </remarks>
    [Fact]
    public void BuildYamlDeclaresTheIdentifierTheAssemblyCarries()
    {
        var declared = DeclaredIdentifier();

        Assert.Equal(MintedIdentifier, declared, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads the identifier out of the packaging metadata.
    /// </summary>
    /// <returns>The value of the manifest's guid key.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the manifest declares no guid, so an absent key fails the
    /// test that reads it rather than passing as an empty comparison.
    /// </exception>
    private static string DeclaredIdentifier()
    {
        var line = File.ReadAllLines(PackagedPluginIdentityTests.RepositoryFile("build.yaml"))
            .FirstOrDefault(candidate => candidate.StartsWith("guid:", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("build.yaml declares no guid, so there is no identifier for the package to carry.");

        return line["guid:".Length..].Trim().Trim('"');
    }
}
