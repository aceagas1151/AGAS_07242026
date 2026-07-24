namespace FileProcessing.Api.Contracts;

public class FileProcessingResult
{
    public string FileName { get; set; } = string.Empty;

    public DateTime ProcessedAtUtc { get; set; }

    public int RowCount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal AverageAmount { get; set; }

    public decimal MinimumAmount { get; set; }

    public decimal MaximumAmount { get; set; }

    public List<DepartmentTotal> DepartmentTotals { get; set; } = new();

    public long ProcessingTimeMilliseconds { get; set; }
}
