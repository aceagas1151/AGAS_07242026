using System.Text;
using FileProcessing.Api.Exceptions;
using FileProcessing.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileProcessing.Api.Tests.Services;

[TestFixture]
public class CsvFileProcessorTests
{
    private CsvFileProcessor _processor = null!;

    [SetUp]
    public void SetUp()
    {
        _processor = new CsvFileProcessor(NullLogger<CsvFileProcessor>.Instance);
    }

    [Test]
    public async Task ProcessAsync_WithValidCsv_ReturnsExpectedAggregates()
    {
        var csv = "Employee,Department,Amount\n" +
                   "Alice,Engineering,1250.50\n" +
                   "Bob,Sales,900.00\n" +
                   "Carol,Engineering,1500.00\n";

        var result = await _processor.ProcessAsync(ToStream(csv), "sales.csv");

        Assert.Multiple(() =>
        {
            Assert.That(result.RowCount, Is.EqualTo(3));
            Assert.That(result.TotalAmount, Is.EqualTo(3650.50m));
            Assert.That(result.AverageAmount, Is.EqualTo(1216.83m));
            Assert.That(result.MinimumAmount, Is.EqualTo(900.00m));
            Assert.That(result.MaximumAmount, Is.EqualTo(1500.00m));
        });
    }

    [Test]
    public async Task ProcessAsync_WithValidCsv_GroupsDepartmentTotalsAlphabetically()
    {
        var csv = "Employee,Department,Amount\n" +
                   "Alice,Engineering,1250.50\n" +
                   "Bob,Sales,900.00\n" +
                   "Carol,Engineering,1500.00\n";

        var result = await _processor.ProcessAsync(ToStream(csv), "sales.csv");

        Assert.That(result.DepartmentTotals, Has.Count.EqualTo(2));
        Assert.That(result.DepartmentTotals[0].Department, Is.EqualTo("Engineering"));
        Assert.That(result.DepartmentTotals[0].TotalAmount, Is.EqualTo(2750.50m));
        Assert.That(result.DepartmentTotals[1].Department, Is.EqualTo("Sales"));
        Assert.That(result.DepartmentTotals[1].TotalAmount, Is.EqualTo(900.00m));
    }

    [Test]
    public async Task ProcessAsync_WithMixedCaseAndWhitespaceHeaders_ParsesSuccessfully()
    {
        var csv = " employee , DEPARTMENT ,Amount \n" +
                   "Alice,Engineering,100.00\n";

        var result = await _processor.ProcessAsync(ToStream(csv), "sales.csv");

        Assert.That(result.RowCount, Is.EqualTo(1));
        Assert.That(result.TotalAmount, Is.EqualTo(100.00m));
    }

    [TestCase("Department,Amount\nEngineering,100.00\n", "Employee")]
    [TestCase("Employee,Amount\nAlice,100.00\n", "Department")]
    [TestCase("Employee,Department\nAlice,Engineering\n", "Amount")]
    public void ProcessAsync_WithMissingRequiredHeader_ThrowsCsvProcessingException(string csv, string missingHeader)
    {
        var ex = Assert.ThrowsAsync<CsvProcessingException>(
            () => _processor.ProcessAsync(ToStream(csv), "sales.csv"));

        Assert.That(ex!.Message, Does.Contain(missingHeader));
    }

    [Test]
    public void ProcessAsync_WithInvalidAmount_ThrowsCsvProcessingException()
    {
        var csv = "Employee,Department,Amount\nAlice,Engineering,not-a-number\n";

        Assert.ThrowsAsync<CsvProcessingException>(() => _processor.ProcessAsync(ToStream(csv), "sales.csv"));
    }

    [Test]
    public void ProcessAsync_WithNegativeAmount_ThrowsCsvProcessingException()
    {
        var csv = "Employee,Department,Amount\nAlice,Engineering,-50.00\n";

        Assert.ThrowsAsync<CsvProcessingException>(() => _processor.ProcessAsync(ToStream(csv), "sales.csv"));
    }

    [Test]
    public void ProcessAsync_WithMissingEmployeeValue_ThrowsCsvProcessingException()
    {
        var csv = "Employee,Department,Amount\n,Engineering,100.00\n";

        Assert.ThrowsAsync<CsvProcessingException>(() => _processor.ProcessAsync(ToStream(csv), "sales.csv"));
    }

    [Test]
    public void ProcessAsync_WithMissingDepartmentValue_ThrowsCsvProcessingException()
    {
        var csv = "Employee,Department,Amount\nAlice,,100.00\n";

        Assert.ThrowsAsync<CsvProcessingException>(() => _processor.ProcessAsync(ToStream(csv), "sales.csv"));
    }

    [Test]
    public void ProcessAsync_WithEmptyFile_ThrowsCsvProcessingException()
    {
        Assert.ThrowsAsync<CsvProcessingException>(() => _processor.ProcessAsync(ToStream(string.Empty), "empty.csv"));
    }

    [Test]
    public void ProcessAsync_WithHeaderOnlyCsv_ThrowsCsvProcessingException()
    {
        var csv = "Employee,Department,Amount\n";

        Assert.ThrowsAsync<CsvProcessingException>(() => _processor.ProcessAsync(ToStream(csv), "sales.csv"));
    }

    private static Stream ToStream(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));
}
