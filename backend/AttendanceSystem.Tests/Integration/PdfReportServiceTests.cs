using AttendanceSystem.Core.Interfaces;
using AttendanceSystem.Infrastructure.ExternalServices;

namespace AttendanceSystem.Tests.Integration;

public class PdfReportServiceTests
{
    private static readonly DateTime From = new(2024, 1, 1);
    private static readonly DateTime To = new(2024, 1, 31);

    private static EmployeeReport BuildReport(
        string username = "john.doe",
        List<HistoryEntry>? entries = null,
        List<AnomalyItem>? anomalies = null,
        double totalHours = 0)
        => new(1, username, totalHours, entries ?? [], anomalies ?? []);

    private static HistoryEntry ClockIn(DateTime at, bool retro = false, string? status = null)
        => new(1, "ClockIn", at, retro, retro ? "Forgot to clock in" : null, status, null);

    private static HistoryEntry ClockOut(DateTime at, bool retro = false, string? status = null)
        => new(2, "ClockOut", at, retro, retro ? "Forgot to clock out" : null, status, null);

    private static AnomalyItem Anomaly(string type = "late_arrival")
        => new(type, "2024-01-15", $"{type} detected", 0.9);

    private static void AssertValidPdf(byte[] bytes)
    {
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0, "PDF output must not be empty");
        Assert.True(bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F',
            "Output must start with PDF magic bytes (%PDF-)");
    }

    // ── GenerateEmployeeReport ──────────────────────────────────────────────

    [Fact]
    public void GenerateEmployeeReport_NoEntriesNoAnomalies_ReturnsPdf()
    {
        var report = BuildReport();
        var pdf = PdfReportService.GenerateEmployeeReport(report, From, To);
        AssertValidPdf(pdf);
    }

    [Fact]
    public void GenerateEmployeeReport_WithClockInAndClockOut_ReturnsPdf()
    {
        var entries = new List<HistoryEntry>
        {
            ClockIn(new DateTime(2024, 1, 15, 7, 0, 0)),
            ClockOut(new DateTime(2024, 1, 15, 16, 0, 0)),
        };
        var report = BuildReport(entries: entries, totalHours: 9);
        var pdf = PdfReportService.GenerateEmployeeReport(report, From, To);
        AssertValidPdf(pdf);
    }

    [Fact]
    public void GenerateEmployeeReport_WithRetrospectivePendingEntries_ReturnsPdf()
    {
        var entries = new List<HistoryEntry>
        {
            ClockIn(new DateTime(2024, 1, 10, 8, 0, 0), retro: true, status: "Pending"),
            ClockOut(new DateTime(2024, 1, 10, 17, 0, 0), retro: true, status: "Pending"),
        };
        var report = BuildReport(entries: entries, totalHours: 9);
        var pdf = PdfReportService.GenerateEmployeeReport(report, From, To);
        AssertValidPdf(pdf);
    }

    [Fact]
    public void GenerateEmployeeReport_WithAnomalies_ReturnsPdf()
    {
        var entries = new List<HistoryEntry>
        {
            ClockIn(new DateTime(2024, 1, 15, 9, 0, 0)),
        };
        var anomalies = new List<AnomalyItem>
        {
            Anomaly("late_arrival"),
            Anomaly("forgotten_clock_out"),
        };
        var report = BuildReport(entries: entries, anomalies: anomalies);
        var pdf = PdfReportService.GenerateEmployeeReport(report, From, To);
        AssertValidPdf(pdf);
    }

    [Fact]
    public void GenerateEmployeeReport_ManyEntries_ReturnsPdf()
    {
        var entries = Enumerable.Range(1, 20)
            .SelectMany(day => new[]
            {
                ClockIn(new DateTime(2024, 1, day, 8, 0, 0)),
                ClockOut(new DateTime(2024, 1, day, 17, 0, 0)),
            })
            .ToList();
        var report = BuildReport(entries: entries, totalHours: 180);
        var pdf = PdfReportService.GenerateEmployeeReport(report, From, To);
        AssertValidPdf(pdf);
    }

    [Fact]
    public void GenerateEmployeeReport_SameDateRange_ReturnsPdf()
    {
        var singleDay = new DateTime(2024, 1, 15);
        var report = BuildReport();
        var pdf = PdfReportService.GenerateEmployeeReport(report, singleDay, singleDay);
        AssertValidPdf(pdf);
    }

    // ── GenerateAllEmployeesReport ──────────────────────────────────────────

    [Fact]
    public void GenerateAllEmployeesReport_EmptyList_ReturnsPdf()
    {
        var pdf = PdfReportService.GenerateAllEmployeesReport([], From, To);
        AssertValidPdf(pdf);
    }

    [Fact]
    public void GenerateAllEmployeesReport_SingleEmployee_ReturnsPdf()
    {
        var entries = new List<HistoryEntry>
        {
            ClockIn(new DateTime(2024, 1, 15, 8, 0, 0)),
            ClockOut(new DateTime(2024, 1, 15, 17, 0, 0)),
        };
        var reports = new List<EmployeeReport>
        {
            BuildReport(username: "alice", entries: entries, totalHours: 9),
        };
        var pdf = PdfReportService.GenerateAllEmployeesReport(reports, From, To);
        AssertValidPdf(pdf);
    }

    [Fact]
    public void GenerateAllEmployeesReport_MultipleEmployees_ReturnsPdf()
    {
        var reports = new List<EmployeeReport>
        {
            BuildReport(username: "alice", totalHours: 160),
            BuildReport(username: "bob", totalHours: 140),
            BuildReport(username: "carol", totalHours: 180),
        };

        var pdf = PdfReportService.GenerateAllEmployeesReport(reports, From, To);
        AssertValidPdf(pdf);
    }

    [Fact]
    public void GenerateAllEmployeesReport_EmployeesWithAnomalies_ReturnsPdf()
    {
        var anomalies = new List<AnomalyItem>
        {
            Anomaly("late_arrival"),
        };
        var reports = new List<EmployeeReport>
        {
            new(1, "alice", 72, [], anomalies),
            new(2, "bob", 80, [], []),
        };
        var pdf = PdfReportService.GenerateAllEmployeesReport(reports, From, To);
        AssertValidPdf(pdf);
    }

    [Fact]
    public void GenerateAllEmployeesReport_EmployeesWithRetrospectiveEntries_PendingCountIncluded_ReturnsPdf()
    {
        var entries = new List<HistoryEntry>
        {
            ClockIn(new DateTime(2024, 1, 10, 8, 0, 0), retro: true, status: "Pending"),
            ClockIn(new DateTime(2024, 1, 11, 8, 0, 0), retro: true, status: "Approved"),
        };
        var reports = new List<EmployeeReport>
        {
            new(1, "alice", 9, entries, []),
        };
        var pdf = PdfReportService.GenerateAllEmployeesReport(reports, From, To);
        AssertValidPdf(pdf);
    }

}
