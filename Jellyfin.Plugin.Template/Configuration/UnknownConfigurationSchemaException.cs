using System;
using System.Globalization;

namespace Jellyfin.Plugin.Template.Configuration;

/// <summary>
/// Thrown when a configuration document declares a schema version this build
/// cannot read.
/// </summary>
/// <remarks>
/// Its own type rather than a bare <see cref="InvalidOperationException"/>,
/// because the caller that has to tell an operator what to do about it needs to
/// separate this from every other reason a save can fail.
/// </remarks>
public class UnknownConfigurationSchemaException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnknownConfigurationSchemaException"/> class.
    /// </summary>
    /// <param name="foundSchemaVersion">The schema version the document declared.</param>
    public UnknownConfigurationSchemaException(int foundSchemaVersion)
        : base(string.Format(
            CultureInfo.InvariantCulture,
            "This configuration declares schema version {0} and this build reads version {1}. It has been refused rather than read as if it were version {1}, because a document from another version can mean something different field by field. Install a build that knows version {0}, or remove the configuration and set it up again.",
            foundSchemaVersion,
            PluginConfiguration.CurrentSchemaVersion))
    {
        FoundSchemaVersion = foundSchemaVersion;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnknownConfigurationSchemaException"/> class.
    /// </summary>
    public UnknownConfigurationSchemaException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnknownConfigurationSchemaException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public UnknownConfigurationSchemaException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnknownConfigurationSchemaException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public UnknownConfigurationSchemaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets the schema version the refused document declared.
    /// </summary>
    public int FoundSchemaVersion { get; }
}
