using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FileProcessing.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace FileProcessing.Api.Tests.Integration;

[TestFixture]
public class FilesControllerTests
{
    private ApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new ApiWebApplicationFactory();
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", ApiWebApplicationFactory.TestApiKey);
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Post_Process_WithValidCsv_ReturnsOkWithAggregates()
    {
        var csv = "Employee,Department,Amount\nAlice,Engineering,1250.50\nBob,Sales,900.00\n";
        var content = BuildMultipartContent(csv, "sales.csv", "text/csv");

        var response = await _client.PostAsync("/api/files/process", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<FileProcessingResult>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(result!.RowCount, Is.EqualTo(2));
        Assert.That(result.TotalAmount, Is.EqualTo(2150.50m));
    }

    [Test]
    public async Task Post_Process_WithoutFile_ReturnsBadRequest()
    {
        var content = new MultipartFormDataContent();

        var response = await _client.PostAsync("/api/files/process", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Post_Process_WithUnsupportedExtension_ReturnsUnsupportedMediaType()
    {
        var content = BuildMultipartContent("some,content\n1,2\n", "sales.txt", "text/csv");

        var response = await _client.PostAsync("/api/files/process", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnsupportedMediaType));
    }

    [Test]
    public async Task Post_Process_WithUnsupportedContentType_ReturnsUnsupportedMediaType()
    {
        var content = BuildMultipartContent(
            "Employee,Department,Amount\nAlice,Engineering,100.00\n",
            "sales.csv",
            "application/json");

        var response = await _client.PostAsync("/api/files/process", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnsupportedMediaType));
    }

    [Test]
    public async Task Post_Process_WithMalformedCsv_ReturnsBadRequest()
    {
        var content = BuildMultipartContent("Employee,Department\nAlice,Engineering\n", "sales.csv", "text/csv");

        var response = await _client.PostAsync("/api/files/process", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Post_Process_WithFileExceedingMaximumSize_ReturnsPayloadTooLarge()
    {
        using var restrictedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileProcessing:MaximumFileSizeBytes"] = "10"
                });
            });
        });
        using var client = restrictedFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiWebApplicationFactory.TestApiKey);

        var content = BuildMultipartContent(
            "Employee,Department,Amount\nAlice,Engineering,100.00\n",
            "sales.csv",
            "text/csv");

        var response = await client.PostAsync("/api/files/process", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
    }

    [Test]
    public async Task Post_Process_WithoutApiKey_ReturnsUnauthorized()
    {
        using var unauthenticatedClient = _factory.CreateClient();
        var content = BuildMultipartContent("Employee,Department,Amount\nAlice,Engineering,100.00\n", "sales.csv", "text/csv");

        var response = await unauthenticatedClient.PostAsync("/api/files/process", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private static MultipartFormDataContent BuildMultipartContent(string csvContent, string fileName, string contentType)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        return content;
    }
}
