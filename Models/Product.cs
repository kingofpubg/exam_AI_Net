using System.ComponentModel.DataAnnotations;

public class Product
{
    [Key]
    public int ProductId { get; set; }
    [Required]
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Features { get; set; } = string.Empty;
    public string? Description { get; set; }
}
