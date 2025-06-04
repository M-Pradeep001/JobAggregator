using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobAggregator.Data.Models;

public class Resume
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    public string StoredFileName { get; set; } = string.Empty; // Unique name for storage

    [Required]
    public string FilePath { get; set; } = string.Empty; // Path in storage (e.g., blob path)

    public long FileSize { get; set; }

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty; // e.g., "application/pdf"

    public DateTimeOffset UploadDate { get; set; }

    public string? ParsedKeywords { get; set; } // Optional: Store extracted keywords as text/JSON

    // Navigation property (assuming one-to-one)
    [ForeignKey("UserId")]
    public virtual ApplicationUser? User { get; set; }
}

