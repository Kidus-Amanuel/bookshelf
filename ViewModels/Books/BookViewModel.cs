using System.ComponentModel.DataAnnotations;

namespace BookShelf.ViewModels.Books;

public class BookViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(4000, ErrorMessage = "Description cannot exceed 4000 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "ISBN is required.")]
    [StringLength(32)]
    public string ISBN { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cover image URL is required.")]
    [Url(ErrorMessage = "Cover image must be a valid URL.")]
    [StringLength(500)]
    public string CoverImageUrl { get; set; } = string.Empty;

    [Range(0, 9999.99, ErrorMessage = "Price must be between 0 and 9,999.99.")]
    public decimal? Price { get; set; }

    public bool IsForSale { get; set; }
    public bool IsForLoan { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative.")]
    public int StockForSale { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative.")]
    public int StockForLoan { get; set; }

    public string Status { get; set; } = "Pending";
}