using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Template.Randomness;
using Jellyfin.Plugin.Template.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// The two things a test has to be able to hold still, and the graph that hands
/// them out.
/// </summary>
/// <remarks>
/// No wall clock is read here and no generator is called directly. Every
/// assertion below goes through the same interface the plugin uses, which is
/// the point: a test that reached past them would be proving something about
/// this machine rather than about the plugin.
/// </remarks>
public class InjectedTimeAndRandomnessTests
{
    /// <summary>
    /// The container hands out the system implementations of both.
    /// </summary>
    /// <remarks>
    /// The registration is what makes the substitution total. If a reader of the
    /// clock resolved something else, or nothing, then replacing the clock in a
    /// test would replace only some of the readers and the ones it missed would
    /// be the ones that flake.
    /// </remarks>
    [Fact]
    public void TheContainerSuppliesBothSources()
    {
        using var provider = BuildTheGraph();

        Assert.IsType<SystemClock>(provider.GetRequiredService<IClock>());
        Assert.IsType<SystemRandomSource>(provider.GetRequiredService<IRandomSource>());
    }

    /// <summary>
    /// Both are one instance for the whole server.
    /// </summary>
    /// <remarks>
    /// Stated as a test rather than as a comment on the registration, because
    /// the lifetime is the half of the decision a later edit is most likely to
    /// change without meaning to.
    /// </remarks>
    [Fact]
    public void BothSourcesAreOneInstanceForTheWholeServer()
    {
        using var provider = BuildTheGraph();

        Assert.Same(provider.GetRequiredService<IClock>(), provider.GetRequiredService<IClock>());
        Assert.Same(provider.GetRequiredService<IRandomSource>(), provider.GetRequiredService<IRandomSource>());
    }

    /// <summary>
    /// The system clock answers in UTC and never goes backwards.
    /// </summary>
    /// <remarks>
    /// Asserted against itself rather than against a second reading of the
    /// machine's clock. A test comparing the two would be a second wall-clock
    /// read, which is the thing the invariant refuses, and it would be the
    /// flakiest line in the suite.
    /// </remarks>
    [Fact]
    public void TheSystemClockAnswersInUtcAndDoesNotGoBackwards()
    {
        var clock = new SystemClock();

        var first = clock.UtcNow;
        var second = clock.UtcNow;

        Assert.Equal(TimeSpan.Zero, first.Offset);
        Assert.Equal(TimeSpan.Zero, second.Offset);
        Assert.True(second >= first, "A clock that goes backwards turns every expiry comparison into a coin toss.");
    }

    /// <summary>
    /// A test moves time by advancing the clock, and by nothing else.
    /// </summary>
    /// <remarks>
    /// This is the boundary case the suite is built to be able to state: one
    /// tick before a deadline and one tick after it, with nothing in between
    /// that depends on how long anything took. The decisions made with this
    /// apparatus live elsewhere and are asserted there: the retention is
    /// <see cref="Jellyfin.Plugin.Template.Catalogue.CatalogueRetention"/>, the
    /// backoff is <see cref="Jellyfin.Plugin.Template.Refresh.SourceRest"/>, and
    /// the deadline on a source request is the adapter's. What is asserted here
    /// is the apparatus rather than any of them.
    /// </remarks>
    [Fact]
    public void AdvancingTheClockIsTheOnlyWayTimePasses()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var deadline = start + TimeSpan.FromHours(3);
        var clock = new ClockATestAdvances(start);

        Assert.Equal(start, clock.UtcNow);

        clock.Advance(TimeSpan.FromHours(3) - TimeSpan.FromTicks(1));
        Assert.True(clock.UtcNow < deadline);

        clock.Advance(TimeSpan.FromTicks(1));
        Assert.Equal(deadline, clock.UtcNow);

        clock.Advance(TimeSpan.FromTicks(1));
        Assert.True(clock.UtcNow > deadline);
    }

    /// <summary>
    /// The clock a test holds refuses to be wound back.
    /// </summary>
    /// <remarks>
    /// A clock that can go backwards lets a test that is failing be made to pass
    /// by rewinding past the decision, which is the one way this apparatus could
    /// be used to hide the thing it exists to expose.
    /// </remarks>
    [Fact]
    public void TheClockATestHoldsRefusesToGoBackwards()
    {
        var clock = new ClockATestAdvances(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromTicks(-1)));
    }

    /// <summary>
    /// The bounded draw stays inside its bound.
    /// </summary>
    /// <remarks>
    /// A draw that can return its own bound is an index one past the end of the
    /// shelf it was drawn for, and it would appear on one run in however many
    /// the bound is.
    /// </remarks>
    [Fact]
    public void TheBoundedDrawStaysInsideItsBound()
    {
        var source = new SystemRandomSource();

        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var drawn = source.Next(4);

            Assert.InRange(drawn, 0, 3);
        }
    }

    /// <summary>
    /// The fraction is in the half-open range from zero to one.
    /// </summary>
    /// <remarks>
    /// The shape a backoff jitter multiplies a delay by. A value of exactly one
    /// would make the jitter the whole delay rather than a part of it.
    /// </remarks>
    [Fact]
    public void TheFractionIsAtLeastZeroAndBelowOne()
    {
        var source = new SystemRandomSource();

        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var drawn = source.NextDouble();

            Assert.True(drawn >= 0.0 && drawn < 1.0, "A jitter fraction outside [0,1) is a delay the caller did not ask for.");
        }
    }

    /// <summary>
    /// Identifiers are not repeated and are not the empty one.
    /// </summary>
    /// <remarks>
    /// The empty identifier is what an uninitialised field reads as, so a
    /// generator returning it is indistinguishable from one that was never
    /// called.
    /// </remarks>
    [Fact]
    public void IdentifiersAreDistinctAndNotEmpty()
    {
        var source = new SystemRandomSource();
        var seen = new HashSet<Guid>();

        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var identifier = source.NewIdentifier();

            Assert.NotEqual(Guid.Empty, identifier);
            Assert.True(seen.Add(identifier), "An identifier was handed out twice.");
        }
    }

    private static ServiceProvider BuildTheGraph()
    {
        var services = new ServiceCollection();

        // The server registers the logging abstractions before it calls a
        // plugin's registrator, so a container built without them is a poorer
        // model of the server than of this plugin. Named here since #95, which
        // added the first registration that takes one.
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        new PluginServiceRegistrator().RegisterServices(services, new ServerApplicationHostThatRefusesEveryCall());

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
