namespace AttendanceSystem.Core.Interfaces;

public interface IWorldTimeApiService
{
    Task<DateTime> GetCurrentUtcTimeAsync();
    Task<DateTimeOffset> GetCurrentZurichTimeAsync();
}
