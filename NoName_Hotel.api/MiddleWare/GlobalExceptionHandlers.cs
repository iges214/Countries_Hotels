using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace NoName_Hotel.api.MiddleWare
{
    public class GlobalExceptionHandlers(ILogger<GlobalExceptionHandlers> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext, Exception exception,
            CancellationToken cancellationToken)
        {
            var traceid = Activity.Current?.Id ?? httpContext.TraceIdentifier;

            logger.LogError
                (
                exception,
                "An unhandled exception occurred while processing the request. " +
                "Traceid: {Traceid}, Path: {Path}, Method: {Method}",
                traceid,
                httpContext.Request.Path,
                httpContext.Request.Method
                );

            var problemDetails = new ProblemDetails
            {
                Title = "An error occurred while preparing your equest",
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1",
                Instance = httpContext.Request.Path,
                Detail = httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() ? exception.Message : "An unexpected error occured. Pkease try again later."
            };

            problemDetails.Extensions["traceId"] = traceid;

            if (httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                problemDetails.Extensions["exceptionType"] = exception.GetType().Name;
                problemDetails.Extensions["stacktrace"] = exception.StackTrace;
            }

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            httpContext.Response.ContentType = "appplication/problem+json";

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;

        }
    }
}
