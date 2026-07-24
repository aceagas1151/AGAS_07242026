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
    private readonly FileProcessingOptions _options;
    private readonly ILogger<FilesController> _logger;

    public FilesController(IFileProcessor fileProcessor, IOptions<FileProcessingOptions> options, ILogger<FilesController> logger)
    {
        _fileProcessor = fileProcessor;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("process")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<FileProcessingResult>> Process(IFormFile? file, CancellationToken cancellationToken)
    {
        ValidateFile(file);

        _logger.LogInformation(
            "Processing started for file {FileName} ({FileSizeBytes} bytes)",
            file!.FileName,
            file.Length);

        await using var stream = file.OpenReadStream();
        var result = await _fileProcessor.ProcessAsync(stream, file.FileName, cancellationToken);

        _logger.LogInformation(
            "Processing completed for file {FileName}: {RowCount} rows in {ElapsedMilliseconds} ms",
            file.FileName,
            result.RowCount,
            result.ProcessingTimeMilliseconds);

        return Ok(result);
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
