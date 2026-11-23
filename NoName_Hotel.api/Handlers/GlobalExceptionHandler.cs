using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace NoName_Hotel.api.Handlers;
public sealed class GlobalExceptionHandler(IProblemDetailsService problems,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Client cancelled {Method} {Path}; trace {TraceId}", context.Request.Method, context.Request.Path, context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }
        var conflict = exception is DbUpdateConcurrencyException ||
            exception is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 or 547 } };
        var unavailable = exception is TimeoutException or OperationCanceledException;
        var status = conflict ? 409 : exception is BadHttpRequestException bad ? bad.StatusCode : unavailable ? 503 : 500;
        logger.Log(status < 500 ? LogLevel.Warning : LogLevel.Error, exception,
            "Request {Method} {Path} failed with status {StatusCode}; trace {TraceId}",
            context.Request.Method, context.Request.Path, status, context.TraceIdentifier);
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "private, no-store";
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = status, Title = conflict ? "The operation conflicts with current data." :
                status < 500 ? "Invalid request." : unavailable ? "The service is temporarily unavailable." : "An unexpected error occurred.",
            Detail = conflict ? "Reload the current data before trying again." :
                status >= 500 ? "Please try again later. If the problem continues, contact the hotel with the trace ID." : "Please check your request.",
            Extensions = { ["traceId"] = context.TraceIdentifier }
        };
        var written = await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem
        });
        if (!written) await context.Response.WriteAsJsonAsync(problem, options: null,
            contentType: "application/problem+json", cancellationToken: token);
        return true;
    }
}
