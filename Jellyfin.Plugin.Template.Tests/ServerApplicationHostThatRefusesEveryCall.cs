using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using MediaBrowser.Common;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A server host that refuses every call made to it.
/// </summary>
/// <remarks>
/// The server hands one of these to the registrator while it is still building
/// its container, so at that moment almost nothing on it is answerable yet. A
/// fake that returned plausible values would let a registration read one, pass
/// here, and fail on a real server. This one throws instead, so any use of the
/// host during registration is a test failure carrying the member name.
///
/// It is written out rather than generated, so what it refuses is visible in a
/// diff the next time the server's interface moves.
///
/// Every refusal is recorded before it is thrown. A registration that reaches
/// the host fails on the throw, so the log adds nothing there; what it adds is
/// the other direction, where a test asserts the log is empty and so states that
/// the registrator asked the host for nothing at all. That is a claim about a
/// run rather than about one member, and without the log it could only be made
/// by listing every member a test did not see fail.
/// </remarks>
internal sealed class ServerApplicationHostThatRefusesEveryCall : IServerApplicationHost
{
    private readonly CallLog _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerApplicationHostThatRefusesEveryCall"/> class,
    /// recording into a log of its own.
    /// </summary>
    public ServerApplicationHostThatRefusesEveryCall()
        : this(new CallLog())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerApplicationHostThatRefusesEveryCall"/> class.
    /// </summary>
    /// <param name="log">The log this fake records into, shared with the other fakes in the run.</param>
    public ServerApplicationHostThatRefusesEveryCall(CallLog log)
    {
        _log = log;
    }

    /// <inheritdoc />
    public event EventHandler? HasPendingRestartChanged
    {
        add => throw Refused();
        remove => throw Refused();
    }

    /// <inheritdoc />
    public string Name => throw Refused();

    /// <inheritdoc />
    public string SystemId => throw Refused();

    /// <inheritdoc />
    public bool HasPendingRestart => throw Refused();

    /// <inheritdoc />
    public bool ShouldRestart
    {
        get => throw Refused();
        set => throw Refused();
    }

    /// <inheritdoc />
    public Version ApplicationVersion => throw Refused();

    /// <inheritdoc />
    [AllowNull]
    public IServiceProvider ServiceProvider
    {
        get => throw Refused();
        set => throw Refused();
    }

    /// <inheritdoc />
    public string ApplicationVersionString => throw Refused();

    /// <inheritdoc />
    public string ApplicationUserAgent => throw Refused();

    /// <inheritdoc />
    public string ApplicationUserAgentAddress => throw Refused();

    /// <inheritdoc />
    public int HttpPort => throw Refused();

    /// <inheritdoc />
    public int HttpsPort => throw Refused();

    /// <inheritdoc />
    public bool ListenWithHttps => throw Refused();

    /// <inheritdoc />
    public string FriendlyName => throw Refused();

    /// <inheritdoc />
    public string? RestoreBackupPath
    {
        get => throw Refused();
        set => throw Refused();
    }

    /// <inheritdoc />
    public bool CoreStartupHasCompleted => throw Refused();

    /// <inheritdoc />
    public IEnumerable<Assembly> GetApiPluginAssemblies() => throw Refused();

    /// <inheritdoc />
    public void NotifyPendingRestart() => throw Refused();

    /// <inheritdoc />
    public IReadOnlyCollection<T> GetExports<T>(bool manageLifetime = true) => throw Refused();

    /// <inheritdoc />
    public IReadOnlyCollection<T> GetExports<T>(CreationDelegateFactory defaultFunc, bool manageLifetime = true) => throw Refused();

    /// <inheritdoc />
    public IEnumerable<Type> GetExportTypes<T>() => throw Refused();

    /// <inheritdoc />
    public T Resolve<T>() => throw Refused();

    /// <inheritdoc />
    public void Init(IServiceCollection serviceCollection) => throw Refused();

    /// <inheritdoc />
    public string ExpandVirtualPath(string path) => throw Refused();

    /// <inheritdoc />
    public string ReverseVirtualPath(string path) => throw Refused();

    /// <inheritdoc />
    public string GetSmartApiUrl(HttpRequest request) => throw Refused();

    /// <inheritdoc />
    public string GetSmartApiUrl(IPAddress remoteAddr) => throw Refused();

    /// <inheritdoc />
    public string GetSmartApiUrl(string hostname) => throw Refused();

    /// <inheritdoc />
    public string GetApiUrlForLocalAccess(IPAddress ipAddress, bool allowHttps = true) => throw Refused();

    /// <inheritdoc />
    public string GetLocalApiUrl(string hostname, string? scheme = null, int? port = null) => throw Refused();

    private InvalidOperationException Refused([CallerMemberName] string member = "")
    {
        _log.Record($"IServerApplicationHost.{member}");
        return new InvalidOperationException($"The registrator reached {member} on the server host. Registration runs while the server is still building its container, so nothing on the host is answerable yet.");
    }
}
