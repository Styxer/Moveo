using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Task = Domain.Models.Task;
using TaskStatus = Domain.Models.TaskStatus;

namespace Infrastructure.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Project> Projects { get; set; }
        public DbSet<Task> Tasks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

         
            modelBuilder.Entity<Project>()
                .HasMany(p => p.Tasks)
                .WithOne(t => t.Project)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade); // Tasks are deleted if project is deleted

            
            modelBuilder.Entity<Project>()
                .HasIndex(p => p.OwnerId);

         
            modelBuilder.Entity<Project>()
                .HasIndex(p => new { p.OwnerId, p.Name })
                .IsUnique(); // Database-level unique constraint

            SeedInitialData(modelBuilder);
        }

        private void SeedInitialData(ModelBuilder modelBuilder)
        {
            var user1Id = "seed_user_1"; 
            var user2Id = "seed_user_2"; 

            var project1Id = Guid.NewGuid();
            var project2Id = Guid.NewGuid();
            var project3Id = Guid.NewGuid();

            modelBuilder.Entity<Project>().HasData(
                new Project
                {
                    Id = project1Id,
                    Name = "Seed Project Alpha",
                    Description = "This is the first seeded project for user 1.",
                    OwnerId = user1Id
                },
                 new Project
                 {
                     Id = project2Id,
                     Name = "Seed Project Beta",
                     Description = "This is the second seeded project for user 1.",
                     OwnerId = user1Id
                 },
                 new Project
                 {
                     Id = project3Id,
                     Name = "Seed Project Gamma",
                     Description = "This is a seeded project for user 2.",
                     OwnerId = user2Id
                 }
            );

            modelBuilder.Entity<Task>().HasData(
                new Task
                {
                    Id = Guid.NewGuid(),
                    Title = "Seed Task 1.1",
                    Description = "First task in Seed Project Alpha.",
                    Status = TaskStatus.Todo,
                    ProjectId = project1Id
                },
                 new Task
                 {
                     Id = Guid.NewGuid(),
                     Title = "Seed Task 1.2",
                     Description = "Second task in Seed Project Alpha.",
                     Status = TaskStatus.InProgress,
                     ProjectId = project1Id
                 },
                 new Task
                 {
                     Id = Guid.NewGuid(),
                     Title = "Seed Task 2.1",
                     Description = "First task in Seed Project Beta.",
                     Status = TaskStatus.Done,
                     ProjectId = project2Id
                 },
                 new Task
                 {
                     Id = Guid.NewGuid(),
                     Title = "Seed Task 3.1",
                     Description = "First task in Seed Project Gamma.",
                     Status = TaskStatus.Todo,
                     ProjectId = project3Id
                 }
            );
        }

    }
}
