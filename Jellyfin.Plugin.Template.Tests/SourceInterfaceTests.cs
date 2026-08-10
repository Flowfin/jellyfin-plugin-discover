using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Template.Catalogue;
using Jellyfin.Plugin.Template.Sources;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// What the source interface is allowed to be made of.
/// </summary>
/// <remarks>
/// #73's first condition is that one interface describes a source and that it
/// is expressed in this plugin's own types, with no type from any source's
/// client library in its signature. Read by a person that is an inspection
/// which goes stale on the day somebody adds a member; read here it is a
/// property that reds the suite instead.
///
/// It is wider than the condition asks and deliberately so. The condition names
/// a source's client library; the assertion admits only this plugin's own
/// assembly and the core library, so a server type is refused by the same test.
/// A server type in the signature would be the same defect arriving from the
/// other side, and there is no reason to catch one and not the other.
/// </remarks>
public class SourceInterfaceTests
{
    /// <summary>
    /// Nothing in the interface's signature comes from anywhere but this plugin and the core library.
    /// </summary>
    /// <remarks>
    /// The near-miss this is written against is the cheap one: an adapter's
    /// response type, or the server's own query, added as a parameter because
    /// it was to hand. That is how a wire vocabulary reaches a caller, and by
    /// the time a second source is added the first one's callers are already
    /// written against it.
    ///
    /// Watched failing rather than assumed. Adding a member returning a server
    /// type to <c>IMetadataSource</c> reds this test and names the type, and the
    /// run is in the pull request that landed it.
    /// </remarks>
    [Fact]
    public void EveryTypeInTheInterfaceIsThisPluginsOrTheCoreLibrarys()
    {
        var mine = typeof(IMetadataSource).Assembly;
        var core = typeof(object).Assembly;

        var foreign = new List<string>();

        foreach (var member in typeof(IMetadataSource).GetMembers(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var type in TypesIn(member))
            {
                if (type.Assembly != mine && type.Assembly != core)
                {
                    foreign.Add($"{member.Name} names {type.FullName} from {type.Assembly.GetName().Name}");
                }
            }
        }

        Assert.Empty(foreign);
    }

    /// <summary>
    /// The interface is the only thing in the plugin that describes a source.
    /// </summary>
    /// <remarks>
    /// #73 asks for one interface rather than for an interface. A second one
    /// added beside it is how the callers end up asking two different questions
    /// of two sources, and it is the shape a hurried second adapter produces.
    /// </remarks>
    [Fact]
    public void ExactlyOneInterfaceDescribesASource()
    {
        var describers = typeof(IMetadataSource).Assembly
            .GetTypes()
            .Where(type => type.IsInterface)
            .Where(type => type.Namespace == typeof(IMetadataSource).Namespace)
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { typeof(IMetadataSource).FullName }, describers);
    }

    /// <summary>
    /// A source implemented against the interface alone needs nothing from anywhere else.
    /// </summary>
    /// <remarks>
    /// The fake beside this file is the demonstration rather than an argument:
    /// it answers questions, records them and names its body, and it references
    /// this plugin and the base class library only. If the interface ever
    /// required a client library to implement, this file would be where that
    /// showed, because the fake could no longer be written without one.
    /// </remarks>
    [Fact]
    public void AFakeSourceCanBeWrittenWithNothingButThisPlugin()
    {
        var referenced = typeof(SourceThatAnswersFromWhatATestGaveIt)
            .GetInterfaces()
            .Select(type => type.Assembly)
            .Distinct()
            .ToArray();

        Assert.Equal(new[] { typeof(IMetadataSource).Assembly }, referenced);
        Assert.Equal(MetadataSource.Tmdb, new SourceThatAnswersFromWhatATestGaveIt(MetadataSource.Tmdb).Source);
    }

    /// <summary>
    /// Every type one member's signature mentions, generic arguments included.
    /// </summary>
    /// <param name="member">The member to read.</param>
    /// <returns>The types, with arrays and generics unwrapped to what they hold.</returns>
    private static IEnumerable<Type> TypesIn(MemberInfo member)
    {
        var declared = member switch
        {
            MethodInfo method => new[] { method.ReturnType }.Concat(method.GetParameters().Select(parameter => parameter.ParameterType)),
            PropertyInfo property => new[] { property.PropertyType },
            _ => Enumerable.Empty<Type>()
        };

        foreach (var type in declared)
        {
            foreach (var part in Unwrap(type))
            {
                yield return part;
            }
        }
    }

    /// <summary>
    /// Turns a constructed type into the types it is made of.
    /// </summary>
    /// <param name="type">The type to take apart.</param>
    /// <returns>
    /// The type itself and, where it is generic or an array, everything it
    /// holds. A <c>Task&lt;SourceAnswer&gt;</c> that named a foreign type would
    /// otherwise pass, because the task itself is the core library's.
    /// </returns>
    private static IEnumerable<Type> Unwrap(Type type)
    {
        yield return type;

        if (type.HasElementType && type.GetElementType() is { } element)
        {
            foreach (var part in Unwrap(element))
            {
                yield return part;
            }
        }

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var part in Unwrap(argument))
            {
                yield return part;
            }
        }
    }
}
