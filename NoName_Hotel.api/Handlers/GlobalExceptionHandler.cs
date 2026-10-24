using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace NoName_Hotel.api.Handlers;
public sealed class GlobalExceptionHandler(IProblemDetailsService problems,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        var conflict = exception is DbUpdateConcurrencyException ||
            exception is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 or 547 } };
        var badRequest = exception is BadHttpRequestException;
        var status = conflict ? 409 : badRequest ? 400 : 500;
        logger.LogError(exception, "Request failed with status {StatusCode}; trace {TraceId}", status, context.TraceIdentifier);
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = status, Title = conflict ? "The operation conflicts with current data." :
                    badRequest ? "Invalid request." : "An unexpected error occurred.",
                Extensions = { ["traceId"] = context.TraceIdentifier }
            }
        });
    }
}
