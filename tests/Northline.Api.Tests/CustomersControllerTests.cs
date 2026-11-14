using Microsoft.AspNetCore.Mvc;
using Northline.Api.Controllers;
using Northline.Api.Models;

namespace Northline.Api.Tests;

[TestClass]
public class CustomersControllerTests
{
    [TestMethod]
    public async Task GetById_ExistingCustomer_ReturnsOk()
    {
        using var db = TestDb.Create();
        var controller = new CustomersController(db);

        var result = await controller.GetById(1, CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        var customer = ok.Value as Customer;
        Assert.IsNotNull(customer);
        Assert.AreEqual("ava.martinez@example.com", customer.Email);
    }

    [TestMethod]
    public async Task Create_DuplicateEmail_ReturnsConflict()
    {
        using var db = TestDb.Create();
        var controller = new CustomersController(db);
        var duplicate = new Customer { FirstName = "Dup", LastName = "User", Email = "ava.martinez@example.com" };

        var result = await controller.Create(duplicate, CancellationToken.None);

        Assert.IsInstanceOfType(result, typeof(ConflictObjectResult));
    }
}
