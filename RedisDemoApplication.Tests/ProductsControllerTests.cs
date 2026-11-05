using Microsoft.AspNetCore.Mvc;
using RedisDemoApplication.Contracts;
using RedisDemoApplication.Controllers;
using RedisDemoApplication.Entities;

namespace RedisDemoApplication.Tests;

/// <summary>Verifies the HTTP status codes the controller maps service results to.</summary>
public sealed class ProductsControllerTests : IDisposable
{
    private readonly ProductTestContext _ctx = new();
    private readonly ProductsController _controller;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public ProductsControllerTests() => _controller = new ProductsController(_ctx.Service);

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task GetById_Existing_Returns200WithProduct()
    {
        var result = await _controller.GetById(1, _ct);

        Assert.Equal(1, result.Value!.Id);
    }

    [Fact]
    public async Task GetById_Missing_Returns404()
    {
        var result = await _controller.GetById(9999, _ct);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_Returns201WithLocation()
    {
        var result = await _controller.Create(new ProductRequest { Name = "New", Description = "", Price = 5m }, _ct);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(ProductsController.GetById), created.ActionName);
        Assert.IsType<Product>(created.Value);
    }

    [Fact]
    public async Task Update_Missing_Returns404()
    {
        var result = await _controller.Update(9999, new ProductRequest { Name = "x", Price = 1m }, _ct);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Delete_Existing_Returns204_AndMissing_Returns404()
    {
        Assert.IsType<NoContentResult>(await _controller.Delete(1, _ct));
        Assert.IsType<NotFoundResult>(await _controller.Delete(1, _ct));
    }
}
