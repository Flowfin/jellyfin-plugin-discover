namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The three headers this plugin puts on a call, copied off one request.
/// </summary>
/// <param name="Authorization">What the request presented as a credential, or the empty string.</param>
/// <param name="UserAgent">What the request called this application, or the empty string.</param>
/// <param name="Accept">What the request said it would take back, or the empty string.</param>
/// <remarks>
/// A copy rather than the request itself, because the client disposes a request
/// once its call is over and a test reading one afterwards would be reading
/// something already released. Three named strings rather than the header
/// collection, so an assertion about one of them says which one it is about in
/// the assertion rather than in a lookup beside it.
///
/// It is here rather than beside the handler that fills it because
/// `no-network-outside-source-adapter` excepts that file by path for naming a
/// request type, and this names none: a type that can live under the rule lives
/// under it.
/// </remarks>
internal sealed record HeadersOnOneRequest(string Authorization, string UserAgent, string Accept);
