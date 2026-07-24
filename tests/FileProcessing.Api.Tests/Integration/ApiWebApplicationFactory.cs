using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FileProcessing.Api.Tests.Integration;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestApiKey = "test-api-key-12345";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileProcessing:ApiKey"] = TestApiKey,
                ["FileProcessing:MaximumFileSizeBytes"] = "5242880",
                ["FileProcessing:MaximumTrackedRecords"] = "100"
            });
        });
    }
}
