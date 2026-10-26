using DevOpsLab.Application.Common;
using DevOpsLab.Application.Products;
using DevOpsLab.Domain.Common;
using DevOpsLab.Domain.Entities;
using DevOpsLab.UnitTests.Fakes;
using Xunit;

namespace DevOpsLab.UnitTests.Application;

public class ProductServiceTests
{
    private readonly FakeProductRepository _products = new();
    private readonly FakeCategoryRepository _categories = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Guid _categoryId = Guid.NewGuid();

    private ProductService CreateSut()
    {
        _categories.Seed(_categoryId);
        return new ProductService(_products, _categories, _unitOfWork);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_PersistsProduct()
    {
        var sut = CreateSut();
        var request = new CreateProductRequest(_categoryId, "sku-001", "Test product", 99.99m, 5);

        var response = await sut.CreateAsync(request);

        Assert.Equal("SKU-001", response.Sku);
        Assert.Single(_products.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownCategory_ThrowsNotFound()
    {
        var sut = CreateSut();
        var request = new CreateProductRequest(Guid.NewGuid(), "SKU-002", "Test product", 10m, 1);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.CreateAsync(request));
        Assert.Empty(_products.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateSku_ThrowsBusinessRuleViolation()
    {
        var sut = CreateSut();
        _products.Seed(Product.Create(_categoryId, "SKU-003", "Existing", 10m, 1));

        var request = new CreateProductRequest(_categoryId, "sku-003", "Duplicate", 10m, 1);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(() => sut.CreateAsync(request));

        Assert.Equal("product.sku.duplicate", exception.Rule);
        Assert.Single(_products.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetAsync_WithUnknownId_ThrowsNotFound()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ListAsync_ClampsPageSizeToMaximum()
    {
        var sut = CreateSut();
        for (var i = 1; i <= 5; i++)
            _products.Seed(Product.Create(_categoryId, $"SKU-{i:000}", $"Product {i}", 10m, 1));

        var result = await sut.ListAsync(search: null, page: 1, pageSize: 5_000);

        Assert.Equal(ProductService.MaxPageSize, result.PageSize);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task ListAsync_WithSearchTerm_FiltersResults()
    {
        var sut = CreateSut();
        _products.Seed(
            Product.Create(_categoryId, "LAP-001", "Laptop", 10m, 1),
            Product.Create(_categoryId, "MON-001", "Monitor", 10m, 1));

        var result = await sut.ListAsync(search: "lap", page: 1, pageSize: 20);

        Assert.Single(result.Items);
        Assert.Equal("LAP-001", result.Items[0].Sku);
    }

    [Fact]
    public async Task ReserveStockAsync_BeyondAvailableStock_ThrowsAndDoesNotSave()
    {
        var sut = CreateSut();
        var product = Product.Create(_categoryId, "SKU-004", "Test", 10m, 2);
        _products.Seed(product);

        // await Assert.ThrowsAsync<BusinessRuleViolationException>(
        //     () => sut.ReserveStockAsync(product.Id, new ReserveStockRequest(3)));

        // Assert.Equal(2, product.StockQuantity);
        // Assert.Equal(0, _unitOfWork.SaveCount);
        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
          () => sut.ReserveStockAsync(product.Id, new ReserveStockRequest(3)));

        Assert.Equal("product.reserve.insufficientStock", exception.Rule);
        Assert.Equal(2, product.StockQuantity);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ChangePriceAsync_WithValidPrice_SavesChange()
    {
        var sut = CreateSut();
        var product = Product.Create(_categoryId, "SKU-005", "Test", 100m, 2);
        _products.Seed(product);

        var response = await sut.ChangePriceAsync(product.Id, new ChangePriceRequest(120m));

        Assert.Equal(120m, response.Price);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }
    // ---- CreateAsync ----

    [Fact]
    public async Task CreateAsync_WhenProductRuleBroken_ThrowsRuleAndDoesNotSave()
    {
        var sut = CreateSut();
        var request = new CreateProductRequest(_categoryId, "SKU-010", "Test product", 0m, 1);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(() => sut.CreateAsync(request));

        Assert.Equal("product.price.positive", exception.Rule);
        Assert.Empty(_products.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // ---- GetAsync ----

    [Fact]
    public async Task GetAsync_WithExistingId_ReturnsThatProduct()
    {
        var sut = CreateSut();
        var product = Product.Create(_categoryId, "SKU-011", "Test", 10m, 1);
        _products.Seed(product);

        var response = await sut.GetAsync(product.Id);

        Assert.Equal("SKU-011", response.Sku);
    }

    // ---- ChangePriceAsync ----

    [Fact]
    public async Task ChangePriceAsync_ExactlyFiftyPercentIncrease_SavesOnce()
    {
        var sut = CreateSut();
        var product = Product.Create(_categoryId, "SKU-012", "Test", 100m, 2);
        _products.Seed(product);

        await sut.ChangePriceAsync(product.Id, new ChangePriceRequest(150m));

        Assert.Equal(150m, product.Price);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ChangePriceAsync_OneCentOverMaxIncrease_ThrowsKeepsPriceAndDoesNotSave()
    {
        var sut = CreateSut();
        var product = Product.Create(_categoryId, "SKU-013", "Test", 100m, 2);
        _products.Seed(product);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => sut.ChangePriceAsync(product.Id, new ChangePriceRequest(150.01m)));

        Assert.Equal("product.price.increaseTooLarge", exception.Rule);
        Assert.Equal(100m, product.Price);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ChangePriceAsync_WithUnknownId_ThrowsNotFoundAndDoesNotSave()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.ChangePriceAsync(Guid.NewGuid(), new ChangePriceRequest(10m)));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // ---- ReserveStockAsync ----

    [Fact]
    public async Task ReserveStockAsync_ExactlyAllRemainingStock_SavesOnce()
    {
        var sut = CreateSut();
        var product = Product.Create(_categoryId, "SKU-014", "Test", 10m, 2);
        _products.Seed(product);

        await sut.ReserveStockAsync(product.Id, new ReserveStockRequest(2));

        Assert.Equal(0, product.StockQuantity);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ReserveStockAsync_ZeroUnits_ThrowsKeepsStockAndDoesNotSave()
    {
        var sut = CreateSut();
        var product = Product.Create(_categoryId, "SKU-015", "Test", 10m, 2);
        _products.Seed(product);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => sut.ReserveStockAsync(product.Id, new ReserveStockRequest(0)));

        Assert.Equal("product.reserve.positive", exception.Rule);
        Assert.Equal(2, product.StockQuantity);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ReserveStockAsync_WithUnknownId_ThrowsNotFoundAndDoesNotSave()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.ReserveStockAsync(Guid.NewGuid(), new ReserveStockRequest(1)));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // ---- ListAsync paging corrections ----

    [Theory]
    [InlineData(100, 100)]   // exactly the maximum: unchanged
    [InlineData(101, 100)]   // one over: clamped
    [InlineData(1, 1)]       // smallest valid: unchanged
    [InlineData(0, 20)]      // below 1: default
    [InlineData(-5, 20)]
    public async Task ListAsync_CorrectsPageSize(int requested, int expected)
    {
        var sut = CreateSut();

        var result = await sut.ListAsync(search: null, page: 1, pageSize: requested);

        Assert.Equal(expected, result.PageSize);
    }

    [Theory]
    [InlineData(1, 1)]       // smallest valid: unchanged
    [InlineData(2, 2)]
    [InlineData(0, 1)]       // below 1: corrected
    [InlineData(-3, 1)]
    public async Task ListAsync_CorrectsPage(int requested, int expected)
    {
        var sut = CreateSut();

        var result = await sut.ListAsync(search: null, page: requested, pageSize: 20);

        Assert.Equal(expected, result.Page);
    }
}
