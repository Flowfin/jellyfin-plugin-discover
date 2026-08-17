using System;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// One recorded body a fuzz campaign mutates, with the name it is declared under.
/// </summary>
/// <remarks>
/// The name is carried so that a failure names the fixture rather than a
/// position in a list. A campaign reports a fixture and an index, and those two
/// are what a reader needs to reproduce the body that failed, so a seed that
/// arrived without its name would leave every failure needing the corpus to be
/// rebuilt by hand before it could be read.
/// </remarks>
/// <param name="Name">The constant the body is declared as.</param>
/// <param name="Body">The bytes, as the source would have sent them.</param>
internal readonly record struct SourceResponseSeed(string Name, byte[] Body)
{
    /// <summary>
    /// Gets how many mutants this seed has at depth one.
    /// </summary>
    public long Count => SourceResponseMutations.Count(Body);

    /// <summary>
    /// Reads one mutant of this seed as the reader receives it.
    /// </summary>
    /// <param name="index">Which mutation.</param>
    /// <returns>The mutated body as text.</returns>
    public string Mutant(long index) =>
        SourceResponseMutations.Text(SourceResponseMutations.Mutant(Body, index));

    /// <summary>
    /// Says which body failed, in the two numbers that reproduce it.
    /// </summary>
    /// <param name="index">Which mutation.</param>
    /// <param name="status">The status the mutant was read beside.</param>
    /// <returns>A description a reader can run again.</returns>
    public string Describe(long index, int status) =>
        FormattableString.Invariant($"{Name}, mutation {index} of {Count}, read beside HTTP {status}");
}
