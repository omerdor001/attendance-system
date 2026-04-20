using AttendanceSystem.Core.Domain;

namespace AttendanceSystem.Core.Interfaces;

public interface IAttendanceAnalysisService
{
    Task<List<AnomalyItem>> AnalyzeUserPatternsAsync(List<AttendanceEvent> events, User user);
}
