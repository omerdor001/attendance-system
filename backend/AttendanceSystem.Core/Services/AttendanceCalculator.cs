using AttendanceSystem.Core.Domain;
using AttendanceSystem.Core.Interfaces;

namespace AttendanceSystem.Core.Services;

public static class AttendanceCalculator
{
    private static readonly TimeZoneInfo ZurichTz =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    public static double CalculateWorkedHours(List<AttendanceEvent> events)
    {
        var sorted = events.OrderBy(e => e.Timestamp).ToList();
        double total = 0;
        AttendanceEvent? lastIn = null;

        foreach (var e in sorted)
        {
            if (e.EventType == "ClockIn") lastIn = e;
            else if (e.EventType == "ClockOut" && lastIn != null)
            {
                total += (e.Timestamp - lastIn.Timestamp).TotalHours;
                lastIn = null;
            }
        }
        return Math.Round(total, 2);
    }

    public static List<AnomalyItem> DetectAnomalies(List<AttendanceEvent> events, User user)
    {
        var anomalies = new List<AnomalyItem>();
        var byDay = events.GroupBy(e => TimeZoneInfo.ConvertTimeFromUtc(e.Timestamp, ZurichTz).Date);

        foreach (var day in byDay)
        {
            var zurichEvents = day.OrderBy(e => e.Timestamp).Select(e => new
            {
                e.EventType,
                Local = TimeZoneInfo.ConvertTimeFromUtc(e.Timestamp, ZurichTz)
            }).ToList();

            var clockIns = zurichEvents.Where(e => e.EventType == "ClockIn").ToList();
            var clockOuts = zurichEvents.Where(e => e.EventType == "ClockOut").ToList();

            if (clockIns.Count > clockOuts.Count)
                anomalies.Add(new AnomalyItem("forgotten_clock_out", day.Key.ToString("yyyy-MM-dd"),
                    $"Clock-in at {clockIns.Last().Local:HH:mm} with no matching clock-out", 0.85,
                    $"Add clock-out at {user.ExpectedShiftEndTime:HH:mm} based on user's expected shift"));

            foreach (var ci in clockIns)
            {
                var localTime = TimeOnly.FromDateTime(ci.Local);
                if (localTime > user.ExpectedShiftStartTime.AddMinutes(15))
                    anomalies.Add(new AnomalyItem("late_arrival", day.Key.ToString("yyyy-MM-dd"),
                        $"Clocked in at {ci.Local:HH:mm}, expected start time is {user.ExpectedShiftStartTime:HH:mm}", 1.0));
                else if (localTime < user.ExpectedShiftStartTime.AddMinutes(-15))
                    anomalies.Add(new AnomalyItem("early_arrival", day.Key.ToString("yyyy-MM-dd"),
                        $"Clocked in at {ci.Local:HH:mm}, expected start time is {user.ExpectedShiftStartTime:HH:mm}", 1.0));
            }

            foreach (var co in clockOuts)
            {
                var localTime = TimeOnly.FromDateTime(co.Local);
                if (localTime < user.ExpectedShiftEndTime.AddMinutes(-60))
                    anomalies.Add(new AnomalyItem("early_leave", day.Key.ToString("yyyy-MM-dd"),
                        $"Clocked out at {co.Local:HH:mm}, expected end time is {user.ExpectedShiftEndTime:HH:mm}", 1.0));
            }
        }
        return anomalies;
    }
}
