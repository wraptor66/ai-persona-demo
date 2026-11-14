using System.ComponentModel.DataAnnotations;

namespace Northline.Api.Models;

/// <summary>A Northline Retail customer.</summary>
public class Customer
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    public List<Order> Orders { get; set; } = new();
}
