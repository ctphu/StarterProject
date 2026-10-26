using DevOpsLab.Domain.Common;
using DevOpsLab.Domain.Entities;
using Xunit;

namespace DevOpsLab.UnitTests.Domain;

/// <summary>
/// Every test follows the 3A structure: Arrange - Act - Assert.
/// Session 3 extends this class; do not delete the failure-case tests.
/// </summary>
public class ProductTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_NormalisesSkuAndActivatesProduct()
    {
        // Arrange
        const string rawSku = " lap-14-pro ";

        // Act
        var product = Product.Create(CategoryId, rawSku, " 14-inch Pro Laptop ", 1899.004m, 25);

        // Assert
        Assert.Equal("LAP-14-PRO", product.Sku);
        Assert.Equal("14-inch Pro Laptop", product.Name);
        Assert.Equal(1899.00m, product.Price);
        Assert.Equal(25, product.StockQuantity);
        Assert.True(product.IsActive);
        Assert.NotEqual(Guid.Empty, product.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AB")]
    [InlineData("THIS-SKU-IS-DEFINITELY-FAR-TOO-LONG-FOR-THE-RULE")]
    [InlineData("BAD SKU")]
    [InlineData("BAD_SKU")]
    public void Create_WithInvalidSku_ThrowsBusinessRuleViolation(string sku)
    {
        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => Product.Create(CategoryId, sku, "Valid name", 10m, 1));

        Assert.StartsWith("product.sku", exception.Rule);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Create_WithNonPositivePrice_ThrowsBusinessRuleViolation(decimal price)
    {
        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => Product.Create(CategoryId, "SKU-001", "Valid name", price, 1));

        Assert.Equal("product.price.positive", exception.Rule);
    }

    [Fact]
    public void Create_WithEmptyCategory_ThrowsBusinessRuleViolation()
    {
        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => Product.Create(Guid.Empty, "SKU-001", "Valid name", 10m, 1));

        Assert.Equal("product.category.required", exception.Rule);
    }

    [Fact]
    public void Create_WithNegativeStock_ThrowsBusinessRuleViolation()
    {
        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => Product.Create(CategoryId, "SKU-001", "Valid name", 10m, -1));

        Assert.Equal("product.stock.negative", exception.Rule);
    }

    [Fact]
    public void ChangePrice_WithinAllowedIncrease_UpdatesPrice()
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", 100m, 10);

        product.ChangePrice(150m);

        Assert.Equal(150m, product.Price);
    }

    [Fact]
    public void ChangePrice_AboveMaximumIncrease_ThrowsBusinessRuleViolation()
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", 100m, 10);

        var exception = Assert.Throws<BusinessRuleViolationException>(() => product.ChangePrice(150.01m));

        Assert.Equal("product.price.increaseTooLarge", exception.Rule);
        Assert.Equal(100m, product.Price);
    }

    [Fact]
    public void Reserve_WithSufficientStock_DecreasesStock()
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", 100m, 10);

        product.Reserve(4);

        Assert.Equal(6, product.StockQuantity);
    }

    [Fact]
    public void Reserve_MoreThanAvailable_ThrowsAndLeavesStockUnchanged()
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", 100m, 10);

        var exception = Assert.Throws<BusinessRuleViolationException>(() => product.Reserve(11));

        Assert.Equal("product.reserve.insufficientStock", exception.Rule);
        Assert.Equal(10, product.StockQuantity);
    }

    [Fact]
    public void Reserve_OnInactiveProduct_ThrowsBusinessRuleViolation()
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", 100m, 10);
        product.Deactivate();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => product.Reserve(1));

        Assert.Equal("product.inactive", exception.Rule);
        Assert.Equal(10, product.StockQuantity);
    }

    [Fact]
    public void Restock_WithPositiveQuantity_IncreasesStock()
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", 100m, 10);

        product.Restock(5);

        Assert.Equal(15, product.StockQuantity);
    }

    // ---- Kills M2 and M7 (the two survivors) ----

    [Fact]
    public void Reserve_ExactlyAllRemainingStock_Succeeds()
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", 100m, 10);

        product.Reserve(10);

        Assert.Equal(0, product.StockQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reserve_NonPositiveQuantity_ThrowsAndLeavesStockUnchanged(int quantity)
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", 100m, 10);

        var exception = Assert.Throws<BusinessRuleViolationException>(() => product.Reserve(quantity));

        Assert.Equal("product.reserve.positive", exception.Rule);
        Assert.Equal(10, product.StockQuantity);
    }

    // ---- SKU boundaries (length 3..32, measured after trimming) ----

    [Theory]
    [InlineData(3)]
    [InlineData(32)]
    public void Create_SkuAtLengthLimits_Succeeds(int length)
    {
        var product = Product.Create(CategoryId, new string('A', length), "Valid name", 10m, 1);

        Assert.Equal(length, product.Sku.Length);
    }

    [Fact]
    public void Create_SkuOneCharacterTooLong_ThrowsLengthRule()
    {
        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => Product.Create(CategoryId, new string('A', 33), "Valid name", 10m, 1));

        Assert.Equal("product.sku.length", exception.Rule);
    }

    [Fact]
    public void Create_SkuTooShortOnlyAfterTrimming_ThrowsLengthRule()
    {
        // "  AB  " is 6 characters raw but 2 after trimming
        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => Product.Create(CategoryId, "  AB  ", "Valid name", 10m, 1));

        Assert.Equal("product.sku.length", exception.Rule);
    }

    [Fact]
    public void Create_SkuWithVietnameseLetter_ThrowsFormatRule()
    {
        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => Product.Create(CategoryId, "ÁO-001", "Valid name", 10m, 1));

        Assert.Equal("product.sku.format", exception.Rule);
    }

    // ---- Name rules ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ThrowsNameRequired(string name)
    {
        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => Product.Create(CategoryId, "SKU-001", name, 10m, 1));

        Assert.Equal("product.name.required", exception.Rule);
    }

    [Fact]
    public void Create_NameOfExactly200Characters_Succeeds()
    {
        var product = Product.Create(CategoryId, "SKU-001", new string('N', 200), 10m, 1);

        Assert.Equal(200, product.Name.Length);
    }

    [Fact]
    public void Create_NameOf201Characters_ThrowsTooLong()
    {
        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => Product.Create(CategoryId, "SKU-001", new string('N', 201), 10m, 1));

        Assert.Equal("product.name.tooLong", exception.Rule);
    }

    // ---- Stock and price boundaries on Create ----

    [Fact]
    public void Create_WithZeroStock_Succeeds()
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", 10m, 0);

        Assert.Equal(0, product.StockQuantity);
    }

    [Fact]
    public void Create_WithSmallestPositivePrice_Succeeds()
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", 0.01m, 1);

        Assert.Equal(0.01m, product.Price);
    }

    // ---- ChangePrice ----

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ChangePrice_NonPositive_ThrowsAndKeepsOldPrice(decimal newPrice)
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", 100m, 10);

        var exception = Assert.Throws<BusinessRuleViolationException>(() => product.ChangePrice(newPrice));

        Assert.Equal("product.price.positive", exception.Rule);
        Assert.Equal(100m, product.Price);
    }

    [Fact]
    public void ChangePrice_LargeDecrease_IsAllowed()
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", 100m, 10);

        product.ChangePrice(1m);

        Assert.Equal(1m, product.Price);
    }

    // ---- Restock / Activate ----

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Restock_NonPositiveQuantity_ThrowsAndLeavesStockUnchanged(int quantity)
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", 100m, 10);

        var exception = Assert.Throws<BusinessRuleViolationException>(() => product.Restock(quantity));

        Assert.Equal("product.restock.positive", exception.Rule);
        Assert.Equal(10, product.StockQuantity);
    }

    [Fact]
    public void Activate_AfterDeactivate_MakesProductActiveAgain()
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", 100m, 10);
        product.Deactivate();

        product.Activate();

        Assert.True(product.IsActive);
    }
    [Theory]
    [InlineData(10.004, 10.00)]
    [InlineData(10.005, 10.00)]   // midpoint, preceding digit 0 is even: stays
    [InlineData(10.006, 10.01)]
    [InlineData(10.015, 10.02)]   // midpoint, preceding digit 1 is odd: rounds up
    [InlineData(10.025, 10.02)]   // midpoint, preceding digit 2 is even: stays
    public void Create_RoundsPriceToTwoDecimalsUsingBankersRounding(double input, double expected)
    {
        var product = Product.Create(CategoryId, "SKU-001", "Valid name", (decimal)input, 1);

        Assert.Equal((decimal)expected, product.Price);
    }
    
}
