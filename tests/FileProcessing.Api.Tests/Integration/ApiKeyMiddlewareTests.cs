using System.Net;

namespace FileProcessing.Api.Tests.Integration;

[TestFixture]
public class ApiKeyMiddlewareTests
{
    private ApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new ApiWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Get_ApiRoute_WithoutApiKey_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/files/report");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Get_ApiRoute_WithoutApiKey_ReturnsProblemDetailsBody()
    {
        var response = await _client.GetAsync("/api/files/report");

        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));

        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("\"status\":401"));
    }

    [Test]
    public async Task Get_ApiRoute_WithInvalidApiKey_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Add("X-API-Key", "wrong-key");

        var response = await _client.GetAsync("/api/files/report");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Get_ApiRoute_WithValidApiKey_IsNotRejectedByMiddleware()
    {
        _client.DefaultRequestHeaders.Add("X-API-Key", ApiWebApplicationFactory.TestApiKey);

        var response = await _client.GetAsync("/api/files/report");

        // No FilesController exists yet, so routing returns 404. That still proves
        // the middleware let the request through instead of short-circuiting it.
        Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Get_Health_WithoutApiKey_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
