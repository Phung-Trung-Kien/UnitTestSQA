using FluentAssertions;
using Moq;
using project.Models;

public class EnrollmentCourseServiceTests
{
    private readonly Mock<IEnrollmentCourseRepository> _enrollmentRepositoryMock = new();
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<IStudentRepository> _studentRepositoryMock = new();
    private readonly Mock<ICourseContentRepository> _courseContentRepositoryMock = new();
    private readonly Mock<ILessonRepository> _lessonRepositoryMock = new();
    private readonly Mock<IExamRepository> _examRepositoryMock = new();
    private readonly Mock<ILessonProgressRepository> _lessonProgressRepositoryMock = new();
    private readonly Mock<ISubmissionExamRepository> _submissionExamRepositoryMock = new();
    private readonly Mock<IRequestRefundCourseRepository> _requestRefundCourseRepositoryMock = new();
    private readonly Mock<IAdminRepository> _adminRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_01]
    // [Mục đích: Đảm bảo CreateEnrollmentAsync tạo enrollment active khi course và student hợp lệ]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task CreateEnrollmentAsync_ShouldCreateActiveEnrollment_WhenCourseAndStudentExist()
    {
        // Arrange
        var courseId = Guid.NewGuid().ToString();
        var studentId = "student-1";
        Enrollment_course? savedEnrollment = null;

        _courseRepositoryMock.Setup(repo => repo.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _studentRepositoryMock.Setup(repo => repo.IsStudentExistAsync(studentId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(repo => repo.IsEnrollmentExistAsync(studentId, courseId)).ReturnsAsync(false);
        _enrollmentRepositoryMock
            .Setup(repo => repo.CreateEnrollmentAsync(It.IsAny<Enrollment_course>()))
            .Callback<Enrollment_course>(enrollment => savedEnrollment = enrollment)
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.CreateEnrollmentAsync(courseId, studentId);

        // Assert
        savedEnrollment.Should().NotBeNull();
        savedEnrollment!.CourseId.Should().Be(courseId);
        savedEnrollment.StudentId.Should().Be(studentId);
        savedEnrollment.Status.Should().Be("active");
        savedEnrollment.Progress.Should().Be(0.00m);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_02]
    // [Mục đích: Đảm bảo CreateEnrollmentAsync chặn enroll trùng khóa học]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task CreateEnrollmentAsync_ShouldThrowException_WhenEnrollmentAlreadyExists()
    {
        // Arrange
        var courseId = Guid.NewGuid().ToString();
        var studentId = "student-1";

        _courseRepositoryMock.Setup(repo => repo.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _studentRepositoryMock.Setup(repo => repo.IsStudentExistAsync(studentId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(repo => repo.IsEnrollmentExistAsync(studentId, courseId)).ReturnsAsync(true);

        var service = CreateService();

        // Act
        Func<Task> act = async () => await service.CreateEnrollmentAsync(courseId, studentId);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Student has already enrolled in this course");
        _enrollmentRepositoryMock.Verify(repo => repo.CreateEnrollmentAsync(It.IsAny<Enrollment_course>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_03]
    // [Mục đích: Đảm bảo CreateEnrollmentAsync báo lỗi khi student không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task CreateEnrollmentAsync_ShouldThrowKeyNotFoundException_WhenStudentDoesNotExist()
    {
        // Arrange
        var courseId = Guid.NewGuid().ToString();
        var nonExistentStudentId = Guid.NewGuid().ToString(); // Student không có trong hệ thống

        _courseRepositoryMock.Setup(repo => repo.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _studentRepositoryMock.Setup(repo => repo.IsStudentExistAsync(nonExistentStudentId)).ReturnsAsync(false);

        var service = CreateService();

        // Act
        Func<Task> act = async () => await service.CreateEnrollmentAsync(courseId, nonExistentStudentId);

        // Assert — phải ném KeyNotFoundException với thông báo chứa studentId
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Student with id: {nonExistentStudentId} not found");
        _enrollmentRepositoryMock.Verify(repo => repo.CreateEnrollmentAsync(It.IsAny<Enrollment_course>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_04]
    // [Mục đích: Đảm bảo UpdateProgressEnrollmentAsync thêm LessonProgress và cập nhật progress khi hoàn thành lesson]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateProgressEnrollmentAsync_ShouldAddLessonProgressAndUpdateProgress_WhenLessonCompleted()
    {
        // Arrange
        var courseId = Guid.NewGuid().ToString();
        var studentId = Guid.NewGuid().ToString();
        var lessonId = Guid.NewGuid().ToString();
        var enrollment = new Enrollment_course
        {
            Id = "enrollment-1",
            CourseId = courseId,
            StudentId = studentId,
            Status = "active",
            Progress = 0
        };
        LessonProgress? savedProgress = null;
        Enrollment_course? updatedEnrollment = null;

        _courseRepositoryMock.Setup(repo => repo.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(repo => repo.GetEnrollmentByStudentAndCourseIdAsync(studentId, courseId)).ReturnsAsync(enrollment);
        _lessonRepositoryMock.Setup(repo => repo.LessonExistsAsync(lessonId)).ReturnsAsync(true);
        _lessonProgressRepositoryMock.Setup(repo => repo.ExistsAsync(lessonId, studentId)).ReturnsAsync(false);
        _lessonProgressRepositoryMock
            .Setup(repo => repo.AddNewLessonProgressAsync(It.IsAny<LessonProgress>()))
            .Callback<LessonProgress>(progress => savedProgress = progress)
            .Returns(Task.CompletedTask);
        _courseContentRepositoryMock.Setup(repo => repo.TotalLessons(courseId)).ReturnsAsync(1);
        _lessonProgressRepositoryMock.Setup(repo => repo.CountCompletedLessonsAsync(courseId, studentId)).ReturnsAsync(1);
        _examRepositoryMock.Setup(repo => repo.TotalExamsInCourseAsync(courseId)).ReturnsAsync(0);
        _submissionExamRepositoryMock.Setup(repo => repo.CountPassExamsAsync(courseId, studentId, 70)).ReturnsAsync(0);
        _enrollmentRepositoryMock
            .Setup(repo => repo.UpdateProgressEnrollmentAsync(It.IsAny<Enrollment_course>()))
            .Callback<Enrollment_course>(value => updatedEnrollment = value)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var request = new EnrollmentProgressUpdateDTO { LessonId = lessonId };

        // Act
        await service.UpdateProgressEnrollmentAsync(studentId, courseId, request);

        // Assert
        savedProgress.Should().NotBeNull();
        savedProgress!.LessonId.Should().Be(lessonId);
        savedProgress.StudentId.Should().Be(studentId);

        updatedEnrollment.Should().NotBeNull();
        updatedEnrollment!.Progress.Should().BeApproximately(70m, 0.01m);
        updatedEnrollment.Status.Should().Be("active");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_05]
    // [Mục đích: Đảm bảo UpdateProgressEnrollmentAsync không cập nhật lại nếu lesson đã hoàn thành trước đó]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateProgressEnrollmentAsync_ShouldReturnWithoutUpdating_WhenLessonAlreadyCompleted()
    {
        // Arrange
        var courseId = Guid.NewGuid().ToString();
        var studentId = Guid.NewGuid().ToString();
        var lessonId = Guid.NewGuid().ToString();

        _courseRepositoryMock.Setup(repo => repo.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _enrollmentRepositoryMock
            .Setup(repo => repo.GetEnrollmentByStudentAndCourseIdAsync(studentId, courseId))
            .ReturnsAsync(new Enrollment_course { CourseId = courseId, StudentId = studentId, Status = "active" });
        _lessonRepositoryMock.Setup(repo => repo.LessonExistsAsync(lessonId)).ReturnsAsync(true);
        _lessonProgressRepositoryMock.Setup(repo => repo.ExistsAsync(lessonId, studentId)).ReturnsAsync(true);

        var service = CreateService();
        var request = new EnrollmentProgressUpdateDTO { LessonId = lessonId };

        // Act
        await service.UpdateProgressEnrollmentAsync(studentId, courseId, request);

        // Assert
        _lessonProgressRepositoryMock.Verify(repo => repo.AddNewLessonProgressAsync(It.IsAny<LessonProgress>()), Times.Never);
        _enrollmentRepositoryMock.Verify(repo => repo.UpdateProgressEnrollmentAsync(It.IsAny<Enrollment_course>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_06]
    // [Mục đích: Đảm bảo RequestCancelEnrollmentAsync tạo refund request và đổi trạng thái enrollment khi đủ điều kiện]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task RequestCancelEnrollmentAsync_ShouldCreateRefundRequestAndMarkEnrollmentPendingRefund_WhenEligible()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var enrollmentId = Guid.NewGuid().ToString();
        var studentId = "student-1";
        var enrollment = new Enrollment_course
        {
            Id = enrollmentId,
            StudentId = studentId,
            CourseId = courseId,
            Status = "active",
            Progress = 20,
            EnrolledAt = DateTime.UtcNow.AddHours(-2),
            Student = new Student { StudentId = studentId, User = new User { Id = userId } }
        };
        RefundRequestCourse? savedRefundRequest = null;
        Enrollment_course? updatedEnrollment = null;

        _courseRepositoryMock.Setup(repo => repo.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(repo => repo.GetEnrrollmentByIdAsync(enrollmentId)).ReturnsAsync(enrollment);
        _userRepositoryMock.Setup(repo => repo.IsUserExistAsync(userId)).ReturnsAsync(true);
        _courseRepositoryMock.Setup(repo => repo.GetCourseByIdAsync(courseId)).ReturnsAsync(new Course
        {
            Id = courseId,
            Price = 1000
        });
        _enrollmentRepositoryMock
            .Setup(repo => repo.UpdateProgressEnrollmentAsync(It.IsAny<Enrollment_course>()))
            .Callback<Enrollment_course>(value => updatedEnrollment = value)
            .Returns(Task.CompletedTask);
        _requestRefundCourseRepositoryMock
            .Setup(repo => repo.CreateRequestRefundCourseAsync(It.IsAny<RefundRequestCourse>()))
            .Callback<RefundRequestCourse>(request => savedRefundRequest = request)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var request = new RequestCancelEnrollmentDTO { ReasonRequest = "Không còn nhu cầu học" };

        // Act
        await service.RequestCancelEnrollmentAsync(userId, courseId, enrollmentId, request);

        // Assert - CheckDB bằng dữ liệu truyền vào repository mock
        updatedEnrollment.Should().NotBeNull();
        updatedEnrollment!.Status.Should().Be("Peding Refund");

        savedRefundRequest.Should().NotBeNull();
        savedRefundRequest!.EnrollmentId.Should().Be(enrollmentId);
        savedRefundRequest.StudentId.Should().Be(studentId);
        savedRefundRequest.RefundAmount.Should().Be(800);
        savedRefundRequest.ProgressSnapshot.Should().Be(20);
        savedRefundRequest.Reason.Should().Be("Không còn nhu cầu học");
        savedRefundRequest.Status.Should().Be("pending");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_07]
    // [Mục đích: Đảm bảo RequestCancelEnrollmentAsync chặn hoàn tiền khi khóa học miễn phí (Price <= 0)]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task RequestCancelEnrollmentAsync_ShouldThrowException_WhenCourseIsFree()
    {
        // Arrange — khóa học miễn phí (Price = 0)
        var userId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var enrollmentId = Guid.NewGuid().ToString();
        var studentId = "student-free";

        var enrollment = new Enrollment_course
        {
            Id = enrollmentId,
            StudentId = studentId,
            CourseId = courseId,
            Status = "active",
            Progress = 0,
            EnrolledAt = DateTime.UtcNow.AddHours(-1),
            Student = new Student { StudentId = studentId, User = new User { Id = userId } }
        };

        _courseRepositoryMock.Setup(repo => repo.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(repo => repo.GetEnrrollmentByIdAsync(enrollmentId)).ReturnsAsync(enrollment);
        _userRepositoryMock.Setup(repo => repo.IsUserExistAsync(userId)).ReturnsAsync(true);
        // Khóa học miễn phí: Price = 0
        _courseRepositoryMock.Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course { Id = courseId, Price = 0 });

        var service = CreateService();
        var request = new RequestCancelEnrollmentDTO { ReasonRequest = "Muốn hoàn tiền" };

        // Act
        Func<Task> act = async () => await service.RequestCancelEnrollmentAsync(userId, courseId, enrollmentId, request);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Free courses are not eligible for refunds.");
        _requestRefundCourseRepositoryMock.Verify(repo => repo.CreateRequestRefundCourseAsync(It.IsAny<RefundRequestCourse>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_08]
    // [Mục đích: Đảm bảo RequestCancelEnrollmentAsync chặn hoàn tiền khi đã quá 2 ngày kể từ khi enroll]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task RequestCancelEnrollmentAsync_ShouldThrowException_WhenRefundPeriodExceeded()
    {
        // Arrange — đã enroll hơn 2 ngày trước
        var userId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var enrollmentId = Guid.NewGuid().ToString();
        var studentId = "student-late";

        var enrollment = new Enrollment_course
        {
            Id = enrollmentId,
            StudentId = studentId,
            CourseId = courseId,
            Status = "active",
            Progress = 10,
            EnrolledAt = DateTime.UtcNow.AddDays(-3), // Enroll 3 ngày trước — đã quá hạn
            Student = new Student { StudentId = studentId, User = new User { Id = userId } }
        };

        _courseRepositoryMock.Setup(repo => repo.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(repo => repo.GetEnrrollmentByIdAsync(enrollmentId)).ReturnsAsync(enrollment);
        _userRepositoryMock.Setup(repo => repo.IsUserExistAsync(userId)).ReturnsAsync(true);
        _courseRepositoryMock.Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course { Id = courseId, Price = 500000 });

        var service = CreateService();
        var request = new RequestCancelEnrollmentDTO { ReasonRequest = "Muốn đổi khóa học" };

        // Act
        Func<Task> act = async () => await service.RequestCancelEnrollmentAsync(userId, courseId, enrollmentId, request);

        // Assert — quá 2 ngày không được hoàn tiền
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Refund requests are only accepted within 2 days of enrollment.");
        _requestRefundCourseRepositoryMock.Verify(repo => repo.CreateRequestRefundCourseAsync(It.IsAny<RefundRequestCourse>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_09]
    // [Mục đích: Đảm bảo RequestCancelEnrollmentAsync chặn hoàn tiền khi tiến độ học >= 50%]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task RequestCancelEnrollmentAsync_ShouldThrowException_WhenProgressIsAtOrAbove50Percent()
    {
        // Arrange — student đã học được 60% khóa học
        var userId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var enrollmentId = Guid.NewGuid().ToString();
        var studentId = "student-advanced";

        var enrollment = new Enrollment_course
        {
            Id = enrollmentId,
            StudentId = studentId,
            CourseId = courseId,
            Status = "active",
            Progress = 60, // Đã vượt ngưỡng 50%
            EnrolledAt = DateTime.UtcNow.AddHours(-5),
            Student = new Student { StudentId = studentId, User = new User { Id = userId } }
        };

        _courseRepositoryMock.Setup(repo => repo.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(repo => repo.GetEnrrollmentByIdAsync(enrollmentId)).ReturnsAsync(enrollment);
        _userRepositoryMock.Setup(repo => repo.IsUserExistAsync(userId)).ReturnsAsync(true);
        _courseRepositoryMock.Setup(repo => repo.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course { Id = courseId, Price = 500000 });

        var service = CreateService();
        var request = new RequestCancelEnrollmentDTO { ReasonRequest = "Không phù hợp" };

        // Act
        Func<Task> act = async () => await service.RequestCancelEnrollmentAsync(userId, courseId, enrollmentId, request);

        // Assert — tiến độ >= 50% không được hoàn tiền
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Refund not available for courses with progress \u2265 50%.");
        _requestRefundCourseRepositoryMock.Verify(repo => repo.CreateRequestRefundCourseAsync(It.IsAny<RefundRequestCourse>()), Times.Never);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_10]
    // [Mục đích: Đảm bảo GetEnrollmentByIdAsync trả về DTO đúng khi đúng thông tin student và enrollment]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetEnrollmentByIdAsync_ShouldReturnEnrollmentDTO_WhenStudentOwnsEnrollment()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var enrollmentId = Guid.NewGuid().ToString();
        var studentId = "student-get";

        var enrollment = new Enrollment_course
        {
            Id = enrollmentId,
            StudentId = studentId,
            CourseId = courseId,
            Status = "active",
            Progress = 30,
            Course = new Course { Title = "Khóa học X" },
            Student = new Student { StudentId = studentId, User = new User { Id = userId, FullName = "Học viên A" } }
        };

        _courseRepositoryMock.Setup(repo => repo.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _userRepositoryMock.Setup(repo => repo.IsUserExistAsync(userId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(repo => repo.GetEnrrollmentByIdAsync(enrollmentId)).ReturnsAsync(enrollment);

        var service = CreateService();

        // Act
        var result = await service.GetEnrollmentByIdAsync(userId, courseId, enrollmentId);

        // Assert
        result.Should().NotBeNull();
        result.StudentName.Should().Be("Học viên A");
        result.Progress.Should().Be(30);
        result.Status.Should().Be("active");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_11]
    // [Mục đích: Đảm bảo IsEnrolledInCourseAsync trả về true khi student đã enroll]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task IsEnrolledInCourseAsync_ShouldReturnTrue_WhenStudentIsEnrolled()
    {
        // Arrange
        var studentId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        _enrollmentRepositoryMock.Setup(r => r.IsEnrollmentExistAsync(studentId, courseId)).ReturnsAsync(true);
        var service = CreateService();

        // Act
        var result = await service.IsEnrolledInCourseAsync(studentId, courseId);

        // Assert
        result.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_12]
    // [Mục đích: Đảm bảo UpdateProgressEnrollmentAsync báo lỗi khi cả LessonId và ExamId đều có giá trị]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateProgressEnrollmentAsync_ShouldThrowArgumentException_WhenBothLessonIdAndExamIdProvided()
    {
        // Arrange — gửi cả 2 cùng lúc (vi phạm business rule: chỉ được 1 trong 2)
        var studentId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var service = CreateService();
        var request = new EnrollmentProgressUpdateDTO
        {
            LessonId = Guid.NewGuid().ToString(),
            ExamId = Guid.NewGuid().ToString()
        };

        // Act
        Func<Task> act = async () => await service.UpdateProgressEnrollmentAsync(studentId, courseId, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("LessonId or ExamId must have value, not both");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_13]
    // [Mục đích: Đảm bảo UpdateProgressEnrollmentAsync báo lỗi khi enrollment không ở trạng thái active]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateProgressEnrollmentAsync_ShouldThrowException_WhenEnrollmentIsNotActive()
    {
        // Arrange — enrollment đang ở trạng thái "Completed", không được cập nhật thêm
        var studentId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var lessonId = Guid.NewGuid().ToString();

        _courseRepositoryMock.Setup(r => r.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _enrollmentRepositoryMock
            .Setup(r => r.GetEnrollmentByStudentAndCourseIdAsync(studentId, courseId))
            .ReturnsAsync(new Enrollment_course
            {
                StudentId = studentId,
                CourseId = courseId,
                Status = "Completed" // không phải "active"
            });

        var service = CreateService();
        var request = new EnrollmentProgressUpdateDTO { LessonId = lessonId };

        // Act
        Func<Task> act = async () => await service.UpdateProgressEnrollmentAsync(studentId, courseId, request);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Only active enrollment can be updated!");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_14]
    // [Mục đích: Đảm bảo UpdateProgressEnrollmentAsync báo lỗi khi lesson không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateProgressEnrollmentAsync_ShouldThrowKeyNotFoundException_WhenLessonDoesNotExist()
    {
        // Arrange
        var studentId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var lessonId = Guid.NewGuid().ToString();

        _courseRepositoryMock.Setup(r => r.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _enrollmentRepositoryMock
            .Setup(r => r.GetEnrollmentByStudentAndCourseIdAsync(studentId, courseId))
            .ReturnsAsync(new Enrollment_course { StudentId = studentId, CourseId = courseId, Status = "active" });
        _lessonRepositoryMock.Setup(r => r.LessonExistsAsync(lessonId)).ReturnsAsync(false);

        var service = CreateService();
        var request = new EnrollmentProgressUpdateDTO { LessonId = lessonId };

        // Act
        Func<Task> act = async () => await service.UpdateProgressEnrollmentAsync(studentId, courseId, request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Lesson with id: {lessonId} not found");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_15]
    // [Mục đích: Đảm bảo UpdateProgressEnrollmentAsync cập nhật qua ExamId và đánh dấu Completed khi >= 95%]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateProgressEnrollmentAsync_ShouldMarkCompleted_WhenProgressReaches95ViaExam()
    {
        // Arrange — dùng ExamId thay vì LessonId; 100% lesson + 100% exam → progress > 95 → Completed
        var studentId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var examId = Guid.NewGuid().ToString();
        Enrollment_course? updatedEnrollment = null;

        var enrollment = new Enrollment_course
        {
            StudentId = studentId,
            CourseId = courseId,
            Status = "active",
            Progress = 0
        };

        _courseRepositoryMock.Setup(r => r.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _enrollmentRepositoryMock
            .Setup(r => r.GetEnrollmentByStudentAndCourseIdAsync(studentId, courseId))
            .ReturnsAsync(enrollment);
        _examRepositoryMock.Setup(r => r.GetExamStatusAsync(examId)).ReturnsAsync((true, true));
        // 1 lesson hoàn thành / 1 tổng + 1 exam đậu / 1 tổng → progress = (1.0*0.7 + 1.0*0.3) * 100 = 100%
        _courseContentRepositoryMock.Setup(r => r.TotalLessons(courseId)).ReturnsAsync(1);
        _lessonProgressRepositoryMock.Setup(r => r.CountCompletedLessonsAsync(courseId, studentId)).ReturnsAsync(1);
        _examRepositoryMock.Setup(r => r.TotalExamsInCourseAsync(courseId)).ReturnsAsync(1);
        _submissionExamRepositoryMock.Setup(r => r.CountPassExamsAsync(courseId, studentId, 70)).ReturnsAsync(1);
        _enrollmentRepositoryMock
            .Setup(r => r.UpdateProgressEnrollmentAsync(It.IsAny<Enrollment_course>()))
            .Callback<Enrollment_course>(e => updatedEnrollment = e)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var request = new EnrollmentProgressUpdateDTO { ExamId = examId };

        // Act
        await service.UpdateProgressEnrollmentAsync(studentId, courseId, request);

        // Assert — tiến độ đạt 100% → trạng thái chuyển sang Completed
        updatedEnrollment.Should().NotBeNull();
        updatedEnrollment!.Progress.Should().BeGreaterThanOrEqualTo(95m);
        updatedEnrollment.Status.Should().Be("Completed");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_16]
    // [Mục đích: Đảm bảo UpdateProgressEnrollmentAsync báo lỗi khi exam không tồn tại hoặc chưa mở]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task UpdateProgressEnrollmentAsync_ShouldThrowKeyNotFoundException_WhenExamNotFoundOrClosed()
    {
        // Arrange — exam không tồn tại (exists = false)
        var studentId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var examId = Guid.NewGuid().ToString();

        _courseRepositoryMock.Setup(r => r.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _enrollmentRepositoryMock
            .Setup(r => r.GetEnrollmentByStudentAndCourseIdAsync(studentId, courseId))
            .ReturnsAsync(new Enrollment_course { StudentId = studentId, CourseId = courseId, Status = "active" });
        _examRepositoryMock.Setup(r => r.GetExamStatusAsync(examId)).ReturnsAsync((false, false));

        var service = CreateService();
        var request = new EnrollmentProgressUpdateDTO { ExamId = examId };

        // Act
        Func<Task> act = async () => await service.UpdateProgressEnrollmentAsync(studentId, courseId, request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Exam with id: {examId} not found");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_17]
    // [Mục đích: Đảm bảo RequestCancelEnrollmentAsync báo lỗi khi user không sở hữu enrollment]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task RequestCancelEnrollmentAsync_ShouldThrowUnauthorized_WhenUserDoesNotOwnEnrollment()
    {
        // Arrange — userId khác với Student.User.Id trong enrollment
        var userId = Guid.NewGuid().ToString();
        var otherUserId = Guid.NewGuid().ToString(); // user thực sự sở hữu enrollment
        var courseId = Guid.NewGuid().ToString();
        var enrollmentId = Guid.NewGuid().ToString();

        var enrollment = new Enrollment_course
        {
            Id = enrollmentId,
            StudentId = "s1",
            CourseId = courseId,
            Status = "active",
            Progress = 0,
            EnrolledAt = DateTime.UtcNow.AddHours(-1),
            Student = new Student { StudentId = "s1", User = new User { Id = otherUserId } }
        };

        _courseRepositoryMock.Setup(r => r.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(r => r.GetEnrrollmentByIdAsync(enrollmentId)).ReturnsAsync(enrollment);
        _userRepositoryMock.Setup(r => r.IsUserExistAsync(userId)).ReturnsAsync(true);

        var service = CreateService();
        var request = new RequestCancelEnrollmentDTO { ReasonRequest = "Test" };

        // Act
        Func<Task> act = async () => await service.RequestCancelEnrollmentAsync(userId, courseId, enrollmentId, request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not authorized to update this enrollment");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_18]
    // [Mục đích: Đảm bảo RequestCancelEnrollmentAsync báo lỗi khi enrollment không thuộc courseId]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task RequestCancelEnrollmentAsync_ShouldThrowKeyNotFoundException_WhenCourseIdMismatch()
    {
        // Arrange — enrollment.CourseId khác với courseId truyền vào
        var userId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var differentCourseId = Guid.NewGuid().ToString(); // courseId thật của enrollment
        var enrollmentId = Guid.NewGuid().ToString();

        var enrollment = new Enrollment_course
        {
            Id = enrollmentId,
            StudentId = "s1",
            CourseId = differentCourseId, // khác với courseId request
            Status = "active",
            Progress = 0,
            EnrolledAt = DateTime.UtcNow.AddHours(-1),
            Student = new Student { StudentId = "s1", User = new User { Id = userId } }
        };

        _courseRepositoryMock.Setup(r => r.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(r => r.GetEnrrollmentByIdAsync(enrollmentId)).ReturnsAsync(enrollment);
        _userRepositoryMock.Setup(r => r.IsUserExistAsync(userId)).ReturnsAsync(true);

        var service = CreateService();
        var request = new RequestCancelEnrollmentDTO { ReasonRequest = "Test" };

        // Act
        Func<Task> act = async () => await service.RequestCancelEnrollmentAsync(userId, courseId, enrollmentId, request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Course Id is not match with enrollment.");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_19]
    // [Mục đích: Đảm bảo RequestCancelEnrollmentAsync báo lỗi khi enrollment không ở trạng thái active]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task RequestCancelEnrollmentAsync_ShouldThrowException_WhenEnrollmentIsNotActive()
    {
        // Arrange — enrollment đã ở trạng thái "Completed"
        var userId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var enrollmentId = Guid.NewGuid().ToString();

        var enrollment = new Enrollment_course
        {
            Id = enrollmentId,
            StudentId = "s2",
            CourseId = courseId,
            Status = "Completed", // không phải active
            Progress = 100,
            EnrolledAt = DateTime.UtcNow.AddHours(-1),
            Student = new Student { StudentId = "s2", User = new User { Id = userId } }
        };

        _courseRepositoryMock.Setup(r => r.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(r => r.GetEnrrollmentByIdAsync(enrollmentId)).ReturnsAsync(enrollment);
        _userRepositoryMock.Setup(r => r.IsUserExistAsync(userId)).ReturnsAsync(true);

        var service = CreateService();
        var request = new RequestCancelEnrollmentDTO { ReasonRequest = "Test" };

        // Act
        Func<Task> act = async () => await service.RequestCancelEnrollmentAsync(userId, courseId, enrollmentId, request);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Can't cancel enrollment without active status");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_20]
    // [Mục đích: Đảm bảo RequestCancelEnrollmentAsync hoàn tiền 100% khi progress = 0]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task RequestCancelEnrollmentAsync_ShouldRefundFullPrice_WhenProgressIsZero()
    {
        // Arrange — student chưa học bài nào (progress = 0) → hoàn tiền 100%
        var userId = Guid.NewGuid().ToString();
        var courseId = Guid.NewGuid().ToString();
        var enrollmentId = Guid.NewGuid().ToString();
        RefundRequestCourse? savedRefund = null;

        var enrollment = new Enrollment_course
        {
            Id = enrollmentId,
            StudentId = "s3",
            CourseId = courseId,
            Status = "active",
            Progress = 0, // chưa học gì
            EnrolledAt = DateTime.UtcNow.AddHours(-1),
            Student = new Student { StudentId = "s3", User = new User { Id = userId } }
        };

        _courseRepositoryMock.Setup(r => r.CourseExistsAsync(courseId)).ReturnsAsync(true);
        _enrollmentRepositoryMock.Setup(r => r.GetEnrrollmentByIdAsync(enrollmentId)).ReturnsAsync(enrollment);
        _userRepositoryMock.Setup(r => r.IsUserExistAsync(userId)).ReturnsAsync(true);
        _courseRepositoryMock.Setup(r => r.GetCourseByIdAsync(courseId))
            .ReturnsAsync(new Course { Id = courseId, Price = 500000 });
        _enrollmentRepositoryMock.Setup(r => r.UpdateProgressEnrollmentAsync(It.IsAny<Enrollment_course>()))
            .Returns(Task.CompletedTask);
        _requestRefundCourseRepositoryMock
            .Setup(r => r.CreateRequestRefundCourseAsync(It.IsAny<RefundRequestCourse>()))
            .Callback<RefundRequestCourse>(req => savedRefund = req)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var request = new RequestCancelEnrollmentDTO { ReasonRequest = "Chưa học bài nào" };

        // Act
        await service.RequestCancelEnrollmentAsync(userId, courseId, enrollmentId, request);

        // Assert — hoàn tiền = 100% giá gốc vì progress = 0
        savedRefund.Should().NotBeNull();
        savedRefund!.RefundAmount.Should().Be(500000m);
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_ECS_21]
    // [Mục đích: Đảm bảo GetRecentEnrollmentsOfTeacherAsync trả về danh sách enrollment gần nhất của teacher]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetRecentEnrollmentsOfTeacherAsync_ShouldReturnRecentEnrollments_WhenTeacherExists()
    {
        // Arrange
        var teacherId = Guid.NewGuid().ToString();
        _studentRepositoryMock.Setup(r => r.IsStudentExistAsync(teacherId)).ReturnsAsync(true);

        var enrollments = new List<Enrollment_course>
        {
            new()
            {
                StudentId = "s1",
                CourseId = "c1",
                Status = "active",
                Progress = 30m,
                CertificateUrl = "No Certificate",
                Student = new Student { User = new User { FullName = "Học viên X" } },
                Course = new Course { Title = "Khóa học Y" }
            }
        };

        _enrollmentRepositoryMock
            .Setup(r => r.GetRecentEnrollmentsOfTeacherAsync(teacherId, 5))
            .ReturnsAsync(enrollments);

        var service = CreateService();

        // Act
        var result = await service.GetRecentEnrollmentsOfTeacherAsync(teacherId, 5);

        // Assert
        result.Should().NotBeNull();
        result.RecentEnrollments.Should().ContainSingle();
        result.RecentEnrollments.First().StudentName.Should().Be("Học viên X");
        result.RecentEnrollments.First().CourseName.Should().Be("Khóa học Y");
    }

    private EnrollmentCourseService CreateService()
    {
        return new EnrollmentCourseService(
            _enrollmentRepositoryMock.Object,
            _courseRepositoryMock.Object,
            _studentRepositoryMock.Object,
            _courseContentRepositoryMock.Object,
            _lessonRepositoryMock.Object,
            _examRepositoryMock.Object,
            _lessonProgressRepositoryMock.Object,
            _submissionExamRepositoryMock.Object,
            _requestRefundCourseRepositoryMock.Object,
            _adminRepositoryMock.Object,
            _userRepositoryMock.Object);
    }
}

