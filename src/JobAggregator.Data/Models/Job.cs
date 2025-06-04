using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobAggregator.Data.Models;

public class Job
{
    [Key]
    public Guid Id { get; set; }

    public string? JobIdFromSource { get; set; } // Original ID from the source platform

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Location { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty; // Can store HTML or plain text

    [Required]
    [Url]
    public string JobUrl { get; set; } = string.Empty;

    public DateTimeOffset? PostingDate { get; set; }

    [Required]
    public DateTimeOffset ScrapedDate { get; set; }

    public DateTimeOffset LastSeenDate { get; set; }

    [Required]
    [MaxLength(100)]
    public string SourcePlatform { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? JobType { get; set; } // e.g., "Full-time", "Internship"

    public bool? IsRemote { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual ICollection<SavedJob> SavedByUsers { get; set; } = new List<SavedJob>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

