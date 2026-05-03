using FluentAssertions;
using Moq;
using project.Models;

public class CourseContentServiceTests
{
    private readonly Mock<ICourseContentRepository> _courseContentRepositoryMock = new();
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CCS_01]
    // [Mục đích: Đảm bảo AddCourseContentAsync thêm content khi course draft và đúng teacher]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task AddCourseContentAsync_ShouldAddContent_WhenCourseIsDraftAndTeacherOwnsCourse()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var courseId = "course-1";
        CourseContent? savedContent = null;
        var service = new CourseContentService(_courseContentRepositoryMock.Object, _courseRepositoryMock.Object);

        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course
            {
                Id = courseId,
                Status = "draft",
                Teacher = new Teacher { User = new User { Id = userId } }
            });
        _courseContentRepositoryMock
            .Setup(repo => repo.CourseContentExistsAsync(courseId))
            .ReturnsAsync(false);
        _courseContentRepositoryMock
            .Setup(repo => repo.AddCourseContentAsync(It.IsAny<CourseContent>()))
            .Callback<CourseContent>(content => savedContent = content)
            .Returns(Task.CompletedTask);

        var request = new CourseContentCreateDTO
        {
            Title = "Nội dung khóa học",
            Introduce = "Giới thiệu khóa học"
        };

        // Act
        await service.AddCourseContentAsync(userId, courseId, request);

        // Assert
        savedContent.Should().NotBeNull();
        savedContent!.CourseId.Should().Be(courseId);
        savedContent.Title.Should().Be("Nội dung khóa học");
        savedContent.Introduce.Should().Be("Giới thiệu khóa học");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CCS_02]
    // [Mục đích: Đảm bảo AddCourseContentAsync chặn thêm content khi course không ở trạng thái draft]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task AddCourseContentAsync_ShouldThrowInvalidOperationException_WhenCourseIsNotDraft()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var courseId = "course-1";
        var service = new CourseContentService(_courseContentRepositoryMock.Object, _courseRepositoryMock.Object);

        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course { Id = courseId, Status = "published" });

        var request = new CourseContentCreateDTO { Title = "Content" };

        // Act
        Func<Task> act = async () => await service.AddCourseContentAsync(userId, courseId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot add course content unless the course is in draft status");
        _courseContentRepositoryMock.Verify(repo => repo.AddCourseContentAsync(It.IsAny<CourseContent>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CCS_03]
    // [Mục đích: Đảm bảo AddCourseContentAsync chặn thêm content khi course đã có content rồi]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task AddCourseContentAsync_ShouldThrowException_WhenCourseContentAlreadyExists()
    {
        // Arrange — khóa học đã có CourseContent, không được tạo thêm
        var userId = Guid.NewGuid().ToString();
        var courseId = "course-has-content";
        var service = new CourseContentService(_courseContentRepositoryMock.Object, _courseRepositoryMock.Object);

        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course
            {
                Id = courseId,
                Status = "draft",
                Teacher = new Teacher { User = new User { Id = userId } }
            });
        // Content đã tồn tại
        _courseContentRepositoryMock
            .Setup(repo => repo.CourseContentExistsAsync(courseId))
            .ReturnsAsync(true);

        var request = new CourseContentCreateDTO { Title = "Thêm lần 2" };

        // Act
        Func<Task> act = async () => await service.AddCourseContentAsync(userId, courseId, request);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Course content already exists for this course");
        _courseContentRepositoryMock.Verify(repo => repo.AddCourseContentAsync(It.IsAny<CourseContent>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CCS_04]
    // [Mục đích: Đảm bảo UpdateCourseContentAsync cập nhật content khi đúng teacher và course draft]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateCourseContentAsync_ShouldUpdateContent_WhenInputIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var courseId = "course-1";
        var contentId = "content-1";
        CourseContent? updatedContent = null;
        var existingContent = new CourseContent
        {
            Id = contentId,
            CourseId = courseId,
            Title = "Old title",
            Introduce = "Old introduce"
        };

        var service = new CourseContentService(_courseContentRepositoryMock.Object, _courseRepositoryMock.Object);

        _courseContentRepositoryMock.Setup(repo => repo.CourseContentExistsByContentIdAsync(contentId)).ReturnsAsync(true);
        _courseContentRepositoryMock.Setup(repo => repo.GetCourseContentByIdAsync(contentId)).ReturnsAsync(existingContent);
        _courseRepositoryMock.Setup(repo => repo.GetCourseByIdAsync(courseId)).ReturnsAsync(new Course
        {
            Id = courseId,
            Status = "draft",
            Teacher = new Teacher { User = new User { Id = userId } }
        });
        _courseContentRepositoryMock
            .Setup(repo => repo.UpdateCourseContentAsync(It.IsAny<CourseContent>()))
            .Callback<CourseContent>(content => updatedContent = content)
            .Returns(Task.CompletedTask);

        var request = new CourseContentUpdateDTO
        {
            Title = "New title",
            Introduce = "New introduce"
        };

        // Act
        await service.UpdateCourseContentAsync(userId, contentId, request);

        // Assert
        updatedContent.Should().NotBeNull();
        updatedContent!.Title.Should().Be("New title");
        updatedContent.Introduce.Should().Be("New introduce");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CCS_05]
    // [Mục đích: Đảm bảo GetCourseContentInformationDTOAsync trả về DTO đúng khi course tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetCourseContentInformationDTOAsync_ShouldReturnDTO_WhenCourseExists()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var contentId = Guid.NewGuid().ToString();

        _courseRepositoryMock.Setup(repo => repo.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _courseContentRepositoryMock
            .Setup(repo => repo.GetCourseContentByCourseIdAsync(courseId))
            .ReturnsAsync(new CourseContent
            {
                Id = contentId,
                CourseId = courseId,
                Title = "Nội dung lấy được",
                Introduce = "Giới thiệu"
            });

        var service = new CourseContentService(_courseContentRepositoryMock.Object, _courseRepositoryMock.Object);

        // Act
        var result = await service.GetCourseContentInformationDTOAsync(teacherId, courseId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(contentId);
        result.Title.Should().Be("Nội dung lấy được");
        result.Introduce.Should().Be("Giới thiệu");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CCS_06]
    // [Mục đích: Đảm bảo GetCourseContentInformationDTOAsync báo lỗi khi khóa học không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetCourseContentInformationDTOAsync_ShouldThrowKeyNotFoundException_WhenCourseDoesNotExist()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var nonExistentCourseId = Guid.NewGuid().ToString();

        _courseRepositoryMock
            .Setup(repo => repo.CourseExistsAsync(nonExistentCourseId))
            .ReturnsAsync(false);

        var service = new CourseContentService(_courseContentRepositoryMock.Object, _courseRepositoryMock.Object);

        // Act
        Func<Task> act = async () => await service.GetCourseContentInformationDTOAsync(teacherId, nonExistentCourseId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Course not found");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CCS_07]
    // [Mục đích: Đảm bảo GetCourseContentOverviewDTOAsync trả về overview đúng khi đúng teacher]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetCourseContentOverviewDTOAsync_ShouldReturnOverview_WhenTeacherOwnsCourse()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var contentId = Guid.NewGuid().ToString();

        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdByTeacherAsync(courseId, teacherId))
            .ReturnsAsync(new Course { Id = courseId, TeacherId = teacherId });
        _courseContentRepositoryMock
            .Setup(repo => repo.GetCourseContentOverviewByCourseIdAsync(courseId))
            .ReturnsAsync(new CourseContent
            {
                Id = contentId,
                CourseId = courseId,
                Lessons = new List<Lesson>
                {
                    new() { Id = "lesson-1", Title = "Bài 1" },
                    new() { Id = "lesson-2", Title = "Bài 2" }
                }
            });

        var service = new CourseContentService(_courseContentRepositoryMock.Object, _courseRepositoryMock.Object);

        // Act
        var result = await service.GetCourseContentOverviewDTOAsync(teacherId, courseId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(contentId);
        result.Lessons.Should().HaveCount(2);
        result.Lessons.First().Title.Should().Be("Bài 1");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CCS_08]
    // [Mục đích: Đảm bảo GetCourseContentOverviewDTOAsync báo lỗi khi giáo viên không sở hữu khóa học]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetCourseContentOverviewDTOAsync_ShouldThrowUnauthorized_WhenTeacherDoesNotOwnCourse()
    {
        // Arrange — course thuộc teacher khác
        var teacherId = Guid.NewGuid().ToString();
        var otherTeacherId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();

        _courseRepositoryMock
            .Setup(repo => repo.GetCourseByIdByTeacherAsync(courseId, teacherId))
            .ReturnsAsync(new Course { Id = courseId, TeacherId = otherTeacherId }); // teacher khác

        var service = new CourseContentService(_courseContentRepositoryMock.Object, _courseRepositoryMock.Object);

        // Act
        Func<Task> act = async () => await service.GetCourseContentOverviewDTOAsync(teacherId, courseId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not the teacher of this course");
    }
}

