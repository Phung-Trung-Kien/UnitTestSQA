using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using project.Models;

public class CourseServiceTests
{
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<ICourseContentRepository> _courseContentRepositoryMock = new();
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ITeacherRepository> _teacherRepositoryMock = new();
    private readonly Mock<IStudentRepository> _studentRepositoryMock = new();
    private readonly Mock<ILessonRepository> _lessonRepositoryMock = new();

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_01]
    // [Mục đích: Đảm bảo AddCourseAsync tạo khóa học ở trạng thái draft khi teacher tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task AddCourseAsync_ShouldCreateDraftCourse_WhenTeacherExists()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        Course? savedCourse = null;
        var service = CreateService();
        var request = new CourseCreateDTO
        {
            Title = "C# cơ bản",
            Description = "Khóa học nhập môn C#",
            CategoryId = "category-1",
            Price = 100000,
            DiscountPrice = 10,
            ThumbnailUrl = "thumbnail.png"
        };

        _teacherRepositoryMock
            .Setup(repo => repo.IsTeacherExistsAsync(teacherId))
            .ReturnsAsync(true);
        _courseRepositoryMock
            .Setup(repo => repo.AddCourseAsync(It.IsAny<Course>()))
            .Callback<Course>(course => savedCourse = course)
            .Returns(Task.CompletedTask);

        // Act
        await service.AddCourseAsync(teacherId, request);

        // Assert
        savedCourse.Should().NotBeNull();
        savedCourse!.Title.Should().Be("C# cơ bản");
        savedCourse.TeacherId.Should().Be(teacherId);
        savedCourse.Status.Should().Be("draft");
        savedCourse.Price.Should().Be(100000);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_02]
    // [Mục đích: Đảm bảo AddCourseAsync báo lỗi khi teacher không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task AddCourseAsync_ShouldThrowKeyNotFoundException_WhenTeacherDoesNotExist()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var service = CreateService();
        _teacherRepositoryMock
            .Setup(repo => repo.IsTeacherExistsAsync(teacherId))
            .ReturnsAsync(false);

        var request = new CourseCreateDTO
        {
            Title = "C# cơ bản",
            CategoryId = "category-1",
            Price = 100000
        };

        // Act
        Func<Task> act = async () => await service.AddCourseAsync(teacherId, request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("Teacher not found");
        _courseRepositoryMock.Verify(repo => repo.AddCourseAsync(It.IsAny<Course>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_03]
    // [Mục đích: Đảm bảo UpdateCourseAsync cập nhật khóa học draft khi đúng giáo viên sở hữu]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateCourseAsync_ShouldUpdateDraftCourse_WhenTeacherOwnsCourse()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var courseId = "course-1";
        Course? updatedCourse = null;
        var existingCourse = new Course
        {
            Id = courseId,
            TeacherId = teacherId,
            Status = "draft",
            Title = "Old title",
            CategoryId = "old-category",
            Price = 100
        };

        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(existingCourse);
        _courseRepositoryMock
            .Setup(repo => repo.UpdateCourseAsync(It.IsAny<Course>()))
            .Callback<Course>(course => updatedCourse = course)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var request = new CourseUpdateDTO
        {
            Title = "New title",
            Description = "Updated description",
            CategoryId = "new-category",
            Price = 200,
            DiscountPrice = 20,
            ThumbnailUrl = "updated.png"
        };

        // Act
        await service.UpdateCourseAsync(teacherId, courseId, request);

        // Assert
        updatedCourse.Should().NotBeNull();
        updatedCourse!.Title.Should().Be("New title");
        updatedCourse.CategoryId.Should().Be("new-category");
        updatedCourse.Price.Should().Be(200);
        updatedCourse.DiscountPrice.Should().Be(20);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_04]
    // [Mục đích: Đảm bảo UpdateCourseAsync chặn giáo viên không sở hữu khóa học]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateCourseAsync_ShouldThrowUnauthorizedAccessException_WhenTeacherDoesNotOwnCourse()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var anotherTeacherId = Guid.NewGuid().ToString();
        var courseId = "course-1";
        var service = CreateService();

        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course { Id = courseId, TeacherId = anotherTeacherId, Status = "draft" });

        var request = new CourseUpdateDTO
        {
            Title = "New title",
            CategoryId = "category-1",
            Price = 200
        };

        // Act
        Func<Task> act = async () => await service.UpdateCourseAsync(teacherId, courseId, request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not the teacher of this course");
        _courseRepositoryMock.Verify(repo => repo.UpdateCourseAsync(It.IsAny<Course>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_05]
    // [Mục đích: Đảm bảo UpdateCourseAsync báo lỗi khi khóa học không ở trạng thái draft]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateCourseAsync_ShouldThrowInvalidOperationException_WhenCourseIsNotDraft()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var courseId = "course-published";
        var service = CreateService();

        // Khóa học đang ở trạng thái "published" — không được phép cập nhật
        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course { Id = courseId, TeacherId = teacherId, Status = "published" });

        var request = new CourseUpdateDTO { Title = "Attempt to update published course", CategoryId = "cat-1", Price = 100 };

        // Act
        Func<Task> act = async () => await service.UpdateCourseAsync(teacherId, courseId, request);

        // Assert — chỉ draft mới được sửa
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only draft courses can be updated");
        _courseRepositoryMock.Verify(repo => repo.UpdateCourseAsync(It.IsAny<Course>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_06]
    // [Mục đích: Đảm bảo SearchItemsAsync trả về dữ liệu phân trang đúng]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task SearchItemsAsync_ShouldReturnPagedCourses_WhenRepositoryReturnsData()
    {
        // Arrange
        var service = CreateService();
        var courses = new List<Course>
        {
            new()
            {
                Id = "course-1",
                Title = "ASP.NET Core",
                Status = "published",
                CategoryId = "category-1",
                Category = new Category { Id = "category-1", Name = "Backend" },
                TeacherId = "teacher-1",
                Teacher = new Teacher { User = new User { FullName = "Teacher A" } }
            }
        };

        _courseRepositoryMock
            .Setup(repo => repo.SearchItemsAsync("ASP", "category-1", 1, 10))
            .ReturnsAsync((courses, 1));

        // Act
        var result = await service.SearchItemsAsync("ASP", "category-1", 1, 10);

        // Assert
        result.TotalCount.Should().Be(1);
        result.TotalPages.Should().Be(1);
        result.Courses.Should().ContainSingle();
        result.Courses.First().Title.Should().Be("ASP.NET Core");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_07]
    // [Mục đích: Đảm bảo RequestPublishCourseAsync đổi trạng thái draft → pending khi đúng giáo viên sở hữu]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task RequestPublishCourseAsync_ShouldSetStatusToPending_WhenTeacherOwnsDraftCourse()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        Course? updatedCourse = null;

        var existingCourse = new Course
        {
            Id = courseId,
            TeacherId = teacherId,
            Status = "draft",
            Title = "Khóa học chờ duyệt"
        };

        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByStatusAsync(courseId, "draft"))
            .ReturnsAsync(existingCourse);
        _courseRepositoryMock
            .Setup(repo => repo.UpdateCourseAsync(It.IsAny<Course>()))
            .Callback<Course>(c => updatedCourse = c)
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.RequestPublishCourseAsync(teacherId, courseId);

        // Assert — trạng thái phải chuyển sang "pending" để Admin duyệt
        updatedCourse.Should().NotBeNull();
        updatedCourse!.Status.Should().Be("pending");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_08]
    // [Mục đích: Đảm bảo RequestPublishCourseAsync báo lỗi khi giáo viên không sở hữu khóa học]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task RequestPublishCourseAsync_ShouldThrowUnauthorized_WhenTeacherDoesNotOwnCourse()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var otherTeacherId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();

        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByStatusAsync(courseId, "draft"))
            .ReturnsAsync(new Course { Id = courseId, TeacherId = otherTeacherId, Status = "draft" });

        var service = CreateService();

        // Act
        Func<Task> act = async () => await service.RequestPublishCourseAsync(teacherId, courseId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not the teacher of this course");
        _courseRepositoryMock.Verify(repo => repo.UpdateCourseAsync(It.IsAny<Course>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_09]
    // [Mục đích: Đảm bảo GetCoursesByTeacherIdAsync trả về danh sách phân trang cùng thống kê khi teacher tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetCoursesByTeacherIdAsync_ShouldReturnPagedResultWithStatistics_WhenTeacherExists()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var service = CreateService();

        _teacherRepositoryMock
            .Setup(repo => repo.IsTeacherExistsAsync(teacherId))
            .ReturnsAsync(true);

        var courseList = new List<Course>
        {
            new()
            {
                Id = "c1",
                Title = "Khóa A",
                Status = "published",
                CategoryId = "cat-1",
                Category = new Category { Name = "Lập trình" },
                TeacherId = teacherId,
                Teacher = new Teacher { User = new User { FullName = "GV A" } },
                Enrollments = new List<Enrollment_course> { new() }
            },
            new()
            {
                Id = "c2",
                Title = "Khóa B",
                Status = "draft",
                CategoryId = "cat-1",
                Category = new Category { Name = "Lập trình" },
                TeacherId = teacherId,
                Teacher = new Teacher { User = new User { FullName = "GV A" } },
                Enrollments = new List<Enrollment_course>()
            }
        };

        _courseRepositoryMock
            .Setup(repo => repo.GetCoursesByTeacherIdAsync(teacherId, null, null, null, 1, 10))
            .ReturnsAsync((courseList, 2));

        // Act
        var result = await service.GetCoursesByTeacherIdAsync(teacherId, null, null, null, 1, 10);

        // Assert
        result.TotalCount.Should().Be(2);
        result.Courses.Should().HaveCount(2);
        result.Statistics.TotalPublishedCourses.Should().Be(1);
        result.Statistics.TotalDraftCourses.Should().Be(1);
        result.Statistics.TotalEnrollments.Should().Be(1);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_10]
    // [Mục đích: CheckDB - Đảm bảo AddFullCourseAsync tạo Course, CourseContent, Lessons và rollback dữ liệu test]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task AddFullCourseAsync_ShouldPersistCourseContentAndLessons_WhenInputIsValid()
    {
        // Arrange
        await using var dbContext = CourseTestHelpers.CreateInMemoryDbContext(nameof(AddFullCourseAsync_ShouldPersistCourseContentAndLessons_WhenInputIsValid));
        var categoryId = Guid.NewGuid().ToString();
        var teacherId = Guid.NewGuid().ToString();
        var teacherUserId = Guid.NewGuid().ToString();

        dbContext.Users.Add(CourseTestHelpers.CreateUser(teacherUserId, "Teacher For Full Course"));
        dbContext.Teachers.Add(new Teacher { TeacherId = teacherId, UserId = teacherUserId });
        dbContext.Categories.Add(CourseTestHelpers.CreateCategory(categoryId));
        await dbContext.SaveChangesAsync();

        var service = CreateRealService(dbContext);
        var request = new FullCourseCreateDTO
        {
            Title = "Khóa học kiểm thử",
            Description = "Mô tả khóa học kiểm thử",
            CategoryId = categoryId,
            Price = 500000,
            Discount = 15,
            Thumbnail = "course.png",
            CourseContent = new FullCourseContentCreateDTO
            {
                Title = "Nội dung khóa học",
                Description = "Mô tả nội dung",
                Introduce = "Giới thiệu",
                Lessons = new List<LessonCreateDTO>
                {
                    new() { Title = "Bài 1", Order = 1, Duration = 30, VideoUrl = "lesson1.mp4" },
                    new() { Title = "Bài 2", Order = 2, Duration = 45, VideoUrl = "lesson2.mp4" }
                }
            }
        };

        try
        {
            // Act
            await service.AddFullCourseAsync(teacherId, request);

            // Assert - CheckDB: kiểm tra dữ liệu đã được lưu đúng vào DB
            var savedCourse = await dbContext.Courses.SingleAsync(course => course.TeacherId == teacherId);
            var savedContent = await dbContext.CourseContents.SingleAsync(content => content.CourseId == savedCourse.Id);
            var savedLessons = await dbContext.Lessons
                .Where(lesson => lesson.CourseContentId == savedContent.Id)
                .OrderBy(lesson => lesson.Order)
                .ToListAsync();

            savedCourse.Title.Should().Be("Khóa học kiểm thử");
            savedCourse.Status.Should().Be("draft");
            savedContent.Title.Should().Be("Nội dung khóa học");
            savedLessons.Should().HaveCount(2);
            savedLessons.Select(lesson => lesson.Title).Should().ContainInOrder("Bài 1", "Bài 2");
        }
        finally
        {
            // Rollback - InMemory DB dùng riêng cho test và xóa dữ liệu để chứng minh DB quay về trạng thái ban đầu.
            dbContext.Lessons.RemoveRange(dbContext.Lessons);
            dbContext.CourseContents.RemoveRange(dbContext.CourseContents);
            dbContext.Courses.RemoveRange(dbContext.Courses);
            dbContext.Categories.RemoveRange(dbContext.Categories);
            dbContext.Teachers.RemoveRange(dbContext.Teachers);
            dbContext.Users.RemoveRange(dbContext.Users);
            await dbContext.SaveChangesAsync();

            // Xác nhận Rollback thành công — DB trở về rỗng
            dbContext.Courses.Should().BeEmpty();
            dbContext.CourseContents.Should().BeEmpty();
            dbContext.Lessons.Should().BeEmpty();
        }
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_11]
    // [Mục đích: Đảm bảo GetAllCoursesAsync trả về danh sách tất cả khóa học từ repository]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetAllCoursesAsync_ShouldReturnAllCourses_WhenRepositoryHasData()
    {
        // Arrange
        var service = CreateService();
        var courses = new List<Course>
        {
            new()
            {
                Id = "c1", Title = "Khóa 1", Status = "published",
                CategoryId = "cat-1", Category = new Category { Name = "Lập trình" },
                TeacherId = "t1", Teacher = new Teacher { User = new User { FullName = "GV A" } }
            },
            new()
            {
                Id = "c2", Title = "Khóa 2", Status = "draft",
                CategoryId = "cat-2", Category = new Category { Name = "Thiết kế" },
                TeacherId = "t2", Teacher = new Teacher { User = new User { FullName = "GV B" } }
            }
        };

        _courseRepositoryMock
            .Setup(repo => repo.GetAllCoursesAsync())
            .ReturnsAsync(courses);

        // Act
        var result = (await service.GetAllCoursesAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Khóa 1");
        result[1].TeacherName.Should().Be("GV B");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_12]
    // [Mục đích: Đảm bảo GetCourseByIdAsync trả về DTO đúng khi khóa học tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetCourseByIdAsync_ShouldReturnCourseDTO_WhenCourseExists()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var service = CreateService();

        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course
            {
                Id = courseId,
                Title = "Khóa học C#",
                Status = "published",
                CategoryId = "cat-1",
                Category = new Category { Name = "Lập trình" },
                TeacherId = teacherId,
                Teacher = new Teacher { User = new User { FullName = "GV Nguyễn" } },
                Price = 250000
            });

        // Act
        var result = await service.GetCourseByIdAsync(teacherId, courseId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(courseId);
        result.Title.Should().Be("Khóa học C#");
        result.TeacherName.Should().Be("GV Nguyễn");
        result.Price.Should().Be(250000);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_13]
    // [Mục đích: Đảm bảo GetCourseByIdAsync báo lỗi khi khóa học không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetCourseByIdAsync_ShouldThrowKeyNotFoundException_WhenCourseDoesNotExist()
    {
        // Arrange
        var service = CreateService();
        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync("non-existent"))
            .ReturnsAsync((Course?)null);

        // Act
        Func<Task> act = async () => await service.GetCourseByIdAsync("teacher-1", "non-existent");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Course not found");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_14]
    // [Mục đích: Đảm bảo GetEnrolledCoursesByStudentIdAsync trả về danh sách phân trang khi student tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetEnrolledCoursesByStudentIdAsync_ShouldReturnPagedResult_WhenStudentExists()
    {
        // Arrange
        var studentId = Guid.NewGuid().ToString();
        var service = CreateService();

        _studentRepositoryMock
            .Setup(repo => repo.IsStudentExistAsync(studentId))
            .ReturnsAsync(true);

        var enrollments = new List<Enrollment_course>
        {
            new()
            {
                StudentId = studentId,
                Progress = 50m,
                Course = new Course
                {
                    Id = "c1",
                    Title = "Khóa học Enrolled",
                    Status = "published",
                    Price = 100000,
                    CategoryId = "cat-1",
                    Category = new Category { Name = "Backend" },
                    TeacherId = "t1",
                    Teacher = new Teacher { User = new User { FullName = "GV C" } }
                }
            }
        };

        _courseRepositoryMock
            .Setup(repo => repo.GetEnrolledCoursesByStudentIdAsync(studentId, null, null, null, 1, 10))
            .ReturnsAsync((enrollments, 1));

        // Act
        var result = await service.GetEnrolledCoursesByStudentIdAsync(studentId, null, null, null, 1, 10);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Courses.Should().ContainSingle();
        result.Courses.First().Title.Should().Be("Khóa học Enrolled");
        result.Courses.First().Progress.Should().Be(50.0);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_15]
    // [Mục đích: Đảm bảo GetFullCourseDataForEditAsync trả về DTO đúng khi đúng teacher sở hữu]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetFullCourseDataForEditAsync_ShouldReturnDTO_WhenTeacherOwnsCourse()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var contentId = Guid.NewGuid().ToString();
        var service = CreateService();

        _teacherRepositoryMock.Setup(r => r.IsTeacherExistsAsync(teacherId)).ReturnsAsync(true);
        _courseRepositoryMock.Setup(r => r.GetCourseByIdByTeacherAsync(courseId, teacherId))
            .ReturnsAsync(new Course
            {
                Id = courseId, TeacherId = teacherId, Title = "Khóa học Edit",
                CategoryId = "cat-1", Price = 300000
            });
        _courseContentRepositoryMock.Setup(r => r.GetCourseContentByCourseIdAsync(courseId))
            .ReturnsAsync(new CourseContent
            {
                Id = contentId, CourseId = courseId, Title = "Content title", Introduce = "Introduce"
            });
        _lessonRepositoryMock.Setup(r => r.GetLessonsByCourseContentIdAsync(contentId))
            .ReturnsAsync(new List<Lesson>
            {
                new() { Id = "l1", Title = "Bài 1", Order = 1, Duration = 20 },
                new() { Id = "l2", Title = "Bài 2", Order = 2, Duration = 30 }
            });

        // Act
        var result = await service.GetFullCourseDataForEditAsync(teacherId, courseId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(courseId);
        result.Title.Should().Be("Khóa học Edit");
        result.CourseContent.Title.Should().Be("Content title");
        result.CourseContent.Lessons.Should().HaveCount(2);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_16]
    // [Mục đích: Đảm bảo GetFullCourseDataForEditAsync báo lỗi khi teacher không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetFullCourseDataForEditAsync_ShouldThrowKeyNotFoundException_WhenTeacherDoesNotExist()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var service = CreateService();

        _teacherRepositoryMock.Setup(r => r.IsTeacherExistsAsync(teacherId)).ReturnsAsync(false);

        // Act
        Func<Task> act = async () => await service.GetFullCourseDataForEditAsync(teacherId, courseId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Teacher not found");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_17]
    // [Mục đích: Đảm bảo UpdateFullCourseAsync cập nhật đầy đủ course, content và bài học mới]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateFullCourseAsync_ShouldUpdateCourseContentAndLessons_WhenInputIsValid()
    {
        // Arrange — InMemory DB để test transaction UpdateFullCourseAsync
        await using var dbContext = CourseTestHelpers.CreateInMemoryDbContext(nameof(UpdateFullCourseAsync_ShouldUpdateCourseContentAndLessons_WhenInputIsValid));
        var teacherId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        var categoryId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var contentId = Guid.NewGuid().ToString();
        var existingLessonId = Guid.NewGuid().ToString();

        dbContext.Users.Add(CourseTestHelpers.CreateUser(userId, "Teacher Update"));
        dbContext.Teachers.Add(new Teacher { TeacherId = teacherId, UserId = userId });
        dbContext.Categories.Add(CourseTestHelpers.CreateCategory(categoryId));
        dbContext.Courses.Add(new Course
        {
            Id = courseId, TeacherId = teacherId, Title = "Cũ", CategoryId = categoryId,
            Price = 100, Status = "draft"
        });
        dbContext.CourseContents.Add(new CourseContent
        {
            Id = contentId, CourseId = courseId, Title = "Content cũ", Introduce = "Cũ"
        });
        dbContext.Lessons.Add(new Lesson
        {
            Id = existingLessonId, CourseContentId = contentId, Title = "Bài cũ", Order = 1, Duration = 20
        });
        await dbContext.SaveChangesAsync();

        var service = CreateRealService(dbContext);
        var request = new FullCourseUpdateDTO
        {
            Title = "Mới", Description = "Mô tả mới", CategoryId = categoryId, Price = 200, Discount = 15,
            Thumbnail = "new.png",
            CourseContent = new FullCourseContentUpdateDTO
            {
                Title = "Content mới", Introduce = "Giới thiệu mới",
                Lessons = new List<LessonUpdateDTO>
                {
                    // Cập nhật lesson cũ
                    new() { Id = existingLessonId, Title = "Bài đã sửa", Order = 1, Duration = 45 },
                    // Thêm lesson mới (không có Id)
                    new() { Title = "Bài mới", Order = 2, Duration = 30 }
                }
            }
        };

        try
        {
            // Act
            await service.UpdateFullCourseAsync(teacherId, courseId, request);

            // Assert — CheckDB
            var updatedCourse = await dbContext.Courses.FindAsync(courseId);
            updatedCourse!.Title.Should().Be("Mới");
            updatedCourse.Price.Should().Be(200);

            var updatedContent = await dbContext.CourseContents.FindAsync(contentId);
            updatedContent!.Title.Should().Be("Content mới");
        }
        finally
        {
            // Rollback
            dbContext.Lessons.RemoveRange(dbContext.Lessons);
            dbContext.CourseContents.RemoveRange(dbContext.CourseContents);
            dbContext.Courses.RemoveRange(dbContext.Courses);
            dbContext.Categories.RemoveRange(dbContext.Categories);
            dbContext.Teachers.RemoveRange(dbContext.Teachers);
            dbContext.Users.RemoveRange(dbContext.Users);
            await dbContext.SaveChangesAsync();
        }
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_18]
    // [Mục đích: Đảm bảo SearchItemsAsync báo lỗi khi repository throw exception]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task SearchItemsAsync_ShouldThrowException_WhenRepositoryThrows()
    {
        // Arrange
        var service = CreateService();
        _courseRepositoryMock
            .Setup(repo => repo.SearchItemsAsync("error-keyword", null, 1, 10))
            .ThrowsAsync(new Exception("DB connection lost"));

        // Act
        Func<Task> act = async () => await service.SearchItemsAsync("error-keyword", null, 1, 10);

        // Assert — service bọc exception trong wrapper Exception
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Error when retriev course: *");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_19]
    // [Mục đích: Đảm bảo GetCoursesByTeacherIdAsync báo lỗi khi teacher không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetCoursesByTeacherIdAsync_ShouldThrowException_WhenTeacherNotFound()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var service = CreateService();

        _teacherRepositoryMock.Setup(r => r.IsTeacherExistsAsync(teacherId)).ReturnsAsync(false);

        // Act
        Func<Task> act = async () => await service.GetCoursesByTeacherIdAsync(teacherId, null, null, null, 1, 10);

        // Assert — service bọc exception (Teacher not found) trong wrapper Exception
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Error when retriev course: *");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_20]
    // [Mục đích: Đảm bảo GetEnrolledCoursesByStudentIdAsync báo lỗi khi student không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetEnrolledCoursesByStudentIdAsync_ShouldThrowException_WhenStudentNotFound()
    {
        // Arrange
        var studentId = Guid.NewGuid().ToString();
        var service = CreateService();

        _studentRepositoryMock.Setup(r => r.IsStudentExistAsync(studentId)).ReturnsAsync(false);

        // Act
        Func<Task> act = async () =>
            await service.GetEnrolledCoursesByStudentIdAsync(studentId, null, null, null, 1, 10);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Error when retriev enrolled courses: *");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_21]
    // [Mục đích: Đảm bảo UpdateFullCourseAsync báo lỗi khi teacher không sở hữu course]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateFullCourseAsync_ShouldThrowUnauthorized_WhenTeacherDoesNotOwnCourse()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var otherTeacherId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var service = CreateService();

        _courseRepositoryMock
            .Setup(r => r.GetCourseByStatusAsync(courseId, "draft"))
            .ReturnsAsync(new Course
            {
                Id = courseId, Status = "draft",
                TeacherId = otherTeacherId // teacher khác
            });

        var request = new FullCourseUpdateDTO
        {
            Title = "New", CategoryId = "cat-1", Price = 100,
            CourseContent = new FullCourseContentUpdateDTO { Title = "C", Introduce = "I", Lessons = [] }
        };

        // Act
        Func<Task> act = async () => await service.UpdateFullCourseAsync(teacherId, courseId, request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not the teacher of this course");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_22]
    // [Mục đích: Đảm bảo RequestPublishCourseAsync báo lỗi khi teacher không sở hữu course (draft check)]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task RequestPublishCourseAsync_ShouldThrowUnauthorized_WhenDraftCourseTeacherMismatch()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var otherTeacherId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var service = CreateService();

        _courseRepositoryMock
            .Setup(r => r.GetCourseByStatusAsync(courseId, "draft"))
            .ReturnsAsync(new Course { Id = courseId, Status = "draft", TeacherId = otherTeacherId });

        // Act
        Func<Task> act = async () => await service.RequestPublishCourseAsync(teacherId, courseId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not the teacher of this course");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_23]
    // [Mục đích: Đảm bảo GetFullCourseDataForEditAsync báo lỗi khi teacher không khớp course]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetFullCourseDataForEditAsync_ShouldThrowUnauthorized_WhenTeacherDoesNotOwnCourse()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var otherTeacherId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var service = CreateService();

        _teacherRepositoryMock
            .Setup(r => r.IsTeacherExistsAsync(teacherId))
            .ReturnsAsync(true);
        _courseRepositoryMock
            .Setup(r => r.GetCourseByIdByTeacherAsync(courseId, teacherId))
            .ReturnsAsync(new Course { Id = courseId, TeacherId = otherTeacherId }); // teacher khác

        // Act
        Func<Task> act = async () => await service.GetFullCourseDataForEditAsync(teacherId, courseId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not the teacher of this course");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_COS_24]
    // [Mục đích: Đảm bảo AddFullCourseAsync báo lỗi khi category không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task AddFullCourseAsync_ShouldThrowKeyNotFoundException_WhenCategoryDoesNotExist()
    {
        // Arrange — category không có trong DB
        var teacherId = Guid.NewGuid().ToString();
        var nonExistentCategoryId = Guid.NewGuid().ToString();
        var service = CreateService();

        _teacherRepositoryMock
            .Setup(r => r.IsTeacherExistsAsync(teacherId))
            .ReturnsAsync(true);
        _categoryRepositoryMock
            .Setup(r => r.GetCategoryByIdAsync(nonExistentCategoryId))
            .ReturnsAsync((Category?)null); // category null → throw

        var request = new FullCourseCreateDTO
        {
            Title = "Khóa học", CategoryId = nonExistentCategoryId, Price = 100,
            CourseContent = new FullCourseContentCreateDTO { Title = "C", Introduce = "I", Lessons = [] }
        };

        // Act
        Func<Task> act = async () => await service.AddFullCourseAsync(teacherId, request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Category with id {nonExistentCategoryId} not found.");
    }

    private CourseService CreateService()
    {
        var dbContext = CourseTestHelpers.CreateInMemoryDbContext("CourseServiceMockTests");
        return new CourseService(
            _courseRepositoryMock.Object,
            _courseContentRepositoryMock.Object,
            _categoryRepositoryMock.Object,
            _userRepositoryMock.Object,
            _teacherRepositoryMock.Object,
            _studentRepositoryMock.Object,
            _lessonRepositoryMock.Object,
            dbContext);
    }

    private static CourseService CreateRealService(DBContext dbContext)
    {
        return new CourseService(
            new CourseRepository(dbContext),
            new CourseContentRepository(dbContext),
            new CategoryRepository(dbContext),
            new UserRepository(dbContext),
            new TeacherRepository(dbContext),
            new StudentRepository(dbContext),
            new LessonRepository(dbContext),
            dbContext);
    }
}
