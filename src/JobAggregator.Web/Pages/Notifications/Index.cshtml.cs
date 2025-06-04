using JobAggregator.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JobAggregator.Web.Pages.Notifications;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ApplicationDbContext dbContext,
        ILogger<IndexModel> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public IList<Notification> Notifications { get; set; } = new List<Notification>();
    public int UnreadCount { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        try
        {
            // Get user's notifications with job details
            Notifications = await _dbContext.Notifications
                .Where(n => n.UserId == userId)
                .Include(n => n.Job)
                .OrderByDescending(n => n.SentDate)
                .ToListAsync();

            UnreadCount = Notifications.Count(n => !n.IsRead);

            // Mark all as read
            var unreadNotifications = await _dbContext.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.ReadDate = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications");
            ModelState.AddModelError(string.Empty, "An error occurred while retrieving notifications. Please try again later.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid notificationId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification != null)
        {
            _dbContext.Notifications.Remove(notification);
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClearAllAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        var notifications = await _dbContext.Notifications
            .Where(n => n.UserId == userId)
            .ToListAsync();

        _dbContext.Notifications.RemoveRange(notifications);
        await _dbContext.SaveChangesAsync();

        return RedirectToPage();
    }
}
