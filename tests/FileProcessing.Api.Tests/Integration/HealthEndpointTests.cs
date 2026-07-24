using System.Net;

namespace FileProcessing.Api.Tests.Integration;

[TestFixture]
public class HealthEndpointTests
{
    private ApiWebApplicationFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new ApiWebApplicationFactory();
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
    }

    [Test]
    public async Task Get_Health_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Get_Health_ReturnsHealthyStatus()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(body, Does.Contain("Healthy"));
    }
}
