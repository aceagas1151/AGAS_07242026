using FileProcessing.Api.Contracts;

namespace FileProcessing.Api.Services;

public interface IFileProcessingTracker
{
    void RecordSuccess(string fileName, long fileSizeBytes, DateTime startedAtUtc, DateTime completedAtUtc, int rowCount);

    void RecordFailure(string fileName, long fileSizeBytes, DateTime startedAtUtc, DateTime completedAtUtc, string errorMessage);

    ProcessingReport GetReport();
}
