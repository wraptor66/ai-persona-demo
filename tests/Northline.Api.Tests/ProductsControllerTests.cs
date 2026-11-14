using Microsoft.AspNetCore.Mvc;
using Northline.Api.Controllers;
using Northline.Api.Models;

namespace Northline.Api.Tests;

[TestClass]
public class ProductsControllerTests
{
    [TestMethod]
    public async Task GetAll_ReturnsOnlyActiveProducts()
    {
        using var db = TestDb.Create();
        var controller = new ProductsController(db);

        var result = await controller.GetAll(null, CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        var products = ok.Value as List<Product>;
        Assert.IsNotNull(products);
        Assert.AreEqual(7, products.Count);
        Assert.IsTrue(products.All(p => p.IsActive));
    }

    [TestMethod]
    public async Task Create_NonPositivePrice_ReturnsBadRequest()
    {
        using var db = TestDb.Create();
        var controller = new ProductsController(db);
        var product = new Product { Sku = "NL-BAD-999", Name = "Free Thing", Price = 0m, StockQuantity = 1 };

        var result = await controller.Create(product, CancellationToken.None);

        Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
    }
}
