// A test declared in a file the suite's naming convention says is apparatus
// rather than a test file, so every rule written against test files is silent
// about it.
//
// This file is a fixture. It is outside every project in the solution, nothing
// compiles it, and it exists so the rule can be watched refusing the line it is
// about. Its name deliberately does not end in Tests.cs, which is the shape
// being refused, and it imports nothing at all so that what it breaks is one
// invariant.
public class BreaksTheRule
{
    [Fact]
    public void TheRecorderKeepsWhatItWasHanded()
    {
    }
}
