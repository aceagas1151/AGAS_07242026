using System.Security.Cryptography;
using System.Text;
using FileProcessing.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FileProcessing.Api.Middleware;

public class ApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private const string ProtectedPathPrefix = "/api";

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<FileProcessingOptions> options)
    {
        if (!context.Request.Path.StartsWithSegments(ProtectedPathPrefix))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey) ||
            string.IsNullOrWhiteSpace(providedKey))
        {
            _logger.LogWarning("Rejected request to {Path}: API key header was missing", context.Request.Path);
            await WriteUnauthorizedResponse(context, "API key is missing.");
            return;
        }

        if (!IsValidKey(providedKey!, options.Value.ApiKey))
        {
            _logger.LogWarning("Rejected request to {Path}: API key was invalid", context.Request.Path);
            await WriteUnauthorizedResponse(context, "API key is invalid.");
            return;
        }

        await _next(context);
    }

    private static bool IsValidKey(string providedKey, string expectedKey)
    {
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);

        return providedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    private static Task WriteUnauthorizedResponse(HttpContext context, string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json");
    }
}
