namespace FileProcessing.Api.Models;

public class ProcessingRecord
{
    public string FileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime CompletedAtUtc { get; set; }

    public long DurationMilliseconds { get; set; }

    public ProcessingStatus Status { get; set; }

    public int? RowCount { get; set; }

    public string? ErrorMessage { get; set; }
}
