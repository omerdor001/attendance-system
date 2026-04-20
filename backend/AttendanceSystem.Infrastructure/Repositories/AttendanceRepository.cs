using AttendanceSystem.Core.Domain;
using AttendanceSystem.Core.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Repositories;

public class AttendanceRepository(AppDbContext db) : IAttendanceRepository
{
    public Task<AttendanceEvent?> GetByIdAsync(int id) =>
        db.AttendanceEvents.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id);

    public Task<List<AttendanceEvent>> GetUserEventsAsync(int userId, DateTime from, DateTime to) =>
        db.AttendanceEvents
          .Where(e => e.UserId == userId && e.Timestamp >= from && e.Timestamp <= to)
          .OrderBy(e => e.Timestamp)
          .ToListAsync();

    public Task<List<AttendanceEvent>> GetUserEventsForDayAsync(int userId, DateTime utcDate)
    {
        var start = utcDate.Date;
        var end = start.AddDays(1);
        return db.AttendanceEvents
            .Where(e => e.UserId == userId && e.Timestamp >= start && e.Timestamp < end)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();
    }

    public Task<List<AttendanceEvent>> GetAllPendingAsync() =>
        db.AttendanceEvents
          .Include(e => e.User)
          .Where(e => e.ApprovalStatus == "Pending")
          .OrderBy(e => e.SubmittedAt)
          .ToListAsync();

    public async Task<AttendanceEvent> AddAsync(AttendanceEvent evt)
    {
        db.AttendanceEvents.Add(evt);
        await db.SaveChangesAsync();
        return evt;
    }

    public async Task<AttendanceEvent> UpdateAsync(AttendanceEvent evt)
    {
        db.AttendanceEvents.Update(evt);
        await db.SaveChangesAsync();
        return evt;
    }

    public Task<List<AttendanceEvent>> GetAllUsersEventsAsync(DateTime from, DateTime to, int? userId)
    {
        var query = db.AttendanceEvents
            .Where(e => e.Timestamp >= from && e.Timestamp <= to);
        if (userId.HasValue)
            query = query.Where(e => e.UserId == userId.Value);
        return query.OrderBy(e => e.Timestamp).ToListAsync();
    }
}
