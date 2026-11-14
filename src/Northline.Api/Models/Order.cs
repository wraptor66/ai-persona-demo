namespace Northline.Api.Models;

/// <summary>A customer order.</summary>
public class Order
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public DateTime OrderDateUtc { get; set; }

    public decimal Total { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}

/// <summary>A line on an order.</summary>
public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}
