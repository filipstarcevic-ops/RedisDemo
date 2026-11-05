using System.ComponentModel.DataAnnotations;

namespace RedisDemoApplication.Contracts;

/// <summary>Request body for creating or updating a product.</summary>
public class ProductRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "1000000")]
    public decimal Price { get; set; }
}
