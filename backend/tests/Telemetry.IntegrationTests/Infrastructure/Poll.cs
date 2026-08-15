namespace Telemetry.IntegrationTests.Infrastructure;

public static class Poll
{
    /// <summary>Polls until the condition returns true, tolerating transient exceptions.</summary>
    public static async Task UntilAsync(Func<Task<bool>> condition, TimeSpan timeout, string description)
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (await condition())
                {
                    return;
                }

                lastError = null;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out after {timeout} waiting for: {description}", lastError);
    }
}
