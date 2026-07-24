using FileProcessing.Api.Middleware;
using FileProcessing.Api.Options;
using FileProcessing.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddOptions<FileProcessingOptions>()
    .Bind(builder.Configuration.GetSection(FileProcessingOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "FileProcessing:ApiKey must be configured.")
    .ValidateOnStart();

builder.Services.AddScoped<IFileProcessor, CsvFileProcessor>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

app.Run();

public partial class Program { }
