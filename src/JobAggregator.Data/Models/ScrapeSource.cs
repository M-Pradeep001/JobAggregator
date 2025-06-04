using System.ComponentModel.DataAnnotations;

namespace JobAggregator.Data.Models;

public class ScrapeSource
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // e.g., "LinkedIn", "Naukri"

    [Url]
    public string? BaseUrl { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int ScrapeIntervalMinutes { get; set; } = 60; // Default to 1 hour

    public DateTimeOffset? LastScrapeStartTime { get; set; }

    public DateTimeOffset? LastScrapeEndTime { get; set; }

    [MaxLength(50)]
    public string? LastScrapeStatus { get; set; } // e.g., "Success", "Failed", "Running"

    // Navigation property
    public virtual ICollection<ScrapeLog> ScrapeLogs { get; set; } = new List<ScrapeLog>();
}

