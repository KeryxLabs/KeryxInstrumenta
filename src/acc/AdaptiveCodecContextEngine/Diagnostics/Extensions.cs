using Microsoft.Extensions.Logging;
using SurrealDb.Net.Models.Response;

namespace AdaptiveCodecContextEngine.Diagnostics;

public static class LoggingExtensions
{
    extension(ILogger logger)
    {
        public void LogPossibleDbWriteError(SurrealDbResponse response)
        {
            if (response.HasErrors)
            {
                var error = (SurrealDbErrorResult?)response.Errors.FirstOrDefault();
                logger.LogDbWriteError(error?.Details);
            }
        }

        public void LogPossibleDbReadError(SurrealDbResponse response)
        {
            if (response.HasErrors)
            {
                var error = (SurrealDbErrorResult?)response.Errors.FirstOrDefault();
                logger.LogDbReadError(error?.Details);
            }
        }
    }
}
