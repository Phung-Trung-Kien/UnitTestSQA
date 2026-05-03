using FluentAssertions;
using Moq;

public class CategoryServiceTests
{
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CAS_01]
    // [Mục đích: Đảm bảo CreateCategoryAsync tạo Category đúng dữ liệu đầu vào]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task CreateCategoryAsync_ShouldCreateCategory_WhenInputIsValid()
    {
        // Arrange
        Category? savedCategory = null;
        var service = new CategoryService(_categoryRepositoryMock.Object);
        var request = new CategoryCreateDTO
        {
            Name = "Lập trình",
            Description = "Các khóa học lập trình"
        };

        _categoryRepositoryMock
            .Setup(repo => repo.CreateCategoryAsync(It.IsAny<Category>()))
            .Callback<Category>(category => savedCategory = category)
            .Returns(Task.CompletedTask);

        // Act
        await service.CreateCategoryAsync(request);

        // Assert
        savedCategory.Should().NotBeNull();
        savedCategory!.Id.Should().NotBeNullOrWhiteSpace();
        savedCategory.Name.Should().Be("Lập trình");
        savedCategory.Description.Should().Be("Các khóa học lập trình");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CAS_02]
    // [Mục đích: Đảm bảo GetCategoryByIdAsync báo lỗi khi Category không tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetCategoryByIdAsync_ShouldThrowKeyNotFoundException_WhenCategoryDoesNotExist()
    {
        // Arrange
        var service = new CategoryService(_categoryRepositoryMock.Object);
        _categoryRepositoryMock
            .Setup(repo => repo.GetCategoryByIdAsync("missing-category"))
            .ReturnsAsync((Category?)null);

        // Act
        Func<Task> act = async () => await service.GetCategoryByIdAsync("missing-category");

        // Assert
        await act.Should()
            .ThrowAsync<KeyNotFoundException>()
            .WithMessage("Category with id missing-category not found.");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CAS_03]
    // [Mục đích: Đảm bảo GetAllCategoriesAsync trả về danh sách tất cả Category từ repository]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetAllCategoriesAsync_ShouldReturnAllCategories_WhenRepositoryHasData()
    {
        // Arrange
        var service = new CategoryService(_categoryRepositoryMock.Object);
        var categories = new List<Category>
        {
            new() { Id = "cat-1", Name = "Lập trình", Description = "Backend, Frontend" },
            new() { Id = "cat-2", Name = "Thiết kế", Description = "UI/UX, Đồ họa" },
            new() { Id = "cat-3", Name = "Marketing", Description = "SEO, Ads" }
        };

        _categoryRepositoryMock
            .Setup(repo => repo.GetAllCategoriesAsync())
            .ReturnsAsync(categories);

        // Act
        var result = (await service.GetAllCategoriesAsync()).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Lập trình");
        result[1].Name.Should().Be("Thiết kế");
        result[2].Id.Should().Be("cat-3");
    }

    // ------------------------------------------------------------------------------------------------
    // [ID: SERV_CAS_04]
    // [Mục đích: Đảm bảo GetCategoryByIdAsync trả về Category đúng khi tồn tại]
    // ------------------------------------------------------------------------------------------------
    [Fact]
    public async Task GetCategoryByIdAsync_ShouldReturnCategory_WhenCategoryExists()
    {
        // Arrange
        var service = new CategoryService(_categoryRepositoryMock.Object);
        var existingCategory = new Category
        {
            Id = "cat-exist",
            Name = "Lập trình C#",
            Description = "Ngôn ngữ lập trình C#"
        };

        _categoryRepositoryMock
            .Setup(repo => repo.GetCategoryByIdAsync("cat-exist"))
            .ReturnsAsync(existingCategory);

        // Act
        var result = await service.GetCategoryByIdAsync("cat-exist");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("cat-exist");
        result.Name.Should().Be("Lập trình C#");
    }
}
