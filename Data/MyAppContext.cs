using IMS.Models;
using Microsoft.EntityFrameworkCore;

namespace IMS.Data
{
    public class MyAppContext : DbContext
    {
        public MyAppContext(DbContextOptions<MyAppContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Applicants> Applicants { get; set; }
        public DbSet<Attendance> Attendance { get; set; }
        public DbSet<Documents> Documents { get; set; }
        public DbSet<Announcements> Announcements { get; set; }
        public DbSet<TasksManage> TasksManage { get; set; }
        public DbSet<Evaluation> Evaluations { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<InternReport> Reports { get; set; }
        public DbSet<Certificate> Certificate { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TasksManage>()
                .HasOne(t => t.AssignedTo)
                .WithMany() // or .WithMany(u => u.TasksAssignedTo) if you have a collection in User
                .HasForeignKey(t => t.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict); // prevent cascade delete issues

            modelBuilder.Entity<TasksManage>()
                .HasOne(t => t.AssignedBy)
                .WithMany() // or .WithMany(u => u.TasksAssignedBy) if you have a collection in User
                .HasForeignKey(t => t.AssignedById)
                .OnDelete(DeleteBehavior.Restrict);
        }

    }
}
