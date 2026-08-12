# What a downgrade does, and what an upgrade is claimed to do

Raised by
[#107](https://github.com/Flowfin/jellyfin-plugin-discover/issues/107), whose
fourth condition asks for a downgrade to be stated as supported or not
supported, with what happens either way.

This plugin reads two documents that outlive a build. The configuration the
server holds for it, and the catalogue it writes for itself. Both carry a
version, both refuse a version the running build does not know, and they refuse
it in different places, which is the whole of why this page exists rather than a
sentence in each of two files.

## A downgrade is not supported

Installing an older build over a newer one leaves both documents ahead of the
build reading them. Neither is read as though the formats agreed, which is the
part that is deliberate, and one of the two loses data on a server rather than
in a build, which is the part to know before doing it.

Keep a copy of the configuration file before installing an older build. The
catalogue needs nothing kept: it is derived data and the next refresh writes it
again.

## What the catalogue does

A catalogue document names its format on its first line, and a version that is
not this build's is refused in both directions:

    git grep -n 'Family = \|CurrentVersion = ' -- Jellyfin.Plugin.Template/Catalogue/CatalogueDocumentFormat.cs
    Jellyfin.Plugin.Template/Catalogue/CatalogueDocumentFormat.cs:48:    public const string Family = "discover-catalogue/";
    Jellyfin.Plugin.Template/Catalogue/CatalogueDocumentFormat.cs:53:    public const int CurrentVersion = 1;

A refusal means the catalogue is absent rather than wrong. Whatever asks for a
shelf gets nothing back, and the reason goes to the log with both version
numbers and what to do about it. The next refresh this build completes replaces
the document with one it wrote itself, so a downgrade costs a refresh and
nothing else.

The behaviour and its two directions are held by tests rather than by this page:

    git grep -n 'public void' -- Jellyfin.Plugin.Template.Tests/CatalogueDocumentVersionTests.cs
    Jellyfin.Plugin.Template.Tests/CatalogueDocumentVersionTests.cs:36:    public void ADocumentFromTheVersionThisBuildWritesIsRead()
    Jellyfin.Plugin.Template.Tests/CatalogueDocumentVersionTests.cs:65:    public void ADocumentANewerBuildWroteIsRefusedAndBothVersionsAreNamed()
    Jellyfin.Plugin.Template.Tests/CatalogueDocumentVersionTests.cs:98:    public void ADocumentFromAnOlderFormatIsRefusedAndSaysNothingMigratesIt()
    Jellyfin.Plugin.Template.Tests/CatalogueDocumentVersionTests.cs:132:    public void AFirstLineThatNamesNoVersionIsNotReportedAsAVersion()
    Jellyfin.Plugin.Template.Tests/CatalogueDocumentVersionTests.cs:164:    public void AShortDocumentFromANewerBuildIsAVersionRatherThanATruncation()
    Jellyfin.Plugin.Template.Tests/CatalogueDocumentVersionTests.cs:208:    public void AMarkerLineThatIsNearlyThisFormatIsNotReadAsIt(string markerLine)
    Jellyfin.Plugin.Template.Tests/CatalogueDocumentVersionTests.cs:218:    public void TheMarkerThisBuildWritesNamesTheVersionItReads()
    Jellyfin.Plugin.Template.Tests/CatalogueDocumentVersionTests.cs:230:    public void ThereIsNoReasonToGiveForTheVersionThisBuildReads()

## What the configuration does, and where it stops

The configuration carries a schema version, and the rule refuses any version
that is not the current one, in both directions:

    git grep -n 'public const int CurrentSchemaVersion' -- Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs
    Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs:23:    public const int CurrentSchemaVersion = 1;

The rule runs on one route, and that route is the save:

    git grep -n 'ThrowIfUnknown' -- 'Jellyfin.Plugin.Template/*.cs'
    Jellyfin.Plugin.Template/Configuration/ConfigurationSchema.cs:27:    public static void ThrowIfUnknown(PluginConfiguration configuration)
    Jellyfin.Plugin.Template/Plugin.cs:52:            ConfigurationSchema.ThrowIfUnknown(pluginConfiguration);

So a downgraded build refuses to write a document whose version it does not
know. It does not refuse to read one, and it cannot: the read is the server's
and it is not a route a plugin can reach. The method is private and there is no
override beside it, on either line this repository builds against:

    git grep -n 'private TConfigurationType LoadConfiguration' v10.11.11 v12.0-rc4 -- MediaBrowser.Common/Plugins/BasePluginOfT.cs
    v10.11.11:MediaBrowser.Common/Plugins/BasePluginOfT.cs:184:        private TConfigurationType LoadConfiguration()
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePluginOfT.cs:184:        private TConfigurationType LoadConfiguration()

read at

    git -C <a jellyfin checkout> rev-parse v10.11.11 v12.0-rc4
    1fbd8739292cce610231be93daf43368733edf63
    b3a06113029585594fe7a44becbfae7d2bdd9974

That leaves a downgrade in one of two states, and they are not equally bad.

Where the older build can still deserialize the document, it runs on what it
understands of it. Settings the older build does not declare are read past, the
version it does not know sits in memory, and the first save from the dashboard
is refused with the message naming both versions. Nothing is lost on disk and
the operator is told, late, at the moment they try to change something.

Where the older build cannot deserialize the document at all, the server does
not stop at the read. It builds a default configuration and writes it back over
the file:

    git grep -n -A4 'catch$' v10.11.11 v12.0-rc4 -- MediaBrowser.Common/Plugins/BasePluginOfT.cs
    v10.11.11:MediaBrowser.Common/Plugins/BasePluginOfT.cs:192:            catch
    v10.11.11:MediaBrowser.Common/Plugins/BasePluginOfT.cs-193-            {
    v10.11.11:MediaBrowser.Common/Plugins/BasePluginOfT.cs-194-                var config = Activator.CreateInstance<TConfigurationType>();
    v10.11.11:MediaBrowser.Common/Plugins/BasePluginOfT.cs-195-                SaveConfiguration(config);
    v10.11.11:MediaBrowser.Common/Plugins/BasePluginOfT.cs-196-                return config;
    --
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePluginOfT.cs:192:            catch
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePluginOfT.cs-193-            {
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePluginOfT.cs-194-                var config = Activator.CreateInstance<TConfigurationType>();
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePluginOfT.cs-195-                SaveConfiguration(config);
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePluginOfT.cs-196-                return config;

So the operator's settings are replaced by defaults rather than refused, before
this plugin has been asked anything, and no rule written here can reach that.
This is the reason the sentence at the top of this page is a plain no rather
than a qualified one.

Where the rule for the configuration ought to run on the read as well, and what
it would have to keep in order to fall back to a previous valid document, is
[#105](https://github.com/Flowfin/jellyfin-plugin-discover/issues/105).

## What an upgrade is claimed to do

Nothing has been released, so nothing has ever been upgraded:

    git ls-remote --tags origin
    gh release list --repo Flowfin/jellyfin-plugin-discover --limit 5

Both answer with nothing. Every sentence about an upgrade is therefore read out
of the code rather than out of a run, and this page marks it as a claim rather
than a measurement.

The claim is that an upgrade to a build carrying a higher version of either
document reads what is there, because the document on disk is then the older
one, and the two rules split at exactly that point: a newer document is refused
and an older one is migrated where a migration exists. No migration exists for
either document today, because neither has had a second version, so the older
direction is refused with a message saying so rather than read field by field.

The upgrade that this plan expects to hurt is not a version of a document. It is
the folder the catalogue sits in, which the base plugin class derives from the
loaded assembly and can append a version to. That hazard is written where the
decision was taken, in
[`docs/decisions/0003-the-catalogue-lives-in-the-plugins-own-data-folder.md`](decisions/0003-the-catalogue-lives-in-the-plugins-own-data-folder.md),
and it belongs to #107 rather than to this page.

## What none of this covers

Items in the library database. This plugin writes none yet, so an upgrade or a
downgrade moves nothing there, and the day that changes is the day the identity
those items are keyed on decides what a downgrade costs.

A real upgrade run. The first condition of #107 asks for one over a previous
release in a container harness, and there is neither a previous release nor the
harness.

Whether a version on the document is enough once a document's payload has a
shape of its own. The catalogue is a container around bytes today, and what goes
inside it is
[#87](https://github.com/Flowfin/jellyfin-plugin-discover/issues/87).

Whether an operator can get back to a newer build after downgrading. Nothing
here prevents it, and nothing here has tried it.
