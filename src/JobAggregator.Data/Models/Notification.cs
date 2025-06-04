using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobAggregator.Data.Models;

public class Notification
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public Guid JobId { get; set; }

    [Required]
    [MaxLength(50)]
    public string NotificationType { get; set; } = string.Empty; // e.g., "Email", "WebPush"

    public DateTimeOffset SentDate { get; set; }

    public bool IsRead { get; set; } = false;

    // Navigation properties
    [ForeignKey("UserId")]
    public virtual ApplicationUser? User { get; set; }

    [ForeignKey("JobId")]
    public virtual Job? Job { get; set; }
}

