using FileProcessing.Api.Models;

namespace FileProcessing.Api.Contracts;

public class ProcessingReport
{
    public int TotalAttempts { get; set; }

    public int SuccessfulAttempts { get; set; }

    public int FailedAttempts { get; set; }

    public double AverageDurationMilliseconds { get; set; }

    public List<ProcessingRecord> RecentRecords { get; set; } = new();
}
