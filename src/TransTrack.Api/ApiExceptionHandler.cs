using Microsoft.AspNetCore.Diagnostics;
using TransTrack.Data;

namespace TransTrack.Api;

/// <summary>
/// Turns the exceptions the domain throws into the responses the client
/// expects, in one place.
///
/// The services signal a broken rule by throwing InvalidOperationException
/// with a message written for the user ("This trip is closed. Reopen it
/// first…"). Controllers were catching that individually, which meant the
/// rule only surfaced properly on the endpoints someone had remembered to
/// wrap — MastersController had fifteen write endpoints and four catches, so
/// eleven of them answered a failed validation with a 500 and a stack trace.
/// Handling it centrally closes that whole class of bug and keeps new
/// endpoints correct by default.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            // A broken business rule: the user's fault, and the message is
            // already written for them.
            case InvalidOperationException:
                AppLog.Info($"Rejected {context.Request.Method} {context.Request.Path}: {exception.Message}");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { message = exception.Message }, cancellationToken);
                return true;

            // Genuinely unexpected. Log the detail server-side; tell the
            // client only that it failed, never the stack trace.
            default:
                logger.LogError(exception, "Unhandled error on {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                AppLog.Error($"Unhandled error on {context.Request.Method} {context.Request.Path}.", exception);

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(
                    new { message = "Something went wrong. Please try again." }, cancellationToken);
                return true;
        }
    }
}
