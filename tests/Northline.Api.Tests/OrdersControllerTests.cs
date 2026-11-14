using Microsoft.AspNetCore.Mvc;
using Northline.Api.Controllers;
using Northline.Api.Models;

namespace Northline.Api.Tests;

[TestClass]
public class OrdersControllerTests
{
    [TestMethod]
    public async Task Create_ValidOrder_ReturnsCreatedWithTotal()
    {
        using var db = TestDb.Create();
        var controller = new OrdersController(db);
        var request = new CreateOrderRequest(2, new List<CreateOrderLine> { new(1, 2), new(5, 1) });

        var result = await controller.Create(request, CancellationToken.None);

        var created = result as CreatedAtActionResult;
        Assert.IsNotNull(created);
        var order = created.Value as Order;
        Assert.IsNotNull(order);
        Assert.AreEqual(64.00m, order.Total);
        Assert.AreEqual(2, order.Items.Count);
    }

    [TestMethod]
    public async Task Create_UnknownCustomer_ReturnsBadRequest()
    {
        using var db = TestDb.Create();
        var controller = new OrdersController(db);
        var request = new CreateOrderRequest(999, new List<CreateOrderLine> { new(1, 1) });

        var result = await controller.Create(request, CancellationToken.None);

        Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
    }
}
