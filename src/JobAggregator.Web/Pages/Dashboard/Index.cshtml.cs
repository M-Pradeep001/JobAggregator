using JobAggregator.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace JobAggregator.Web.Pages.Dashboard;

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

    public IList<SavedJob> SavedJobs { get; set; } = new List<SavedJob>();
    public IList<Keyword> UserKeywords { get; set; } = new List<Keyword>();
    public Resume? UserResume { get; set; }
    public int NewJobsCount { get; set; }
    public int TotalJobsCount { get; set; }
    public int SavedJobsCount { get; set; }
    public int UnreadNotificationsCount { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        try
        {
            // Get user's saved jobs with job details
            SavedJobs = await _dbContext.SavedJobs
                .Where(sj => sj.UserId == userId)
                .Include(sj => sj.Job)
                .OrderByDescending(sj => sj.SavedDate)
                .Take(5) // Only get the 5 most recent saved jobs for the dashboard
                .ToListAsync();

            SavedJobsCount = await _dbContext.SavedJobs
                .Where(sj => sj.UserId == userId)
                .CountAsync();

            // Get user's keywords
            UserKeywords = await _dbContext.Keywords
                .Where(k => k.UserId == userId)
                .OrderByDescending(k => k.CreatedDate)
                .ToListAsync();

            // Get user's resume
            UserResume = await _dbContext.Resumes
                .FirstOrDefaultAsync(r => r.UserId == userId);

            // Get count of new jobs in the last 24 hours
            var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
            NewJobsCount = await _dbContext.Jobs
                .Where(j => j.IsActive && j.ScrapedDate >= yesterday)
                .CountAsync();

            // Get total active jobs count
            TotalJobsCount = await _dbContext.Jobs
                .Where(j => j.IsActive)
                .CountAsync();

            // Get unread notifications count
            UnreadNotificationsCount = await _dbContext.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard data");
            ModelState.AddModelError(string.Empty, "An error occurred while retrieving your dashboard data. Please try again later.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRemoveSavedJobAsync(Guid savedJobId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        var savedJob = await _dbContext.SavedJobs
            .FirstOrDefaultAsync(sj => sj.Id == savedJobId && sj.UserId == userId);

        if (savedJob != null)
        {
            _dbContext.SavedJobs.Remove(savedJob);
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage();
    }
}
