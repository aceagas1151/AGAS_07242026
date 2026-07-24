namespace FileProcessing.Api.Options;

public class FileProcessingOptions
{
    public const string SectionName = "FileProcessing";

    public string ApiKey { get; set; } = string.Empty;

    public long MaximumFileSizeBytes { get; set; } = 5_242_880;

    public int MaximumTrackedRecords { get; set; } = 100;
}
