// Puts what configuration holds into a log line.
namespace Fixture;

public sealed class BreaksTheRule
{
    public void Warn(ILogger logger, Config config) => logger.LogWarning("key {Key}", config.ApiKey);
}
