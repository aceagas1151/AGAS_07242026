using FileProcessing.Api.Options;
using FileProcessing.Api.Services;

namespace FileProcessing.Api.Tests.Services;

[TestFixture]
public class InMemoryFileProcessingTrackerTests
{
    private static InMemoryFileProcessingTracker CreateTracker(int maximumTrackedRecords = 100)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new FileProcessingOptions { MaximumTrackedRecords = maximumTrackedRecords });
        return new InMemoryFileProcessingTracker(options);
    }

    [Test]
    public void GetReport_WithNoAttempts_ReturnsZeroedReport()
    {
        var tracker = CreateTracker();

        var report = tracker.GetReport();

        Assert.Multiple(() =>
        {
            Assert.That(report.TotalAttempts, Is.EqualTo(0));
            Assert.That(report.SuccessfulAttempts, Is.EqualTo(0));
            Assert.That(report.FailedAttempts, Is.EqualTo(0));
            Assert.That(report.AverageDurationMilliseconds, Is.EqualTo(0));
            Assert.That(report.RecentRecords, Is.Empty);
        });
    }

    [Test]
    public void RecordSuccess_IncrementsSuccessfulAndTotalAttempts()
    {
        var tracker = CreateTracker();
        var started = DateTime.UtcNow;

        tracker.RecordSuccess("sales.csv", 100, started, started.AddMilliseconds(50), rowCount: 3);

        var report = tracker.GetReport();

        Assert.Multiple(() =>
        {
            Assert.That(report.TotalAttempts, Is.EqualTo(1));
            Assert.That(report.SuccessfulAttempts, Is.EqualTo(1));
            Assert.That(report.FailedAttempts, Is.EqualTo(0));
        });
    }

    [Test]
    public void RecordFailure_IncrementsFailedAndTotalAttempts()
    {
        var tracker = CreateTracker();
        var started = DateTime.UtcNow;

        tracker.RecordFailure("bad.csv", 50, started, started.AddMilliseconds(20), "Invalid CSV.");

        var report = tracker.GetReport();

        Assert.Multiple(() =>
        {
            Assert.That(report.TotalAttempts, Is.EqualTo(1));
            Assert.That(report.SuccessfulAttempts, Is.EqualTo(0));
            Assert.That(report.FailedAttempts, Is.EqualTo(1));
            Assert.That(report.RecentRecords[0].ErrorMessage, Is.EqualTo("Invalid CSV."));
        });
    }

    [Test]
    public void GetReport_CalculatesAverageDurationAcrossAllAttempts()
    {
        var tracker = CreateTracker();
        var started = DateTime.UtcNow;

        tracker.RecordSuccess("a.csv", 10, started, started.AddMilliseconds(100), rowCount: 1);
        tracker.RecordFailure("b.csv", 10, started, started.AddMilliseconds(200), "error");

        var report = tracker.GetReport();

        Assert.That(report.AverageDurationMilliseconds, Is.EqualTo(150));
    }

    [Test]
    public void GetReport_ReturnsMostRecentRecordFirst()
    {
        var tracker = CreateTracker();
        var started = DateTime.UtcNow;

        tracker.RecordSuccess("first.csv", 10, started, started.AddMilliseconds(10), rowCount: 1);
        tracker.RecordSuccess("second.csv", 10, started, started.AddMilliseconds(10), rowCount: 1);

        var report = tracker.GetReport();

        Assert.That(report.RecentRecords[0].FileName, Is.EqualTo("second.csv"));
        Assert.That(report.RecentRecords[1].FileName, Is.EqualTo("first.csv"));
    }

    [Test]
    public void GetReport_EnforcesMaximumTrackedRecordsLimit()
    {
        var tracker = CreateTracker(maximumTrackedRecords: 2);
        var started = DateTime.UtcNow;

        tracker.RecordSuccess("first.csv", 10, started, started, rowCount: 1);
        tracker.RecordSuccess("second.csv", 10, started, started, rowCount: 1);
        tracker.RecordSuccess("third.csv", 10, started, started, rowCount: 1);

        var report = tracker.GetReport();

        Assert.That(report.RecentRecords, Has.Count.EqualTo(2));
        Assert.That(report.RecentRecords.Select(r => r.FileName), Is.EqualTo(new[] { "third.csv", "second.csv" }));
        Assert.That(report.TotalAttempts, Is.EqualTo(3));
    }

    [Test]
    public void Tracker_UnderConcurrentAccess_RecordsAllAttemptsAccurately()
    {
        var tracker = CreateTracker(maximumTrackedRecords: 1000);
        var started = DateTime.UtcNow;

        Parallel.For(0, 200, i =>
        {
            if (i % 2 == 0)
            {
                tracker.RecordSuccess($"file{i}.csv", 10, started, started.AddMilliseconds(5), rowCount: 1);
            }
            else
            {
                tracker.RecordFailure($"file{i}.csv", 10, started, started.AddMilliseconds(5), "error");
            }
        });

        var report = tracker.GetReport();

        Assert.Multiple(() =>
        {
            Assert.That(report.TotalAttempts, Is.EqualTo(200));
            Assert.That(report.SuccessfulAttempts, Is.EqualTo(100));
            Assert.That(report.FailedAttempts, Is.EqualTo(100));
        });
    }
}
