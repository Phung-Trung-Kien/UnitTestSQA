using FluentAssertions;
using Moq;
using project.Models;

public class CourseReviewServiceTests
{
    private readonly Mock<ICourseReviewRepository> _courseReviewRepositoryMock = new();
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<IStudentRepository> _studentRepositoryMock = new();
    private readonly Mock<IEnrollmentCourseRepository> _enrollmentRepositoryMock = new();

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CRS_01]
    // [Mục đích: Đảm bảo AddCourseReviewAsync tạo review mới và cập nhật AverageRating của Course]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task AddCourseReviewAsync_ShouldCreateReviewAndUpdateCourseRating_WhenInputIsValid()
    {
        // Arrange
        var courseId = Guid.NewGuid().ToString();
        var studentId = "student-1";
        CourseReview? savedReview = null;
        Course? updatedCourse = null;
        var course = new Course
        {
            Id = courseId,
            ReviewCount = 1,
            AverageRating = 4
        };

        _courseRepositoryMock.Setup(repo => repo.GetCourseByIdAsync(courseId)).ReturnsAsync(course);
        _studentRepositoryMock.Setup(repo => repo.IsStudentExistAsync(studentId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(repo => repo.IsEnrollmentExistAsync(studentId, courseId)).ReturnsAsync(true);
        _courseReviewRepositoryMock.Setup(repo => repo.CheckReviewedCourseAsync(courseId, studentId)).ReturnsAsync(false);
        _courseReviewRepositoryMock
            .Setup(repo => repo.CreateCourseReviewAsync(It.IsAny<CourseReview>()))
            .Callback<CourseReview>(review => savedReview = review)
            .Returns(Task.CompletedTask);
        _courseRepositoryMock
            .Setup(repo => repo.UpdateCourseAsync(It.IsAny<Course>()))
            .Callback<Course>(updated => updatedCourse = updated)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var request = new CourseReviewCreateDTO
        {
            Rating = 5,
            Comment = "Khóa học tốt"
        };

        // Act
        await service.AddCourseReviewAsync(courseId, studentId, request);

        // Assert
        savedReview.Should().NotBeNull();
        savedReview!.CourseId.Should().Be(courseId);
        savedReview.StudentId.Should().Be(studentId);
        savedReview.Rating.Should().Be(5);
        savedReview.IsNewest.Should().BeTrue();

        updatedCourse.Should().NotBeNull();
        updatedCourse!.ReviewCount.Should().Be(2);
        updatedCourse.AverageRating.Should().Be(4.5);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CRS_02]
    // [Mục đích: Đảm bảo AddCourseReviewAsync chặn review khi student chưa enroll khóa học]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task AddCourseReviewAsync_ShouldThrowException_WhenStudentIsNotEnrolled()
    {
        // Arrange
        var courseId = Guid.NewGuid().ToString();
        var studentId = "student-1";

        _courseRepositoryMock.Setup(repo => repo.GetCourseByIdAsync(courseId)).ReturnsAsync(new Course { Id = courseId });
        _studentRepositoryMock.Setup(repo => repo.IsStudentExistAsync(studentId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(repo => repo.IsEnrollmentExistAsync(studentId, courseId)).ReturnsAsync(false);

        var service = CreateService();
        var request = new CourseReviewCreateDTO { Rating = 4, Comment = "Good" };

        // Act
        Func<Task> act = async () => await service.AddCourseReviewAsync(courseId, studentId, request);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage($"Student with id {studentId} is not enrolled in course with id {courseId}");
        _courseReviewRepositoryMock.Verify(repo => repo.CreateCourseReviewAsync(It.IsAny<CourseReview>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CRS_03]
    // [Mục đích: Đảm bảo AddCourseReviewAsync chặn review khi student đã review khóa học này trước đó]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task AddCourseReviewAsync_ShouldThrowException_WhenStudentAlreadyReviewed()
    {
        // Arrange — student đã từng đánh giá khóa học này rồi
        var courseId = Guid.NewGuid().ToString();
        var studentId = "student-reviewed";

        _courseRepositoryMock.Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course { Id = courseId });
        _studentRepositoryMock.Setup(repo => repo.IsStudentExistAsync(studentId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(repo => repo.IsEnrollmentExistAsync(studentId, courseId)).ReturnsAsync(true);
        // Đã review trước đó
        _courseReviewRepositoryMock.Setup(repo => repo.CheckReviewedCourseAsync(courseId, studentId)).ReturnsAsync(true);

        var service = CreateService();
        var request = new CourseReviewCreateDTO { Rating = 3, Comment = "Lần 2" };

        // Act
        Func<Task> act = async () => await service.AddCourseReviewAsync(courseId, studentId, request);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage($"Student with id {studentId} has already reviewed course with id {courseId}");
        _courseReviewRepositoryMock.Verify(repo => repo.CreateCourseReviewAsync(It.IsAny<CourseReview>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CRS_04]
    // [Mục đích: Đảm bảo UpdateCourseReviewAsync tạo review mới nhất và đánh dấu review cũ không còn newest]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateCourseReviewAsync_ShouldCreateNewestReviewAndMarkOldReviewAsNotNewest()
    {
        // Arrange
        var reviewId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var oldReview = new CourseReview
        {
            Id = reviewId,
            CourseId = courseId,
            StudentId = "student-1",
            Rating = 3,
            Comment = "Old comment",
            IsNewest = true
        };
        CourseReview? newestReview = null;

        _courseReviewRepositoryMock.Setup(repo => repo.CourseReviewExistsAsync(reviewId)).ReturnsAsync(true);
        _courseReviewRepositoryMock.Setup(repo => repo.GetCourseReviewByIdAsync(reviewId)).ReturnsAsync(oldReview);
        _courseRepositoryMock.Setup(repo => repo.GetCourseByIdAsync(courseId)).ReturnsAsync(new Course
        {
            Id = courseId,
            ReviewCount = 2,
            AverageRating = 4
        });
        _courseReviewRepositoryMock
            .Setup(repo => repo.CreateCourseReviewAsync(It.IsAny<CourseReview>()))
            .Callback<CourseReview>(review => newestReview = review)
            .Returns(Task.CompletedTask);
        _courseRepositoryMock.Setup(repo => repo.UpdateCourseAsync(It.IsAny<Course>())).Returns(Task.CompletedTask);

        var service = CreateService();
        var request = new CourseReviewUpdateDTO
        {
            Rating = 5,
            Comment = "Updated comment"
        };

        // Act
        await service.UpdateCourseReviewAsync(reviewId, request);

        // Assert
        oldReview.IsNewest.Should().BeFalse();
        newestReview.Should().NotBeNull();
        newestReview!.ParentId.Should().Be(reviewId);
        newestReview.Comment.Should().Be("Updated comment");
        newestReview.Rating.Should().Be(5);
        newestReview.IsNewest.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CRS_05]
    // [Mục đích: Đảm bảo GetAllReviewsByCourseIdAsync trả về danh sách review đúng khi course tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetAllReviewsByCourseIdAsync_ShouldReturnReviewList_WhenCourseExists()
    {
        // Arrange
        var courseId = Guid.NewGuid().ToString();

        _courseRepositoryMock.Setup(repo => repo.CourseExistsAsync(courseId)).ReturnsAsync(true);

        var reviewList = new List<CourseReview>
        {
            new()
            {
                Id = Guid.NewGuid().ToString(),
                CourseId = courseId,
                StudentId = "s1",
                Rating = 4,
                Comment = "Tốt",
                IsNewest = true,
                Student = new Student { User = new User { FullName = "Học viên A", AvatarUrl = null } }
            },
            new()
            {
                Id = Guid.NewGuid().ToString(),
                CourseId = courseId,
                StudentId = "s2",
                Rating = 5,
                Comment = "Rất tốt",
                IsNewest = true,
                Student = new Student { User = new User { FullName = "Học viên B", AvatarUrl = null } }
            }
        };

        _courseReviewRepositoryMock.Setup(repo => repo.GetReviewsByCourseIdAsync(courseId))
            .ReturnsAsync(reviewList);

        var service = CreateService();

        // Act
        var result = (await service.GetAllReviewsByCourseIdAsync(courseId)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Rating.Should().Be(4);
        result[1].StudentName.Should().Be("Học viên B");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CRS_06]
    // [Mục đích: Đảm bảo GetReviewsByStudentIdAsync trả về danh sách review của student]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetReviewsByStudentIdAsync_ShouldReturnReviews_WhenStudentExists()
    {
        // Arrange
        var studentId = Guid.NewGuid().ToString();
        _studentRepositoryMock.Setup(repo => repo.IsStudentExistAsync(studentId)).ReturnsAsync(true);
        _courseReviewRepositoryMock
            .Setup(repo => repo.GetReviewsByStudentIdAsync(studentId))
            .ReturnsAsync(new List<CourseReview>
            {
                new() { Id = "r1", CourseId = "c1", StudentId = studentId, Rating = 4, Comment = "Tốt", IsNewest = true },
                new() { Id = "r2", CourseId = "c2", StudentId = studentId, Rating = 5, Comment = "Xuất sắc", IsNewest = true }
            });

        var service = CreateService();

        // Act
        var result = (await service.GetReviewsByStudentIdAsync(studentId)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Rating.Should().Be(4);
        result[1].Comment.Should().Be("Xuất sắc");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CRS_06]
    // [Mục đích: Đảm bảo CheckReviewedCourseAsync trả về true khi student đã review]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task CheckReviewedCourseAsync_ShouldReturnTrue_WhenStudentHasAlreadyReviewed()
    {
        // Arrange
        var courseId = Guid.NewGuid().ToString();
        var studentId = Guid.NewGuid().ToString();
        _courseReviewRepositoryMock
            .Setup(r => r.CheckReviewedCourseAsync(courseId, studentId))
            .ReturnsAsync(true);

        var service = CreateService();

        // Act
        var result = await service.CheckReviewedCourseAsync(courseId, studentId);

        // Assert
        result.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CRS_07]
    // [Mục đích: Đảm bảo AddCourseReviewAsync báo lỗi khi student không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task AddCourseReviewAsync_ShouldThrowException_WhenStudentDoesNotExist()
    {
        // Arrange
        var courseId = Guid.NewGuid().ToString();
        var studentId = Guid.NewGuid().ToString();

        _courseRepositoryMock
            .Setup(r => r.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course { Id = courseId, Title = "Khóa học" });
        _studentRepositoryMock
            .Setup(r => r.IsStudentExistAsync(studentId))
            .ReturnsAsync(false); // student không tồn tại

        var service = CreateService();
        var request = new CourseReviewCreateDTO { Rating = 5, Comment = "Tốt" };

        // Act
        Func<Task> act = async () => await service.AddCourseReviewAsync(courseId, studentId, request);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage($"Student with id {studentId} not found");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CRS_08]
    // [Mục đích: Đảm bảo UpdateCourseReviewAsync báo lỗi khi review không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateCourseReviewAsync_ShouldThrowException_WhenReviewDoesNotExist()
    {
        // Arrange
        var reviewId = Guid.NewGuid().ToString();
        _courseReviewRepositoryMock
            .Setup(r => r.CourseReviewExistsAsync(reviewId))
            .ReturnsAsync(false); // review không tồn tại

        var service = CreateService();
        var request = new CourseReviewUpdateDTO { Comment = "Cập nhật" };

        // Act
        Func<Task> act = async () => await service.UpdateCourseReviewAsync(reviewId, request);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage($"Review with id {reviewId} not found");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CRS_09]
    // [Mục đích: Đảm bảo GetAllReviewsByCourseIdAsync báo lỗi khi course không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetAllReviewsByCourseIdAsync_ShouldThrowException_WhenCourseDoesNotExist()
    {
        // Arrange
        var courseId = Guid.NewGuid().ToString();
        _courseRepositoryMock
            .Setup(r => r.CourseExistsAsync(courseId))
            .ReturnsAsync(false);

        var service = CreateService();

        // Act
        Func<Task> act = async () => await service.GetAllReviewsByCourseIdAsync(courseId);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage($"Course with id {courseId} not found");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CRS_10]
    // [Mục đích: Đảm bảo GetReviewsByStudentIdAsync báo lỗi khi student không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetReviewsByStudentIdAsync_ShouldThrowException_WhenStudentDoesNotExist()
    {
        // Arrange
        var studentId = Guid.NewGuid().ToString();
        _studentRepositoryMock
            .Setup(r => r.IsStudentExistAsync(studentId))
            .ReturnsAsync(false);

        var service = CreateService();

        // Act
        Func<Task> act = async () => await service.GetReviewsByStudentIdAsync(studentId);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage($"Student with id {studentId} not found");
    }

    private CourseReviewService CreateService()
    {
        return new CourseReviewService(
            _courseReviewRepositoryMock.Object,
            _courseRepositoryMock.Object,
            _studentRepositoryMock.Object,
            _enrollmentRepositoryMock.Object);
    }
}
