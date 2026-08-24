// Passes a source's content on, in the two shapes the terms page names as
// refused and this rule can see: an export of the catalogue off the server, the
// upload that feeds a catalogue shared between installations, and a telemetry
// payload carrying fields a response supplied.
//
// It reaches no network type and names no server type, because a fixture that
// broke a neighbouring rule as well would redden both.
namespace Fixture;

using System.Collections.Generic;
using System.Text;

public sealed class BreaksTheRule
{
    private readonly IReadOnlyList<string> _catalogue;

    public BreaksTheRule(IReadOnlyList<string> catalogue)
    {
        _catalogue = catalogue;
    }

    public string ExportCatalogue()
    {
        var written = new StringBuilder();

        foreach (var title in _catalogue)
        {
            written.Append(title).Append('\n');
        }

        return written.ToString();
    }

    public string UploadToTheSharedIndex(string endpoint)
        => endpoint + "?payload=" + ExportCatalogue();

    public string TelemetryPayload()
        => "titles=" + ExportCatalogue();
}
