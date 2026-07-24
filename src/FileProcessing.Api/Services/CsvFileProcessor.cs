using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using FileProcessing.Api.Contracts;
using FileProcessing.Api.Exceptions;

namespace FileProcessing.Api.Services;

public class CsvFileProcessor : IFileProcessor
{
    private static readonly string[] RequiredHeaders = { "Employee", "Department", "Amount" };

    private readonly ILogger<CsvFileProcessor> _logger;

    public CsvFileProcessor(ILogger<CsvFileProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<FileProcessingResult> ProcessAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant()
        };

        using var reader = new StreamReader(fileStream);
        using var csv = new CsvReader(reader, config);

        if (!await csv.ReadAsync())
        {
            throw new CsvProcessingException("The CSV file is empty.");
        }

        csv.ReadHeader();
        ValidateHeaders(csv.HeaderRecord);

        var rows = new List<ParsedRow>();
        var rowNumber = 1;

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            rows.Add(ParseRow(csv, rowNumber));
        }

        if (rows.Count == 0)
        {
            throw new CsvProcessingException("The CSV file does not contain any data rows.");
        }

        stopwatch.Stop();

        var result = BuildResult(fileName, rows, stopwatch.ElapsedMilliseconds);

        _logger.LogInformation(
            "Processed file {FileName} with {RowCount} rows in {ElapsedMilliseconds} ms",
            fileName,
            result.RowCount,
            result.ProcessingTimeMilliseconds);

        return result;
    }

    private static void ValidateHeaders(string[]? headerRecord)
    {
        if (headerRecord is null)
        {
            throw new CsvProcessingException("The CSV file is missing a header row.");
        }

        var normalizedHeaders = headerRecord
            .Select(h => h.Trim().ToLowerInvariant())
            .ToHashSet();

        foreach (var required in RequiredHeaders)
        {
            if (!normalizedHeaders.Contains(required.ToLowerInvariant()))
            {
                throw new CsvProcessingException($"The CSV file is missing the required '{required}' column.");
            }
        }
    }

    private static ParsedRow ParseRow(CsvReader csv, int rowNumber)
    {
        var employee = csv.GetField("employee")?.Trim();
        var department = csv.GetField("department")?.Trim();
        var amountRaw = csv.GetField("amount")?.Trim();

        if (string.IsNullOrWhiteSpace(employee))
        {
            throw new CsvProcessingException($"Row {rowNumber}: Employee is required.");
        }

        if (string.IsNullOrWhiteSpace(department))
        {
            throw new CsvProcessingException($"Row {rowNumber}: Department is required.");
        }

        if (!decimal.TryParse(amountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            throw new CsvProcessingException($"Row {rowNumber}: Amount '{amountRaw}' is not a valid number.");
        }

        if (amount < 0)
        {
            throw new CsvProcessingException($"Row {rowNumber}: Amount cannot be negative.");
        }

        return new ParsedRow(employee, department, amount);
    }

    private static FileProcessingResult BuildResult(string fileName, List<ParsedRow> rows, long elapsedMilliseconds)
    {
        var amounts = rows.Select(r => r.Amount).ToList();

        var departmentTotals = rows
            .GroupBy(r => r.Department)
            .Select(g => new DepartmentTotal { Department = g.Key, TotalAmount = g.Sum(r => r.Amount) })
            .OrderBy(d => d.Department, StringComparer.Ordinal)
            .ToList();

        return new FileProcessingResult
        {
            FileName = fileName,
            ProcessedAtUtc = DateTime.UtcNow,
            RowCount = rows.Count,
            TotalAmount = amounts.Sum(),
            // Displayed average is rounded to 2 decimal places using away-from-zero
            // midpoint rounding so results are deterministic and match normal currency display.
            AverageAmount = Math.Round(amounts.Average(), 2, MidpointRounding.AwayFromZero),
            MinimumAmount = amounts.Min(),
            MaximumAmount = amounts.Max(),
            DepartmentTotals = departmentTotals,
            ProcessingTimeMilliseconds = elapsedMilliseconds
        };
    }

    private sealed record ParsedRow(string Employee, string Department, decimal Amount);
}
