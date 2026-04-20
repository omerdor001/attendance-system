using AttendanceSystem.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AttendanceEvent> AttendanceEvents => Set<AttendanceEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Username).HasMaxLength(100).IsRequired();
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
            e.Property(u => u.Role).HasMaxLength(20).IsRequired();
            e.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<AttendanceEvent>(e =>
        {
            e.HasKey(ae => ae.Id);
            e.Property(ae => ae.EventType).HasMaxLength(20).IsRequired();
            e.Property(ae => ae.RetrospectiveReason).HasMaxLength(1000);
            e.Property(ae => ae.ApprovalStatus).HasMaxLength(20);
            e.Property(ae => ae.RejectionReason).HasMaxLength(500);

            e.HasOne(ae => ae.User)
             .WithMany(u => u.AttendanceEvents)
             .HasForeignKey(ae => ae.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ae => ae.ApprovedByUser)
             .WithMany()
             .HasForeignKey(ae => ae.ApprovedByUserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(ae => ae.UserId);
            e.HasIndex(ae => ae.Timestamp);
            e.HasIndex(ae => new { ae.UserId, ae.Timestamp });
        });
    }
}
