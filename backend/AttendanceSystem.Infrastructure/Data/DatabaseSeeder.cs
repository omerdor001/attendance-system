using AttendanceSystem.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        // Seed users
        var admin = new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!", workFactor: 12),
            Role = "Admin",
            ExpectedShiftStartTime = new TimeOnly(8, 0),
            ExpectedShiftEndTime = new TimeOnly(17, 0),
            CreatedAt = DateTime.UtcNow
        };

        var john = new User
        {
            Username = "john.doe",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee1!", workFactor: 12),
            Role = "Employee",
            ExpectedShiftStartTime = new TimeOnly(8, 0),
            ExpectedShiftEndTime = new TimeOnly(17, 0),
            CreatedAt = DateTime.UtcNow
        };

        var jane = new User
        {
            Username = "jane.smith",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee1!", workFactor: 12),
            Role = "Employee",
            ExpectedShiftStartTime = new TimeOnly(9, 0),
            ExpectedShiftEndTime = new TimeOnly(18, 0),
            CreatedAt = DateTime.UtcNow
        };

        var bob = new User
        {
            Username = "bob.wilson",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee1!", workFactor: 12),
            Role = "Employee",
            ExpectedShiftStartTime = new TimeOnly(7, 0),
            ExpectedShiftEndTime = new TimeOnly(16, 0),
            CreatedAt = DateTime.UtcNow
        };

        db.Users.AddRange(admin, john, jane, bob);
        await db.SaveChangesAsync();

        // Seed attendance events (last 10 days)
        var now = DateTime.UtcNow;
        var events = new List<AttendanceEvent>();

        // John — normal days + late arrival + forgotten clock-out
        for (int i = 9; i >= 2; i--)
        {
            var day = now.Date.AddDays(-i);
            var clockInHour = i == 7 ? 9 : 8; // Late arrival on day -7
            events.Add(new AttendanceEvent { UserId = john.Id, EventType = "ClockIn", Timestamp = day.AddHours(clockInHour).AddMinutes(2), IsRetrospective = false });
            if (i != 5) // Forgotten clock-out on day -5
                events.Add(new AttendanceEvent { UserId = john.Id, EventType = "ClockOut", Timestamp = day.AddHours(17).AddMinutes(3), IsRetrospective = false });
        }

        // Jane — normal days + early arrival
        for (int i = 8; i >= 2; i--)
        {
            var day = now.Date.AddDays(-i);
            var clockInHour = i == 6 ? 7 : 9; // Early arrival on day -6
            events.Add(new AttendanceEvent { UserId = jane.Id, EventType = "ClockIn", Timestamp = day.AddHours(clockInHour).AddMinutes(5), IsRetrospective = false });
            events.Add(new AttendanceEvent { UserId = jane.Id, EventType = "ClockOut", Timestamp = day.AddHours(18).AddMinutes(10), IsRetrospective = false });
        }

        // Bob — with retrospective entries
        for (int i = 7; i >= 3; i--)
        {
            var day = now.Date.AddDays(-i);
            events.Add(new AttendanceEvent { UserId = bob.Id, EventType = "ClockIn", Timestamp = day.AddHours(7).AddMinutes(1), IsRetrospective = false });
            events.Add(new AttendanceEvent { UserId = bob.Id, EventType = "ClockOut", Timestamp = day.AddHours(16).AddMinutes(5), IsRetrospective = false });
        }

        db.AttendanceEvents.AddRange(events);
        await db.SaveChangesAsync();

        // Retrospective entries
        var retroDay = now.Date.AddDays(-3);
        var pendingRetro = new AttendanceEvent
        {
            UserId = bob.Id,
            EventType = "ClockIn",
            Timestamp = retroDay.AddHours(7),
            IsRetrospective = true,
            RetrospectiveReason = "Forgot to clock in, was in a morning meeting with the client",
            ApprovalStatus = "Pending",
            SubmittedAt = retroDay.AddHours(10)
        };

        var approvedRetro = new AttendanceEvent
        {
            UserId = john.Id,
            EventType = "ClockOut",
            Timestamp = now.Date.AddDays(-8).AddHours(17),
            IsRetrospective = true,
            RetrospectiveReason = "System was down when I tried to clock out, had to leave urgently",
            ApprovalStatus = "Approved",
            ApprovedByUserId = admin.Id,
            ApprovedAt = now.Date.AddDays(-7).AddHours(9),
            SubmittedAt = now.Date.AddDays(-8).AddHours(18)
        };

        var rejectedRetro = new AttendanceEvent
        {
            UserId = jane.Id,
            EventType = "ClockIn",
            Timestamp = now.Date.AddDays(-4).AddHours(9),
            IsRetrospective = true,
            RetrospectiveReason = "Forgot to clock in while working from the office lobby",
            ApprovalStatus = "Rejected",
            RejectionReason = "Entry does not match building access logs",
            SubmittedAt = now.Date.AddDays(-4).AddHours(14)
        };

        db.AttendanceEvents.AddRange(pendingRetro, approvedRetro, rejectedRetro);
        await db.SaveChangesAsync();
    }
}
