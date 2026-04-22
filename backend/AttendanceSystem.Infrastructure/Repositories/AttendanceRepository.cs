using AttendanceSystem.Core.Domain;
using AttendanceSystem.Core.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Repositories;

public class AttendanceRepository(AppDbContext db) : IAttendanceRepository
{
    //Add event to DB
    public async Task<AttendanceEvent> AddAsync(AttendanceEvent evt)
    {
        db.AttendanceEvents.Add(evt);
        await db.SaveChangesAsync();
        return evt;
    }

    //Update event on DB
    public async Task<AttendanceEvent> UpdateAsync(AttendanceEvent evt)
    {
        db.AttendanceEvents.Update(evt);
        await db.SaveChangesAsync();
        return evt;
    }

    //Get event by ID
    public Task<AttendanceEvent?> GetByIdAsync(int id) =>
        db.AttendanceEvents.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id);

    public Task<List<AttendanceEvent>> GetEventsAsync(DateTime from, DateTime to, int? userId = null)
    {
        var query = db.AttendanceEvents.Where(e => e.Timestamp >= from && e.Timestamp <= to);
        if (userId.HasValue)
            query = query.Where(e => e.UserId == userId.Value);
        return query.OrderBy(e => e.Timestamp).ToListAsync();
    }

    //Get events for user to one date
    public Task<List<AttendanceEvent>> GetUserEventsForDayAsync(int userId, DateTime utcDate) {
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

}
