using AttendanceSystem.Core.Domain;
using AttendanceSystem.Core.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(int id) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id);

    public Task<User?> GetByUsernameAsync(string username) =>
        db.Users.FirstOrDefaultAsync(u => u.Username == username);

    public Task<bool> ExistsByUsernameAsync(string username) =>
        db.Users.AnyAsync(u => u.Username == username);

    //Add to DB
    public async Task<User> AddAsync(User user) {
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public Task<List<User>> GetAllAsync() =>
        db.Users.OrderBy(u => u.Username).ToListAsync();
}
