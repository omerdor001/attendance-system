namespace AttendanceSystem.Core.Exceptions;

public class BusinessException(string message) : Exception(message);

public class ConflictException(string message) : BusinessException(message);

public class NotFoundException(string message) : BusinessException(message);
