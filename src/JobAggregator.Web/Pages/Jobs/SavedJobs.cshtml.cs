using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using JobAggregator.Data.Models;
using JobAggregator.Data.Services;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace JobAggregator.Web.Pages.Jobs;

[Authorize]
public class SavedJobsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<SavedJobsModel> _logger;

    public SavedJobsModel(
        ApplicationDbContext dbContext,
        ILogger<SavedJobsModel> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public IList<SavedJob> SavedJobs { get; set; } = new List<SavedJob>();
    
    // Filter properties
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? Company { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? JobType { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public bool? IsRemote { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? DateFilter { get; set; }
    
    public IList<string> AvailableCompanies { get; set; } = new List<string>();
    public IList<string> AvailableJobTypes { get; set; } = new List<string>();

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        try
        {
            // Build query
            var query = _dbContext.SavedJobs
                .Where(sj => sj.UserId == userId)
                .Include(sj => sj.Job)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(SearchTerm))
            {
                query = query.Where(sj => 
                    sj.Job.Title.Contains(SearchTerm) || 
                    sj.Job.Description.Contains(SearchTerm) ||
                    sj.Job.CompanyName.Contains(SearchTerm));
            }

            if (!string.IsNullOrEmpty(Company))
            {
                query = query.Where(sj => sj.Job.CompanyName == Company);
            }

            if (!string.IsNullOrEmpty(JobType))
            {
                query = query.Where(sj => sj.Job.JobType == JobType);
            }

            if (IsRemote.HasValue)
            {
                query = query.Where(sj => sj.Job.IsRemote == IsRemote.Value);
            }

            if (!string.IsNullOrEmpty(DateFilter))
            {
                var fromDate = DateFilter switch
                {
                    "today" => DateTimeOffset.UtcNow.AddDays(-1),
                    "week" => DateTimeOffset.UtcNow.AddDays(-7),
                    "month" => DateTimeOffset.UtcNow.AddMonths(-1),
                    _ => (DateTimeOffset?)null
                };

                if (fromDate.HasValue)
                {
                    query = query.Where(sj => sj.SavedDate >= fromDate.Value);
                }
            }

            // Order by saved date (newest first)
            query = query.OrderByDescending(sj => sj.SavedDate);

            // Execute query
            SavedJobs = await query.ToListAsync();

            // Get filter options
            await LoadFilterOptions(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving saved jobs");
            ModelState.AddModelError(string.Empty, "An error occurred while retrieving your saved jobs. Please try again later.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid savedJobId)
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

    public async Task<IActionResult> OnPostAddNoteAsync(Guid savedJobId, string note)
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
            savedJob.Notes = note;
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    private async Task LoadFilterOptions(string userId)
    {
        // Get companies from saved jobs
        AvailableCompanies = await _dbContext.SavedJobs
            .Where(sj => sj.UserId == userId)
            .Select(sj => sj.Job.CompanyName)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        // Get job types from saved jobs
        AvailableJobTypes = await _dbContext.SavedJobs
            .Where(sj => sj.UserId == userId)
            .Where(sj => !string.IsNullOrEmpty(sj.Job.JobType))
            .Select(sj => sj.Job.JobType)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
    }
}
