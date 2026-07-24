using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileProcessing.Api.Contracts;

namespace FileProcessing.Api.Tests.Integration;

[TestFixture]
public class ReportEndpointTests
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
    public async Task Get_Report_WithoutApiKey_ReturnsUnauthorized()
    {
        using var unauthenticatedClient = _factory.CreateClient();

        var response = await unauthenticatedClient.GetAsync("/api/files/report");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Get_Report_WithNoPriorAttempts_ReturnsZeroedReport()
    {
        var response = await _client.GetAsync("/api/files/report");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var report = await DeserializeReport(response);

        Assert.That(report!.TotalAttempts, Is.EqualTo(0));
    }

    [Test]
    public async Task Get_Report_AfterSuccessfulAndFailedProcessing_ReflectsBothOutcomes()
    {
        await _client.PostAsync("/api/files/process", BuildMultipartContent(
            "Employee,Department,Amount\nAlice,Engineering,100.00\n", "sales.csv", "text/csv"));

        await _client.PostAsync("/api/files/process", BuildMultipartContent(
            "Employee,Department\nAlice,Engineering\n", "bad.csv", "text/csv"));

        var response = await _client.GetAsync("/api/files/report");
        var report = await DeserializeReport(response);

        Assert.Multiple(() =>
        {
            Assert.That(report!.TotalAttempts, Is.EqualTo(2));
            Assert.That(report.SuccessfulAttempts, Is.EqualTo(1));
            Assert.That(report.FailedAttempts, Is.EqualTo(1));
            Assert.That(report.RecentRecords, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task Get_Report_AfterAuthenticationFailure_DoesNotCountAsAttempt()
    {
        using var unauthenticatedClient = _factory.CreateClient();
        await unauthenticatedClient.PostAsync("/api/files/process", BuildMultipartContent(
            "Employee,Department,Amount\nAlice,Engineering,100.00\n", "sales.csv", "text/csv"));

        var response = await _client.GetAsync("/api/files/report");
        var report = await DeserializeReport(response);

        Assert.That(report!.TotalAttempts, Is.EqualTo(0));
    }

    private static async Task<ProcessingReport?> DeserializeReport(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Deserialize<ProcessingReport>(body, options);
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
