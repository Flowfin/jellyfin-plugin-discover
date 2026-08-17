using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Jellyfin.Plugin.Template.Tests.Fixtures;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The bodies a fuzz campaign hands the source response reader, derived from the recorded fixtures.
/// </summary>
/// <remarks>
/// A corpus and a mutation rule rather than a generator. #37 asks that the
/// reader be exercised from raw bytes to catalogue record, seeded from the
/// fixtures #48 recorded, and a body drawn from nothing at all would spend the
/// whole campaign being refused as not-JSON at the first byte. A recorded
/// response with one byte changed reaches the mapping, which is where the
/// untrusted values actually go.
///
/// Every mutant is a function of a seed and an index, so a campaign is
/// reproducible from two numbers and nothing has to be recorded when one fails.
/// That is why there is no generator here and no seeded one either: the
/// invariant <c>no-random</c> refuses a draw this project cannot fix, and a
/// campaign that needs a corpus file written out beside a failure is one whose
/// failures arrive without the input that caused them.
///
/// The mutants are bytes and the reader takes text, so each one is decoded as
/// UTF-8 on the way out. That is the same decode the adapter's own transport
/// performs on a response body, invalid sequences included: the runtime
/// substitutes the replacement character rather than throwing, so a mutant that
/// breaks a multi-byte sequence reaches the reader as a body a real connection
/// could have produced.
/// </remarks>
internal static class SourceResponseMutations
{
    /// <summary>
    /// The bytes a mutation substitutes and inserts.
    /// </summary>
    /// <remarks>
    /// Chosen for what each one does to a reader of JSON rather than for
    /// spread. The structural bytes open and close what was not opened, the
    /// quote and the backslash move a string boundary and start an escape the
    /// body does not finish, the digit and the exponent turn a number into one
    /// no thirty-two bit integer holds, the solidus is what would move an
    /// artwork location off the source's own host, and the null and the
    /// continuation byte are the two a text decoder has to answer for. A
    /// uniform sweep of all 256 would spend most of its budget on bytes that
    /// only ever produce the same refusal.
    /// </remarks>
    private static readonly byte[] _substituted =
    {
        0x00, 0x22, 0x2C, 0x2D, 0x2E, 0x2F, 0x39, 0x3A, 0x5B, 0x5C, 0x5D, 0x65, 0x7B, 0x7D, 0x80, 0xFF
    };

    private static readonly IReadOnlyList<SourceResponseSeed> _corpus = Seeds();

    /// <summary>
    /// Gets the recorded fixtures a campaign starts from.
    /// </summary>
    /// <remarks>
    /// Read off <see cref="TmdbFixtures"/> by reflection rather than listed
    /// here. A fixture added for a shape the parser met later is then fuzzed by
    /// the campaign the day it lands, with nobody having to remember this file,
    /// and a list here would be the second statement of the set that drifts
    /// against the first.
    /// </remarks>
    public static IReadOnlyList<SourceResponseSeed> Corpus => _corpus;

    /// <summary>
    /// Counts the mutants one seed has at depth one.
    /// </summary>
    /// <param name="seed">The bytes being mutated.</param>
    /// <returns>How many distinct indices <see cref="Mutant"/> answers for.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="seed"/> is null.</exception>
    public static long Count(byte[] seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        long length = seed.Length;

        return (2 * length) + (2 * length * _substituted.Length);
    }

    /// <summary>
    /// Applies one mutation to one seed.
    /// </summary>
    /// <param name="seed">The bytes being mutated.</param>
    /// <param name="index">Which mutation, from zero up to <see cref="Count"/>.</param>
    /// <returns>The mutated bytes.</returns>
    /// <remarks>
    /// Four families, in a fixed order so an index names the same mutant for as
    /// long as the seed and this file are unchanged. Truncation is the
    /// connection that dropped, deletion and substitution are the byte that
    /// arrived wrong, and insertion is the byte that arrived twice. A failing
    /// index is quoted with the fixture's name, and the two together are the
    /// whole of what a reader needs to run it again.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="seed"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is outside what <see cref="Count"/>
    /// answers for, because a campaign walking past the end would otherwise
    /// repeat the last mutant silently and report a budget it did not spend.
    /// </exception>
    public static byte[] Mutant(byte[] seed, long index)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count(seed));

        long length = seed.Length;

        if (index < length)
        {
            return seed[..(int)index];
        }

        index -= length;

        if (index < length)
        {
            var shorter = new byte[seed.Length - 1];
            Array.Copy(seed, 0, shorter, 0, (int)index);
            Array.Copy(seed, (int)index + 1, shorter, (int)index, seed.Length - (int)index - 1);

            return shorter;
        }

        index -= length;

        if (index < length * _substituted.Length)
        {
            var substituted = (byte[])seed.Clone();
            substituted[(int)(index / _substituted.Length)] = _substituted[(int)(index % _substituted.Length)];

            return substituted;
        }

        index -= length * _substituted.Length;

        var at = (int)(index / _substituted.Length);
        var longer = new byte[seed.Length + 1];
        Array.Copy(seed, 0, longer, 0, at);
        longer[at] = _substituted[(int)(index % _substituted.Length)];
        Array.Copy(seed, at, longer, at + 1, seed.Length - at);

        return longer;
    }

    /// <summary>
    /// Reads the mutated bytes back as the reader receives them.
    /// </summary>
    /// <param name="body">What the mutation produced.</param>
    /// <returns>The body as text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="body"/> is null.</exception>
    public static string Text(byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);

        return Encoding.UTF8.GetString(body);
    }

    /// <summary>
    /// Reads the fixtures off the type that holds them.
    /// </summary>
    /// <returns>Every recorded body, with the name it is declared under.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the fixture type holds no bodies, which would leave a
    /// campaign reporting that it found nothing after asking nothing.
    /// </exception>
    private static SourceResponseSeed[] Seeds()
    {
        var seeds = typeof(TmdbFixtures)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .Select(field => new SourceResponseSeed(
                field.Name,
                Encoding.UTF8.GetBytes(TmdbFixtures.Body((string)field.GetRawConstantValue()!))))
            .ToArray();

        if (seeds.Length == 0)
        {
            throw new InvalidOperationException(
                "The fuzz corpus is derived from the recorded fixtures and there are none, so a campaign over it would pass by asking nothing.");
        }

        return seeds;
    }
}
