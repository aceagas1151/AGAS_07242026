using FileProcessing.Api.Contracts;
using FileProcessing.Api.Models;
using FileProcessing.Api.Options;
using Microsoft.Extensions.Options;

namespace FileProcessing.Api.Services;

public class InMemoryFileProcessingTracker : IFileProcessingTracker
{
    private readonly object _lock = new();
    private readonly LinkedList<ProcessingRecord> _records = new();
    private readonly int _maximumTrackedRecords;

    private int _totalAttempts;
    private int _successfulAttempts;
    private int _failedAttempts;
    private long _totalDurationMilliseconds;

    public InMemoryFileProcessingTracker(IOptions<FileProcessingOptions> options)
    {
        _maximumTrackedRecords = options.Value.MaximumTrackedRecords;
    }

    public void RecordSuccess(string fileName, long fileSizeBytes, DateTime startedAtUtc, DateTime completedAtUtc, int rowCount)
    {
        Add(new ProcessingRecord
        {
            FileName = fileName,
            FileSizeBytes = fileSizeBytes,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            DurationMilliseconds = (long)(completedAtUtc - startedAtUtc).TotalMilliseconds,
            Status = ProcessingStatus.Success,
            RowCount = rowCount
        });
    }

    public void RecordFailure(string fileName, long fileSizeBytes, DateTime startedAtUtc, DateTime completedAtUtc, string errorMessage)
    {
        Add(new ProcessingRecord
        {
            FileName = fileName,
            FileSizeBytes = fileSizeBytes,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            DurationMilliseconds = (long)(completedAtUtc - startedAtUtc).TotalMilliseconds,
            Status = ProcessingStatus.Failed,
            ErrorMessage = errorMessage
        });
    }

    public ProcessingReport GetReport()
    {
        lock (_lock)
        {
            var averageDuration = _totalAttempts == 0
                ? 0
                : Math.Round((double)_totalDurationMilliseconds / _totalAttempts, 2);

            return new ProcessingReport
            {
                TotalAttempts = _totalAttempts,
                SuccessfulAttempts = _successfulAttempts,
                FailedAttempts = _failedAttempts,
                AverageDurationMilliseconds = averageDuration,
                RecentRecords = _records.ToList()
            };
        }
    }

    private void Add(ProcessingRecord record)
    {
        lock (_lock)
        {
            _totalAttempts++;
            _totalDurationMilliseconds += record.DurationMilliseconds;

            if (record.Status == ProcessingStatus.Success)
            {
                _successfulAttempts++;
            }
            else
            {
                _failedAttempts++;
            }

            _records.AddFirst(record);

            if (_records.Count > _maximumTrackedRecords)
            {
                _records.RemoveLast();
            }
        }
    }
}
