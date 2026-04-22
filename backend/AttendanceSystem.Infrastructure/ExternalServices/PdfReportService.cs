using AttendanceSystem.Core.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF;

namespace AttendanceSystem.Infrastructure.ExternalServices;

public static class PdfReportService
{
    static PdfReportService() {
       Settings.License = LicenseType.Community;
    }

    public static byte[] GenerateEmployeeReport(EmployeeReport report, DateTime from, DateTime to) {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                ConfigurePage(page);
                page.Header().Element(c => RenderHeader(c, "Employee Attendance Report"));
                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Employee: {report.Username}").FontSize(11).Bold();
                        row.RelativeItem().AlignRight()
                           .Text($"Period: {from:yyyy-MM-dd} to {to:yyyy-MM-dd}")
                           .FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                    col.Item().PaddingTop(12).Element(c => RenderSummary(c, report));
                    col.Item().PaddingTop(12).Element(c => RenderEntriesTable(c, report.Entries));
                    if (report.Anomalies.Count > 0)
                        col.Item().PaddingTop(12).Element(c => RenderAnomalies(c, report.Anomalies));
                });
                page.Footer().Element(RenderFooter);
            });
        }).GeneratePdf();
    }

    public static byte[] GenerateAllEmployeesReport(List<EmployeeReport> reports, DateTime from, DateTime to)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                ConfigurePage(page);
                page.Content().AlignCenter().PaddingTop(180).Column(col =>
                {
                    col.Item().AlignCenter().Text("Company Attendance Report")
                       .FontSize(26).Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().AlignCenter().PaddingTop(8).Text("All Employees")
                       .FontSize(16).FontColor(Colors.Grey.Darken1);
                    col.Item().AlignCenter().PaddingTop(20)
                       .Text($"Period: {from:yyyy-MM-dd} to {to:yyyy-MM-dd}").FontSize(12);
                    col.Item().AlignCenter().PaddingTop(8)
                       .Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                       .FontSize(10).FontColor(Colors.Grey.Medium);
                    col.Item().AlignCenter().PaddingTop(8)
                       .Text($"Total Employees: {reports.Count}").FontSize(11);
                });
            });

            // Per-employee page
            foreach (var report in reports) {
                doc.Page(page =>
                {
                    ConfigurePage(page);
                    page.Header().Element(c => RenderHeader(c, $"Employee: {report.Username}"));
                    page.Content().PaddingTop(15).Column(col =>
                    {
                        col.Item().Text($"Period: {from:yyyy-MM-dd} to {to:yyyy-MM-dd}")
                           .FontSize(9).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(10).Element(c => RenderSummary(c, report));
                        col.Item().PaddingTop(10).Element(c => RenderEntriesTable(c, report.Entries));
                        if (report.Anomalies.Count > 0)
                            col.Item().PaddingTop(10).Element(c => RenderAnomalies(c, report.Anomalies));
                    });
                    page.Footer().Element(RenderFooter);
                });
            }

            // Company-wide summary page
            doc.Page(page =>
            {
                ConfigurePage(page);
                page.Header().Element(c => RenderHeader(c, "Company-Wide Summary"));
                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Item().Text($"Period: {from:yyyy-MM-dd} to {to:yyyy-MM-dd}")
                       .FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(sc =>
                    {
                        sc.Item().PaddingBottom(6).Text("Summary Statistics").Bold().FontSize(12);
                        var totalHours = reports.Sum(r => r.TotalWorkedHours);
                        var avgHours = reports.Count > 0 ? totalHours / reports.Count : 0;
                        var totalPending = reports.Sum(r => r.Entries.Count(e => e.IsRetrospective && e.ApprovalStatus == "Pending"));
                        var totalAnomalies = reports.Sum(r => r.Anomalies.Count);
                        sc.Item().Text($"Total Employees: {reports.Count}");
                        sc.Item().Text($"Total Hours Worked: {totalHours:F2}h");
                        sc.Item().Text($"Average Hours per Employee: {avgHours:F2}h");
                        sc.Item().Text($"Total Pending Retrospective Requests: {totalPending}");
                        sc.Item().Text($"Total Anomalies Detected: {totalAnomalies}");
                    });
                    var top5 = reports.OrderByDescending(r => r.TotalWorkedHours).Take(5).ToList();
                    col.Item().PaddingTop(15).Column(tc =>
                    {
                        tc.Item().PaddingBottom(6).Text("Top 5 Employees by Hours Worked").Bold().FontSize(12);
                        tc.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(30);
                                cols.RelativeColumn(3);
                                cols.RelativeColumn(2);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(6).Text("#").Bold();
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(6).Text("Employee").Bold();
                                header.Cell().Background(Colors.Blue.Lighten3).Padding(6).Text("Hours Worked").Bold();
                            });
                            for (int i = 0; i < top5.Count; i++)
                            {
                                var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                table.Cell().Background(bg).Padding(6).Text($"{i + 1}");
                                table.Cell().Background(bg).Padding(6).Text(top5[i].Username);
                                table.Cell().Background(bg).Padding(6).Text($"{top5[i].TotalWorkedHours:F2}h");
                            }
                        });
                    });
                });
                page.Footer().Element(RenderFooter);
            });
        }).GeneratePdf();
    }

    private static void ConfigurePage(PageDescriptor page) {
        page.Size(PageSizes.A4);
        page.Margin(40);
        page.DefaultTextStyle(x => x.FontSize(10));
    }

    private static void RenderHeader(IContainer container, string title) {
        container.BorderBottom(2).BorderColor(Colors.Blue.Medium).PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Text(title).Bold().FontSize(16).FontColor(Colors.Blue.Darken2);
            row.ConstantItem(130).AlignRight().AlignBottom()
               .Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd}").FontSize(8).FontColor(Colors.Grey.Medium);
        });
    }

    private static void RenderFooter(IContainer container) { 
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text("Attendance System — Confidential").FontSize(8).FontColor(Colors.Grey.Medium);
            row.ConstantItem(80).AlignRight().Text(text =>
            {
                text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                text.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                text.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private static void RenderSummary(IContainer container, EmployeeReport report) {
        var daysWorked = report.Entries
            .Where(e => e.EventType == "ClockIn")
            .Select(e => e.Timestamp.Date)
            .Distinct().Count();
        var avgHours = daysWorked > 0 ? report.TotalWorkedHours / daysWorked : 0;
        var retroPending = report.Entries.Count(e => e.IsRetrospective && e.ApprovalStatus == "Pending");
        var retroApproved = report.Entries.Count(e => e.IsRetrospective && e.ApprovalStatus == "Approved");
        var retroRejected = report.Entries.Count(e => e.IsRetrospective && e.ApprovalStatus == "Rejected");
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
        {
            col.Item().PaddingBottom(6).Text("Summary").Bold().FontSize(11);
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Total Hours Worked: {report.TotalWorkedHours:F2}h");
                    c.Item().Text($"Days Worked: {daysWorked}");
                    c.Item().Text($"Avg Hours/Day: {avgHours:F2}h");
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Retro Pending: {retroPending}");
                    c.Item().Text($"Retro Approved: {retroApproved}");
                    c.Item().Text($"Retro Rejected: {retroRejected}");
                });
            });
        });
    }

    private static void RenderEntriesTable(IContainer container, List<HistoryEntry> entries) {
        container.Column(col =>
        {
            col.Item().PaddingBottom(6).Text("Attendance Entries").Bold().FontSize(11);
            if (entries.Count == 0) {
                col.Item().Text("No attendance data available for this period.").FontColor(Colors.Grey.Medium);
                return;
            }
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2.5f);
                    cols.RelativeColumn(1.5f);
                    cols.RelativeColumn(1.5f);
                    cols.RelativeColumn(1.5f);
                });
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Blue.Lighten3).Padding(5).Text("Timestamp").Bold();
                    header.Cell().Background(Colors.Blue.Lighten3).Padding(5).Text("Type").Bold();
                    header.Cell().Background(Colors.Blue.Lighten3).Padding(5).Text("Entry Kind").Bold();
                    header.Cell().Background(Colors.Blue.Lighten3).Padding(5).Text("Status").Bold();
                });
                bool odd = true;
                foreach (var e in entries.OrderBy(e => e.Timestamp)) {
                    var bg = odd ? Colors.White : Colors.Grey.Lighten4;
                    table.Cell().Background(bg).Padding(5).Text(e.Timestamp.ToString("yyyy-MM-dd HH:mm"));
                    table.Cell().Background(bg).Padding(5).Text(e.EventType);
                    table.Cell().Background(bg).Padding(5).Text(e.IsRetrospective ? "Retrospective" : "Real-time");
                    table.Cell().Background(bg).Padding(5).Text(e.IsRetrospective ? (e.ApprovalStatus ?? "—") : "—");
                    odd = !odd;
                }
            });
        });
    }

    private static void RenderAnomalies(IContainer container, List<AnomalyItem> anomalies) {
        container.Column(col =>
        {
            col.Item().PaddingBottom(6).Text("Anomalies Detected").Bold().FontSize(11);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1.5f);
                    cols.RelativeColumn(2f);
                    cols.RelativeColumn(3f);
                });
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Orange.Lighten3).Padding(5).Text("Date").Bold();
                    header.Cell().Background(Colors.Orange.Lighten3).Padding(5).Text("Type").Bold();
                    header.Cell().Background(Colors.Orange.Lighten3).Padding(5).Text("Description").Bold();
                });
                foreach (var a in anomalies)
                {
                    table.Cell().Padding(5).Text(a.Date);
                    table.Cell().Padding(5).Text(a.Type.Replace("_", " "));
                    table.Cell().Padding(5).Text(a.Description);
                }
            });
        });
    }
}
