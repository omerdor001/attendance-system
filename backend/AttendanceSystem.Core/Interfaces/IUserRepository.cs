using AttendanceSystem.Core.Domain;

namespace AttendanceSystem.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByUsernameAsync(string username);
    Task<bool> ExistsByUsernameAsync(string username);
    Task<User> AddAsync(User user);    //add a new user and return the created user with its assigned Id
    Task<List<User>> GetAllAsync();
}
