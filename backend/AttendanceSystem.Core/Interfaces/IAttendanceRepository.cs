using AttendanceSystem.Core.Domain;

namespace AttendanceSystem.Core.Interfaces;

public interface IAttendanceRepository
{
    Task<AttendanceEvent?> GetByIdAsync(int id);
    Task<List<AttendanceEvent>> GetUserEventsAsync(int userId, DateTime from, DateTime to);
    Task<List<AttendanceEvent>> GetUserEventsForDayAsync(int userId, DateTime utcDate);
    Task<List<AttendanceEvent>> GetAllPendingAsync();
    Task<AttendanceEvent> AddAsync(AttendanceEvent evt);
    Task<AttendanceEvent> UpdateAsync(AttendanceEvent evt);
    Task<List<AttendanceEvent>> GetAllUsersEventsAsync(DateTime from, DateTime to, int? userId);
}
