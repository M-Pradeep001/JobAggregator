using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobAggregator.Data.Models;

public class ScrapeLog
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid ScrapeSourceId { get; set; }

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset? EndTime { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty; // e.g., "Success", "Failed"

    public string? Message { get; set; } // Error message or summary

    public int JobsFound { get; set; } = 0;

    public int NewJobsAdded { get; set; } = 0;

    // Navigation property
    [ForeignKey("ScrapeSourceId")]
    public virtual ScrapeSource? ScrapeSource { get; set; }
}

