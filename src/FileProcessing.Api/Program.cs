using FileProcessing.Api.Middleware;
using FileProcessing.Api.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddOptions<FileProcessingOptions>()
    .Bind(builder.Configuration.GetSection(FileProcessingOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "FileProcessing:ApiKey must be configured.")
    .ValidateOnStart();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

app.Run();

public partial class Program { }
