using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A logger that keeps the lines written to it, so a test can assert what an
/// operator would have been told.
/// </summary>
/// <typeparam name="T">Whatever the logger is for.</typeparam>
/// <remarks>
/// The lines are kept formatted rather than as templates and arguments,
/// because what matters to the condition being tested is what a person reading
/// the log sees. A template asserted on its own passes while the argument that
/// names the file is missing from it.
///
/// Every level is enabled. A test asserting that something was logged should
/// fail when the statement is removed, not when a level filter somewhere
/// happens to be set differently from the server's.
/// </remarks>
internal sealed class LoggerThatRecordsWhatIsWritten<T> : ILogger<T>
{
    private readonly List<string> _lines = new List<string>();

    /// <summary>
    /// Gets the lines written so far, oldest first.
    /// </summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        _lines.Add(formatter(state, exception));
    }
}
