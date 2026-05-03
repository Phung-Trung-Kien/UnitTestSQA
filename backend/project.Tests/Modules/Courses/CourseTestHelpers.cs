using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using project.Models;

internal static class CourseTestHelpers
{
    public static DBContext CreateInMemoryDbContext(string testName)
    {
        var options = new DbContextOptionsBuilder<DBContext>()
            .UseInMemoryDatabase($"{testName}_{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new DBContext(options);
    }

    public static User CreateUser(string userId, string fullName = "Unit Test User")
    {
        return new User
        {
            Id = userId,
            UserName = $"{userId}@test.local",
            Email = $"{userId}@test.local",
            FullName = fullName
        };
    }

    public static Category CreateCategory(string categoryId)
    {
        return new Category
        {
            Id = categoryId,
            Name = "Unit Test Category",
            Description = "Category for unit testing"
        };
    }

    public static Teacher CreateTeacher(string teacherId, string userId)
    {
        return new Teacher
        {
            TeacherId = teacherId,
            UserId = userId,
            User = CreateUser(userId, "Unit Test Teacher")
        };
    }

    public static Student CreateStudent(string studentId, string userId)
    {
        return new Student
        {
            StudentId = studentId,
            UserId = userId,
            User = CreateUser(userId, "Unit Test Student")
        };
    }
}
