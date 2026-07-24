using FileProcessing.Api.Contracts;
using FileProcessing.Api.Exceptions;
using FileProcessing.Api.Options;
using FileProcessing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FileProcessing.Api.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private static readonly string[] AllowedExtensions = { ".csv" };

    private static readonly string[] AllowedContentTypes =
    {
        "text/csv",
        "application/vnd.ms-excel",
        "application/octet-stream"
    };

    private readonly IFileProcessor _fileProcessor;
    private readonly IFileProcessingTracker _tracker;
    private readonly FileProcessingOptions _options;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IFileProcessor fileProcessor,
        IFileProcessingTracker tracker,
        IOptions<FileProcessingOptions> options,
        ILogger<FilesController> logger)
    {
        _fileProcessor = fileProcessor;
        _tracker = tracker;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("process")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<FileProcessingResult>> Process(IFormFile? file, CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTime.UtcNow;
        var fileName = file?.FileName ?? "(unknown)";
        var fileSizeBytes = file?.Length ?? 0;

        try
        {
            ValidateFile(file);

            _logger.LogInformation(
                "Processing started for file {FileName} ({FileSizeBytes} bytes)",
                fileName,
                fileSizeBytes);

            await using var stream = file!.OpenReadStream();
            var result = await _fileProcessor.ProcessAsync(stream, fileName, cancellationToken);

            _tracker.RecordSuccess(fileName, fileSizeBytes, startedAtUtc, DateTime.UtcNow, result.RowCount);

            _logger.LogInformation(
                "Processing completed for file {FileName}: {RowCount} rows in {ElapsedMilliseconds} ms",
                fileName,
                result.RowCount,
                result.ProcessingTimeMilliseconds);

            return Ok(result);
        }
        catch (Exception ex) when (ex is FileValidationException or CsvProcessingException)
        {
            _tracker.RecordFailure(fileName, fileSizeBytes, startedAtUtc, DateTime.UtcNow, ex.Message);
            throw;
        }
        catch (Exception)
        {
            _tracker.RecordFailure(
                fileName,
                fileSizeBytes,
                startedAtUtc,
                DateTime.UtcNow,
                "An unexpected error occurred while processing the file.");
            throw;
        }
    }

    [HttpGet("report")]
    public ActionResult<ProcessingReport> GetReport()
    {
        return Ok(_tracker.GetReport());
    }

    private void ValidateFile(IFormFile? file)
    {
        if (file is null)
        {
            throw new FileValidationException("No file was provided. Use the 'file' form field.", StatusCodes.Status400BadRequest);
        }

        if (file.Length == 0)
        {
            throw new FileValidationException("The uploaded file is empty.", StatusCodes.Status400BadRequest);
        }

        if (file.Length > _options.MaximumFileSizeBytes)
        {
            throw new FileValidationException(
                $"The uploaded file exceeds the maximum allowed size of {_options.MaximumFileSizeBytes} bytes.",
                StatusCodes.Status413PayloadTooLarge);
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new FileValidationException(
                $"Unsupported file extension '{extension}'. Only .csv files are supported.",
                StatusCodes.Status415UnsupportedMediaType);
        }

        if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new FileValidationException(
                $"Unsupported content type '{file.ContentType}'.",
                StatusCodes.Status415UnsupportedMediaType);
        }
    }
}
