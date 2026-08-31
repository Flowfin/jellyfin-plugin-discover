// A test that reaches past the plugin's own interfaces and imports a server
// type, instead of driving a hand-written fake of the interface the plugin
// talks to the server through.
//
// This file is a fixture. It is outside every project in the solution, nothing
// compiles it, and it exists so the rule can be watched refusing the line it is
// about. Its name ends in Tests.cs because the rule's subject is a test file,
// and it imports a namespace no other rule here names, so what it breaks is one
// invariant.
using MediaBrowser.Controller.Plugins;

public class BreaksTheRuleTests
{
    public void TheRegistratorTakesTheServersOwnType()
    {
        IPluginServiceRegistrator registrator = null;
        System.Console.WriteLine(registrator);
    }
}
