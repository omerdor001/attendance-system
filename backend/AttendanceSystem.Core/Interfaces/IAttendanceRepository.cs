using AttendanceSystem.Core.Domain;

namespace AttendanceSystem.Core.Interfaces;

public interface IAttendanceRepository
{
    Task<AttendanceEvent?> GetByIdAsync(int id);
    Task<List<AttendanceEvent>> GetEventsAsync(DateTime from, DateTime to, int? userId = null);
    Task<List<AttendanceEvent>> GetUserEventsForDayAsync(int userId, DateTime utcDate);
    Task<List<AttendanceEvent>> GetAllPendingAsync();
    Task<AttendanceEvent> AddAsync(AttendanceEvent evt);   //add event, used for clock-in/clock-out and retrospective event creation
    Task<AttendanceEvent> UpdateAsync(AttendanceEvent evt);   //update event, used for approval/rejection and retrospective reason updates
}
