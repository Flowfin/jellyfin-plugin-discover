// A test that waits for the cache to expire instead of advancing the clock.
// This file is a fixture. It is outside every project in the solution, nothing
// compiles it, and it exists so the rule can be watched refusing the line it is
// about.
public class BreaksTheRule
{
    public void TheCacheHasExpiredByNow()
    {
        System.Threading.Thread.Sleep(3000);
    }
}
