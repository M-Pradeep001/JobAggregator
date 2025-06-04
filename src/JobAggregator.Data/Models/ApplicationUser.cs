using Microsoft.AspNetCore.Identity;

namespace JobAggregator.Data.Models;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    // Navigation properties
    public virtual ICollection<Keyword> Keywords { get; set; } = new List<Keyword>();
    public virtual ICollection<SavedJob> SavedJobs { get; set; } = new List<SavedJob>();
    public virtual Resume? Resume { get; set; } // Assuming one-to-one for simplicity
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

