using System.Text;
using System.Text.Json;
using FileProcessing.Api.Exceptions;
using FileProcessing.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileProcessing.Api.Tests.Middleware;

[TestFixture]
public class GlobalExceptionHandlerTests
{
    private GlobalExceptionHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
    }

    [Test]
    public async Task TryHandleAsync_WithUnexpectedException_ReturnsGenericProblemDetailsWithoutInternalDetails()
    {
        var context = CreateHttpContext();
        var exception = new InvalidOperationException("Connection string password=hunter2 failed at C:\\secrets\\config.json");

        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);
        var problem = await ReadProblemDetails(context);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
            Assert.That(problem!.Detail, Does.Not.Contain("hunter2"));
            Assert.That(problem.Detail, Does.Not.Contain("InvalidOperationException"));
            Assert.That(problem.Detail, Does.Not.Contain("config.json"));
        });
    }

    [Test]
    public async Task TryHandleAsync_WithCsvProcessingException_ReturnsBadRequestWithSafeMessage()
    {
        var context = CreateHttpContext();
        var exception = new CsvProcessingException("Row 3: Amount is not a valid number.");

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);
        var problem = await ReadProblemDetails(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(problem!.Detail, Is.EqualTo("Row 3: Amount is not a valid number."));
        });
    }

    [Test]
    public async Task TryHandleAsync_WithFileValidationException_UsesExceptionStatusCode()
    {
        var context = CreateHttpContext();
        var exception = new FileValidationException(
            "Unsupported file extension '.txt'. Only .csv files are supported.",
            StatusCodes.Status415UnsupportedMediaType);

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status415UnsupportedMediaType));
    }

    [Test]
    public async Task TryHandleAsync_WritesProblemDetailsContentType()
    {
        var context = CreateHttpContext();

        await _handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        Assert.That(context.Response.ContentType, Is.EqualTo("application/problem+json"));
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<ProblemDetails?> ReadProblemDetails(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<ProblemDetails>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}
