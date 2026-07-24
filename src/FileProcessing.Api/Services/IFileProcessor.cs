using FileProcessing.Api.Contracts;

namespace FileProcessing.Api.Services;

public interface IFileProcessor
{
    Task<FileProcessingResult> ProcessAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}
