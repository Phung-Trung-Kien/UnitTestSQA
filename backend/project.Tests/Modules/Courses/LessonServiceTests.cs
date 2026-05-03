using FluentAssertions;
using Moq;
using project.Models;

public class LessonServiceTests
{
    private readonly Mock<ILessonRepository> _lessonRepositoryMock = new();
    private readonly Mock<ICourseContentRepository> _courseContentRepositoryMock = new();
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<IEnrollmentCourseRepository> _enrollmentRepositoryMock = new();

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_LSS_01]
    // [Mục đích: Đảm bảo AddLessonAsync thêm lesson khi course draft và đúng teacher]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task AddLessonAsync_ShouldAddLesson_WhenCourseIsDraftAndTeacherOwnsCourse()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var courseContentId = "content-1";
        var courseId = "course-1";
        Lesson? savedLesson = null;
        var service = CreateService();

        _courseContentRepositoryMock
            .Setup(repo => repo.GetCourseContentByIdAsync(courseContentId))
            .ReturnsAsync(new CourseContent { Id = courseContentId, CourseId = courseId });
        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course
            {
                Id = courseId,
                Status = "draft",
                Teacher = new Teacher { User = new User { Id = userId } }
            });
        _lessonRepositoryMock
            .Setup(repo => repo.AddLessonAsync(It.IsAny<Lesson>()))
            .Callback<Lesson>(lesson => savedLesson = lesson)
            .Returns(Task.CompletedTask);

        var request = new LessonCreateDTO
        {
            Title = "Bài học 1",
            Order = 1,
            Duration = 30,
            VideoUrl = "lesson.mp4",
            TextContent = "Nội dung bài học"
        };

        // Act
        await service.AddLessonAsync(userId, courseContentId, request);

        // Assert
        savedLesson.Should().NotBeNull();
        savedLesson!.CourseContentId.Should().Be(courseContentId);
        savedLesson.Title.Should().Be("Bài học 1");
        savedLesson.Order.Should().Be(1);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_LSS_02]
    // [Mục đích: Đảm bảo AddLessonAsync chặn thêm lesson khi course không phải draft]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task AddLessonAsync_ShouldThrowException_WhenCourseIsNotDraft()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var courseContentId = "content-1";
        var courseId = "course-1";
        var service = CreateService();

        _courseContentRepositoryMock
            .Setup(repo => repo.GetCourseContentByIdAsync(courseContentId))
            .ReturnsAsync(new CourseContent { Id = courseContentId, CourseId = courseId });
        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course { Id = courseId, Status = "published" });

        var request = new LessonCreateDTO { Title = "Bài học 1", Order = 1 };

        // Act
        Func<Task> act = async () => await service.AddLessonAsync(userId, courseContentId, request);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Can only update lessons for courses in Draft status");
        _lessonRepositoryMock.Verify(repo => repo.AddLessonAsync(It.IsAny<Lesson>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_LSS_03]
    // [Mục đích: Đảm bảo GetLessonsByCourseContentIdAsync trả về danh sách lesson card đúng]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetLessonsByCourseContentIdAsync_ShouldReturnLessonCards_WhenContentExists()
    {
        // Arrange
        var courseContentId = "content-1";
        var service = CreateService();

        _courseContentRepositoryMock
            .Setup(repo => repo.CourseContentExistsByContentIdAsync(courseContentId))
            .ReturnsAsync(true);
        _lessonRepositoryMock
            .Setup(repo => repo.GetLessonsByCourseContentIdAsync(courseContentId))
            .ReturnsAsync(new List<Lesson>
            {
                new() { Id = "lesson-1", Title = "Bài 1", Order = 1, Duration = 20 },
                new() { Id = "lesson-2", Title = "Bài 2", Order = 2, Duration = 25 }
            });

        // Act
        var result = (await service.GetLessonsByCourseContentIdAsync(courseContentId)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Bài 1");
        result[1].Order.Should().Be(2);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_LSS_04]
    // [Mục đích: Đảm bảo UpdateOrderLessonsAsync báo lỗi khi danh sách lesson ID request không khớp DB]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateOrderLessonsAsync_ShouldThrowException_WhenLessonIdsDoNotMatch()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var courseContentId = "content-1";
        var courseId = "course-1";
        var service = CreateService();

        _courseContentRepositoryMock
            .Setup(repo => repo.GetCourseContentByIdAsync(courseContentId))
            .ReturnsAsync(new CourseContent { Id = courseContentId, CourseId = courseId });
        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course
            {
                Id = courseId,
                Status = "draft",
                Teacher = new Teacher { User = new User { Id = userId } }
            });
        _lessonRepositoryMock
            .Setup(repo => repo.GetLessonsByCourseContentIdAsync(courseContentId))
            .ReturnsAsync(new List<Lesson> { new() { Id = "lesson-1", Order = 1 } });

        var request = new List<LessonOrderDTO> { new() { LessonId = "another-lesson", Order = 1 } };

        // Act
        Func<Task> act = async () => await service.UpdateOrderLessonsAsync(userId, courseContentId, request);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Lesson IDs in the request do not match the existing lessons");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_LSS_05]
    // [Mục đích: Đảm bảo UpdateLessonAsync cập nhật lesson khi course draft và đúng giáo viên sở hữu]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateLessonAsync_ShouldUpdateLesson_WhenCourseIsDraftAndTeacherOwns()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var courseContentId = "content-update";
        var courseId = "course-update";
        var lessonId = "lesson-update";
        Lesson? updatedLesson = null;

        var existingLesson = new Lesson
        {
            Id = lessonId,
            Title = "Tiêu đề cũ",
            VideoUrl = "old.mp4",
            Duration = 10,
            TextContent = "Nội dung cũ"
        };

        _courseContentRepositoryMock
            .Setup(repo => repo.GetCourseContentByIdAsync(courseContentId))
            .ReturnsAsync(new CourseContent { Id = courseContentId, CourseId = courseId });
        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course
            {
                Id = courseId,
                Status = "draft",
                Teacher = new Teacher { User = new User { Id = userId } }
            });
        _lessonRepositoryMock
            .Setup(repo => repo.GetLessonByIdAsync(lessonId))
            .ReturnsAsync(existingLesson);
        _lessonRepositoryMock
            .Setup(repo => repo.UpdateLessonAsync(It.IsAny<Lesson>()))
            .Callback<Lesson>(l => updatedLesson = l)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var request = new LessonUpdateDTO
        {
            Title = "Tiêu đề mới",
            VideoUrl = "new.mp4",
            Duration = 45,
            TextContent = "Nội dung mới"
        };

        // Act
        await service.UpdateLessonAsync(userId, courseContentId, lessonId, request);

        // Assert
        updatedLesson.Should().NotBeNull();
        updatedLesson!.Title.Should().Be("Tiêu đề mới");
        updatedLesson.VideoUrl.Should().Be("new.mp4");
        updatedLesson.Duration.Should().Be(45);
        updatedLesson.TextContent.Should().Be("Nội dung mới");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_LSS_06]
    // [Mục đích: Đảm bảo UpdateOrderLessonsAsync sắp xếp thứ tự bài học thành công khi IDs khớp nhau]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateOrderLessonsAsync_ShouldUpdateLessonOrder_WhenLessonIdsMatch()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var courseContentId = "content-order";
        var courseId = "course-order";
        List<Lesson>? updatedLessons = null;

        _courseContentRepositoryMock
            .Setup(repo => repo.GetCourseContentByIdAsync(courseContentId))
            .ReturnsAsync(new CourseContent { Id = courseContentId, CourseId = courseId });
        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course
            {
                Id = courseId,
                Status = "draft",
                Teacher = new Teacher { User = new User { Id = userId } }
            });
        _lessonRepositoryMock
            .Setup(repo => repo.GetLessonsByCourseContentIdAsync(courseContentId))
            .ReturnsAsync(new List<Lesson>
            {
                new() { Id = "lesson-A", Order = 1 },
                new() { Id = "lesson-B", Order = 2 }
            });
        _lessonRepositoryMock
            .Setup(repo => repo.UpdateOrderLessonsAsync(It.IsAny<List<Lesson>>()))
            .Callback<List<Lesson>>(list => updatedLessons = list)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        // Đảo thứ tự: B lên trước, A xuống sau
        var request = new List<LessonOrderDTO>
        {
            new() { LessonId = "lesson-B", Order = 1 },
            new() { LessonId = "lesson-A", Order = 2 }
        };

        // Act
        await service.UpdateOrderLessonsAsync(userId, courseContentId, request);

        // Assert
        updatedLessons.Should().NotBeNull();
        updatedLessons.Should().HaveCount(2);
        updatedLessons!.First(l => l.Id == "lesson-B").Order.Should().Be(1);
        updatedLessons!.First(l => l.Id == "lesson-A").Order.Should().Be(2);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_LSS_07]
    // [Mục đích: Đảm bảo GetLessonByIdAsync trả về DTO đúng khi student đã enroll khóa học có lesson đó]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetLessonByIdAsync_ShouldReturnLessonDTO_WhenStudentIsEnrolled()
    {
        // Arrange
        var studentId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var courseContentId = Guid.NewGuid().ToString();
        var lessonId = Guid.NewGuid().ToString();

        _courseContentRepositoryMock
            .Setup(repo => repo.GetCourseContentByIdAsync(courseContentId))
            .ReturnsAsync(new CourseContent { Id = courseContentId, CourseId = courseId });
        _enrollmentRepositoryMock
            .Setup(repo => repo.IsEnrollmentExistAsync(studentId, courseId))
            .ReturnsAsync(true);
        _lessonRepositoryMock
            .Setup(repo => repo.GetLessonByIdAsync(lessonId))
            .ReturnsAsync(new Lesson
            {
                Id = lessonId,
                Title = "Bài học 5",
                VideoUrl = "video5.mp4",
                Duration = 35,
                Order = 5,
                TextContent = "Nội dung bài học",
                CourseContentId = courseContentId,
                CourseContent = new CourseContent
                {
                    CourseId = courseId,
                    Course = new Course { Title = "Khóa học thực hành" }
                }
            });

        var service = CreateService();

        // Act
        var result = await service.GetLessonByIdAsync(studentId, courseContentId, lessonId);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Bài học 5");
        result.VideoUrl.Should().Be("video5.mp4");
        result.Duration.Should().Be(35);
        result.Order.Should().Be(5);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_LSS_08]
    // [Mục đích: Đảm bảo AddLessonAsync báo lỗi khi giáo viên không sở hữu khóa học]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task AddLessonAsync_ShouldThrowUnauthorizedAccessException_WhenTeacherDoesNotOwnCourse()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var otherUserId = Guid.NewGuid().ToString(); // chủ sở hữu thực sự
        var courseContentId = "content-1";
        var courseId = "course-1";

        _courseContentRepositoryMock
            .Setup(r => r.GetCourseContentByIdAsync(courseContentId))
            .ReturnsAsync(new CourseContent { Id = courseContentId, CourseId = courseId });
        _courseRepositoryMock
            .Setup(r => r.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course
            {
                Id = courseId,
                Status = "draft",
                Teacher = new Teacher { User = new User { Id = otherUserId } } // khác userId
            });

        var service = CreateService();
        var request = new LessonCreateDTO { Title = "Bài học", Order = 1 };

        // Act
        Func<Task> act = async () => await service.AddLessonAsync(userId, courseContentId, request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not the teacher of this course");
        _lessonRepositoryMock.Verify(r => r.AddLessonAsync(It.IsAny<Lesson>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_LSS_09]
    // [Mục đích: Đảm bảo UpdateLessonAsync báo lỗi khi giáo viên không sở hữu khóa học]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateLessonAsync_ShouldThrowUnauthorizedAccessException_WhenTeacherDoesNotOwnCourse()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var otherUserId = Guid.NewGuid().ToString();
        var courseContentId = "content-unauth";
        var courseId = "course-unauth";
        var lessonId = "lesson-unauth";

        _courseContentRepositoryMock
            .Setup(r => r.GetCourseContentByIdAsync(courseContentId))
            .ReturnsAsync(new CourseContent { Id = courseContentId, CourseId = courseId });
        _courseRepositoryMock
            .Setup(r => r.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course
            {
                Id = courseId,
                Status = "draft",
                Teacher = new Teacher { User = new User { Id = otherUserId } }
            });

        var service = CreateService();
        var request = new LessonUpdateDTO { Title = "Tên mới" };

        // Act
        Func<Task> act = async () => await service.UpdateLessonAsync(userId, courseContentId, lessonId, request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not the teacher of this course");
        _lessonRepositoryMock.Verify(r => r.UpdateLessonAsync(It.IsAny<Lesson>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_LSS_10]
    // [Mục đích: Đảm bảo UpdateOrderLessonsAsync báo lỗi khi giáo viên không sở hữu khóa học]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateOrderLessonsAsync_ShouldThrowUnauthorizedAccessException_WhenTeacherDoesNotOwnCourse()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var otherUserId = Guid.NewGuid().ToString();
        var courseContentId = "content-order-unauth";
        var courseId = "course-order-unauth";

        _courseContentRepositoryMock
            .Setup(r => r.GetCourseContentByIdAsync(courseContentId))
            .ReturnsAsync(new CourseContent { Id = courseContentId, CourseId = courseId });
        _courseRepositoryMock
            .Setup(r => r.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course
            {
                Id = courseId,
                Status = "draft",
                Teacher = new Teacher { User = new User { Id = otherUserId } }
            });

        var service = CreateService();
        var request = new List<LessonOrderDTO> { new() { LessonId = "lesson-1", Order = 1 } };

        // Act
        Func<Task> act = async () => await service.UpdateOrderLessonsAsync(userId, courseContentId, request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not the teacher of this course");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_LSS_11]
    // [Mục đích: Đảm bảo GetLessonsByCourseContentIdAsync báo lỗi khi courseContent không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetLessonsByCourseContentIdAsync_ShouldThrowKeyNotFoundException_WhenContentDoesNotExist()
    {
        // Arrange
        var courseContentId = "not-found-content";
        _courseContentRepositoryMock
            .Setup(r => r.CourseContentExistsByContentIdAsync(courseContentId))
            .ReturnsAsync(false);

        var service = CreateService();

        // Act
        Func<Task> act = async () => await service.GetLessonsByCourseContentIdAsync(courseContentId);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage($"Course content with id: {courseContentId} not found");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_LSS_12]
    // [Mục đích: Đảm bảo GetLessonByIdAsync báo lỗi khi student chưa enroll khóa học]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetLessonByIdAsync_ShouldThrowUnauthorizedAccessException_WhenStudentNotEnrolled()
    {
        // Arrange
        var studentId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var courseContentId = Guid.NewGuid().ToString();
        var lessonId = Guid.NewGuid().ToString();

        _courseContentRepositoryMock
            .Setup(r => r.GetCourseContentByIdAsync(courseContentId))
            .ReturnsAsync(new CourseContent { Id = courseContentId, CourseId = courseId });
        // Student chưa enroll → trả về false
        _enrollmentRepositoryMock
            .Setup(r => r.IsEnrollmentExistAsync(studentId, courseId))
            .ReturnsAsync(false);

        var service = CreateService();

        // Act
        Func<Task> act = async () => await service.GetLessonByIdAsync(studentId, courseContentId, lessonId);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("You are not enrolled in this course");
    }

    private LessonService CreateService()
    {
        return new LessonService(
            _lessonRepositoryMock.Object,
            _courseContentRepositoryMock.Object,
            _courseRepositoryMock.Object,
            _enrollmentRepositoryMock.Object);
    }
}
