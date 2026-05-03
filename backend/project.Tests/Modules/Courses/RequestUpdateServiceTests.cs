using FluentAssertions;
using Moq;
using project.Models;

public class RequestUpdateServiceTests
{
    private readonly Mock<IRequestUpdateRepository> _requestUpdateRepositoryMock = new();
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<ICourseContentRepository> _courseContentRepositoryMock = new();
    private readonly Mock<ILessonRepository> _lessonRepositoryMock = new();
    private readonly Mock<ITeacherRepository> _teacherRepositoryMock = new();

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_RUS_01]
    // [Mục đích: Đảm bảo CreateRequestUpdateAsync tạo request khi target là course thuộc đúng teacher]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task CreateRequestUpdateAsync_ShouldCreateRequest_WhenCourseTargetBelongsToTeacher()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        UpdateRequestCourse? savedRequest = null;
        var service = CreateService();

        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course
            {
                Id = courseId,
                Teacher = new Teacher { User = new User { Id = userId } }
            });
        _teacherRepositoryMock
            .Setup(repo => repo.IsTeacherExistsAsync(userId))
            .ReturnsAsync(true);
        _requestUpdateRepositoryMock
            .Setup(repo => repo.CreateRequestUpdateRequestAsync(It.IsAny<UpdateRequestCourse>()))
            .Callback<UpdateRequestCourse>(request => savedRequest = request)
            .Returns(Task.CompletedTask);

        var requestDto = new RequestUpdateRequestDTO
        {
            TargetType = "course",
            TargetId = courseId,
            UpdatedDataJSON = "{\"title\":\"updated\"}"
        };

        // Act
        await service.CreateRequestUpdateAsync(userId, requestDto);

        // Assert
        savedRequest.Should().NotBeNull();
        savedRequest!.TargetType.Should().Be("course");
        savedRequest.TargetId.Should().Be(courseId);
        savedRequest.RequestById.Should().Be(userId);
        savedRequest.UpdatedDataJSON.Should().Be("{\"title\":\"updated\"}");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_RUS_02]
    // [Mục đích: Đảm bảo CreateRequestUpdateAsync báo lỗi khi TargetType không hợp lệ]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task CreateRequestUpdateAsync_ShouldThrowArgumentException_WhenTargetTypeIsInvalid()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var service = CreateService();
        var requestDto = new RequestUpdateRequestDTO
        {
            TargetType = "invalid",
            TargetId = Guid.NewGuid().ToString(),
            UpdatedDataJSON = "{}"
        };

        // Act
        Func<Task> act = async () => await service.CreateRequestUpdateAsync(userId, requestDto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid TargetType. Only 'course', 'coursecontent', 'lesson' is allowed.");
        _requestUpdateRepositoryMock.Verify(repo => repo.CreateRequestUpdateRequestAsync(It.IsAny<UpdateRequestCourse>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_RUS_03]
    // [Mục đích: Đảm bảo CreateRequestUpdateAsync báo lỗi khi TargetId hoặc UserId không phải GUID hợp lệ]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task CreateRequestUpdateAsync_ShouldThrowArgumentException_WhenTargetIdIsNotValidGuid()
    {
        // Arrange — TargetId là chuỗi tùy ý, không phải GUID hợp lệ
        var userId = Guid.NewGuid().ToString();
        var service = CreateService();
        var requestDto = new RequestUpdateRequestDTO
        {
            TargetType = "course",
            TargetId = "not-a-valid-guid", // ID không hợp lệ
            UpdatedDataJSON = "{}"
        };

        // Act
        Func<Task> act = async () => await service.CreateRequestUpdateAsync(userId, requestDto);

        // Assert — service phải từ chối ngay mà không gọi repository
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid TargetId or RequestById. It must be a valid GUID.");
        _requestUpdateRepositoryMock.Verify(repo => repo.CreateRequestUpdateRequestAsync(It.IsAny<UpdateRequestCourse>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_RUS_04]
    // [Mục đích: Đảm bảo CreateRequestUpdateAsync tạo request khi target là coursecontent thuộc đúng teacher]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task CreateRequestUpdateAsync_ShouldCreateRequest_WhenCourseContentTargetBelongsToTeacher()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var contentId = Guid.NewGuid().ToString();
        UpdateRequestCourse? savedRequest = null;

        _courseContentRepositoryMock
            .Setup(repo => repo.GetCourseContentByIdAsync(contentId))
            .ReturnsAsync(new CourseContent
            {
                Id = contentId,
                Course = new Course
                {
                    Teacher = new Teacher { User = new User { Id = userId } }
                }
            });
        _teacherRepositoryMock
            .Setup(repo => repo.IsTeacherExistsAsync(userId))
            .ReturnsAsync(true);
        _requestUpdateRepositoryMock
            .Setup(repo => repo.CreateRequestUpdateRequestAsync(It.IsAny<UpdateRequestCourse>()))
            .Callback<UpdateRequestCourse>(req => savedRequest = req)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var requestDto = new RequestUpdateRequestDTO
        {
            TargetType = "coursecontent",
            TargetId = contentId,
            UpdatedDataJSON = "{\"title\":\"new content title\"}"
        };

        // Act
        await service.CreateRequestUpdateAsync(userId, requestDto);

        // Assert
        savedRequest.Should().NotBeNull();
        savedRequest!.TargetType.Should().Be("coursecontent");
        savedRequest.TargetId.Should().Be(contentId);
        savedRequest.RequestById.Should().Be(userId);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_RUS_05]
    // [Mục đích: Đảm bảo CreateRequestUpdateAsync tạo request khi target là lesson thuộc đúng teacher]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task CreateRequestUpdateAsync_ShouldCreateRequest_WhenLessonTargetBelongsToTeacher()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var lessonId = Guid.NewGuid().ToString();
        UpdateRequestCourse? savedRequest = null;

        _lessonRepositoryMock
            .Setup(repo => repo.GetLessonByIdAsync(lessonId))
            .ReturnsAsync(new Lesson
            {
                Id = lessonId,
                CourseContent = new CourseContent
                {
                    Course = new Course
                    {
                        Teacher = new Teacher { User = new User { Id = userId } }
                    }
                }
            });
        _teacherRepositoryMock
            .Setup(repo => repo.IsTeacherExistsAsync(userId))
            .ReturnsAsync(true);
        _requestUpdateRepositoryMock
            .Setup(repo => repo.CreateRequestUpdateRequestAsync(It.IsAny<UpdateRequestCourse>()))
            .Callback<UpdateRequestCourse>(req => savedRequest = req)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var requestDto = new RequestUpdateRequestDTO
        {
            TargetType = "lesson",
            TargetId = lessonId,
            UpdatedDataJSON = "{\"title\":\"new lesson title\"}"
        };

        // Act
        await service.CreateRequestUpdateAsync(userId, requestDto);

        // Assert
        savedRequest.Should().NotBeNull();
        savedRequest!.TargetType.Should().Be("lesson");
        savedRequest.TargetId.Should().Be(lessonId);
        savedRequest.RequestById.Should().Be(userId);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_RUS_06]
    // [Mục đích: Đảm bảo CreateRequestUpdateAsync báo lỗi khi teacher không tồn tại trong hệ thống]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task CreateRequestUpdateAsync_ShouldThrowArgumentException_WhenTeacherDoesNotExist()
    {
        // Arrange — user là teacher hợp lệ (GUID) nhưng không tồn tại trong DB
        var userId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();

        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course
            {
                Id = courseId,
                Teacher = new Teacher { User = new User { Id = userId } }
            });
        // Teacher không tồn tại trong hệ thống
        _teacherRepositoryMock
            .Setup(repo => repo.IsTeacherExistsAsync(userId))
            .ReturnsAsync(false);

        var service = CreateService();
        var requestDto = new RequestUpdateRequestDTO
        {
            TargetType = "course",
            TargetId = courseId,
            UpdatedDataJSON = "{}"
        };

        // Act
        Func<Task> act = async () => await service.CreateRequestUpdateAsync(userId, requestDto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Teacher with the given RequestById does not exist.");
        _requestUpdateRepositoryMock.Verify(repo => repo.CreateRequestUpdateRequestAsync(It.IsAny<UpdateRequestCourse>()), Times.Never);
    }

    private RequestUpdateService CreateService()
    {
        return new RequestUpdateService(
            _requestUpdateRepositoryMock.Object,
            _courseRepositoryMock.Object,
            _courseContentRepositoryMock.Object,
            _lessonRepositoryMock.Object,
            _teacherRepositoryMock.Object);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_RUS_07]
    // [Mục đích: Đảm bảo CreateRequestUpdateAsync báo lỗi khi teacher không sở hữu course target]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task CreateRequestUpdateAsync_ShouldThrowArgumentException_WhenTeacherDoesNotOwnCourseTarget()
    {
        // Arrange — course tồn tại nhưng thuộc teacher khác
        var userId = Guid.NewGuid().ToString();
        var otherTeacherId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();

        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course
            {
                Id = courseId,
                Teacher = new Teacher { User = new User { Id = otherTeacherId } } // khác userId
            });

        var service = CreateService();
        var requestDto = new RequestUpdateRequestDTO
        {
            TargetType = "course",
            TargetId = courseId,
            UpdatedDataJSON = "{}"
        };

        // Act
        Func<Task> act = async () => await service.CreateRequestUpdateAsync(userId, requestDto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("You are not the teacher of this course.");
        _requestUpdateRepositoryMock.Verify(repo => repo.CreateRequestUpdateRequestAsync(It.IsAny<UpdateRequestCourse>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_RUS_08]
    // [Mục đích: Đảm bảo CreateRequestUpdateAsync báo lỗi khi teacher không sở hữu coursecontent target]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task CreateRequestUpdateAsync_ShouldThrowArgumentException_WhenTeacherDoesNotOwnContentTarget()
    {
        // Arrange — content tồn tại nhưng thuộc teacher khác
        var userId = Guid.NewGuid().ToString();
        var otherTeacherId = Guid.NewGuid().ToString();
        var contentId = Guid.NewGuid().ToString();

        _courseContentRepositoryMock
            .Setup(repo => repo.GetCourseContentByIdAsync(contentId))
            .ReturnsAsync(new CourseContent
            {
                Id = contentId,
                Course = new Course
                {
                    Teacher = new Teacher { User = new User { Id = otherTeacherId } } // khác userId
                }
            });

        var service = CreateService();
        var requestDto = new RequestUpdateRequestDTO
        {
            TargetType = "coursecontent",
            TargetId = contentId,
            UpdatedDataJSON = "{}"
        };

        // Act
        Func<Task> act = async () => await service.CreateRequestUpdateAsync(userId, requestDto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("You are not the teacher of this course content.");
        _requestUpdateRepositoryMock.Verify(repo => repo.CreateRequestUpdateRequestAsync(It.IsAny<UpdateRequestCourse>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_RUS_09]
    // [Mục đích: Đảm bảo CreateRequestUpdateAsync báo lỗi khi teacher không sở hữu lesson target]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task CreateRequestUpdateAsync_ShouldThrowArgumentException_WhenTeacherDoesNotOwnLessonTarget()
    {
        // Arrange — lesson tồn tại nhưng thuộc teacher khác
        var userId = Guid.NewGuid().ToString();
        var otherTeacherId = Guid.NewGuid().ToString();
        var lessonId = Guid.NewGuid().ToString();

        _lessonRepositoryMock
            .Setup(repo => repo.GetLessonByIdAsync(lessonId))
            .ReturnsAsync(new Lesson
            {
                Id = lessonId,
                CourseContent = new CourseContent
                {
                    Course = new Course
                    {
                        Teacher = new Teacher { User = new User { Id = otherTeacherId } } // khác userId
                    }
                }
            });

        var service = CreateService();
        var requestDto = new RequestUpdateRequestDTO
        {
            TargetType = "lesson",
            TargetId = lessonId,
            UpdatedDataJSON = "{}"
        };

        // Act
        Func<Task> act = async () => await service.CreateRequestUpdateAsync(userId, requestDto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("You are not the teacher of this lesson.");
        _requestUpdateRepositoryMock.Verify(repo => repo.CreateRequestUpdateRequestAsync(It.IsAny<UpdateRequestCourse>()), Times.Never);
    }
}

