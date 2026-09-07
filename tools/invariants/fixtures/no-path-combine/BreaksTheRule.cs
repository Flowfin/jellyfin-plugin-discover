// Composes a path with the method that drops every earlier argument the
// moment a later one is rooted. A name arriving rooted lands outside the
// directory the first argument named and still writes successfully.
namespace Fixture;

using System.IO;

public static class BreaksTheRule
{
    public static string Under(string root, string name)
        => Path.Combine(root, name);
}
