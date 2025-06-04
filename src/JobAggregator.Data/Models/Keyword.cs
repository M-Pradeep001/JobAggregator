using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobAggregator.Data.Models;

public class Keyword
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string KeywordText { get; set; } = string.Empty;

    public DateTimeOffset AddedDate { get; set; }

    // Navigation property
    [ForeignKey("UserId")]
    public virtual ApplicationUser? User { get; set; }
}

