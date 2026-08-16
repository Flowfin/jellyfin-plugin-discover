using System;

namespace Jellyfin.Plugin.Template.Sources;

/// <summary>
/// What came back over the wire, before any source's vocabulary is read out of it.
/// </summary>
/// <remarks>
/// The seam between the part of an adapter that talks and the part that reads.
/// It carries three things because those are the three an adapter has to tell
/// the four outcomes of <see cref="SourceOutcome"/> apart with: the status, the
/// body, and what the source said about waiting.
///
/// It exists so that the reading half can be tested. A test cannot supply a
/// fake for the talking half directly: the type it would have to name to do
/// that is refused in every file but an adapter's by
/// `no-network-outside-source-adapter`, which is exactly the rule keeping
/// outbound calls in one place. So the adapter takes a function returning this
/// record, the real one wraps the transport inside the adapter's own file, and
/// a test supplies one that returns bytes it wrote. That is the whole reason
/// this type is public rather than an implementation detail, and it is worth
/// knowing that a fixture-driven test therefore proves the parse and the
/// mapping and says nothing about the request that would have carried them.
///
/// Nothing here is any source's shape. A status is a number, a body is text,
/// and how long to wait is a span, so a second adapter over a source that
/// answers differently reuses this without either one learning about the other.
/// </remarks>
/// <param name="StatusCode">
/// What the source answered with. Zero where nothing answered at all, which is
/// the connection that never opened rather than a status any source sent.
/// </param>
/// <param name="Body">
/// What arrived, or null where nothing did. Not parsed and not trusted: a body
/// on a refusal is as likely to be a proxy's page as the source's own words.
/// </param>
/// <param name="RetryAfter">
/// How long the source said to wait, or null where it said nothing or said it
/// in a form this plugin does not read. Only ever meaningful beside a status
/// that refused for rate.
/// </param>
public readonly record struct SourceTransportReply(int StatusCode, string? Body, TimeSpan? RetryAfter);
