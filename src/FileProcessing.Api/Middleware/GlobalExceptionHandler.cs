using FileProcessing.Api.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FileProcessing.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            FileValidationException fileValidationException =>
                (fileValidationException.StatusCode, "File validation failed", fileValidationException.Message),
            CsvProcessingException csvProcessingException =>
                (StatusCodes.Status400BadRequest, "CSV processing failed", csvProcessingException.Message),
            _ =>
                (StatusCodes.Status500InternalServerError, "An unexpected error occurred",
                    "An unexpected error occurred while processing the request.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception processing {Path}", httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning("Request to {Path} failed: {Detail}", httpContext.Request.Path, detail);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }
}
