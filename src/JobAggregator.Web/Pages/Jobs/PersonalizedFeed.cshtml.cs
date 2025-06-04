using JobAggregator.Data.Models;
using JobAggregator.Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JobAggregator.Web.Pages.Jobs;

[Authorize]
public class PersonalizedFeedModel : PageModel
{
    private readonly JobAggregationService _jobAggregationService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PersonalizedFeedModel> _logger;

    public PersonalizedFeedModel(
        JobAggregationService jobAggregationService,
        ApplicationDbContext dbContext,
        ILogger<PersonalizedFeedModel> logger)
    {
        _jobAggregationService = jobAggregationService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public IList<Job> Jobs { get; set; } = new List<Job>();
    public IList<string> UserKeywords { get; set; } = new List<string>();
    
    [BindProperty]
    public string NewKeyword { get; set; } = string.Empty;
    
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
    public string? Location { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? DateFilter { get; set; }
    
    public IList<string> AvailableCompanies { get; set; } = new List<string>();
    public IList<string> AvailableJobTypes { get; set; } = new List<string>();
    public IList<string> AvailableLocations { get; set; } = new List<string>();

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        // Get user's keywords
        UserKeywords = await _dbContext.Keywords
            .Where(k => k.UserId == userId)
            .Select(k => k.KeywordText)
            .ToListAsync();

        // Get filter options for dropdowns
        await LoadFilterOptions();

        // Apply filters
        DateTimeOffset? fromDate = null;
        if (!string.IsNullOrEmpty(DateFilter))
        {
            fromDate = DateFilter switch
            {
                "today" => DateTimeOffset.UtcNow.AddDays(-1),
                "week" => DateTimeOffset.UtcNow.AddDays(-7),
                "month" => DateTimeOffset.UtcNow.AddMonths(-1),
                _ => null
            };
        }

        // Get personalized jobs with filters
        if (UserKeywords.Any())
        {
            try
            {
                if (!string.IsNullOrEmpty(SearchTerm) || !string.IsNullOrEmpty(Company) || 
                    !string.IsNullOrEmpty(JobType) || IsRemote.HasValue || 
                    !string.IsNullOrEmpty(Location) || fromDate.HasValue)
                {
                    // Apply filters
                    Jobs = (await _jobAggregationService.FilterJobsAsync(
                        SearchTerm, fromDate, Company, JobType, IsRemote, Location)).ToList();
                }
                else
                {
                    // Get personalized jobs based on user keywords
                    Jobs = (await _jobAggregationService.GetPersonalizedJobsAsync(userId)).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving personalized jobs");
                ModelState.AddModelError(string.Empty, "An error occurred while retrieving jobs. Please try again later.");
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAddKeywordAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        if (!string.IsNullOrWhiteSpace(NewKeyword))
        {
            // Check if keyword already exists for this user
            var existingKeyword = await _dbContext.Keywords
                .FirstOrDefaultAsync(k => k.UserId == userId && k.KeywordText == NewKeyword);

            if (existingKeyword == null)
            {
                // Add new keyword
                var keyword = new Keyword
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    KeywordText = NewKeyword,
                    CreatedDate = DateTimeOffset.UtcNow
                };

                await _dbContext.Keywords.AddAsync(keyword);
                await _dbContext.SaveChangesAsync();
            }
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteKeywordAsync(string keyword)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        var keywordToDelete = await _dbContext.Keywords
            .FirstOrDefaultAsync(k => k.UserId == userId && k.KeywordText == keyword);

        if (keywordToDelete != null)
        {
            _dbContext.Keywords.Remove(keywordToDelete);
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveJobAsync(Guid jobId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        // Check if job is already saved
        var existingSavedJob = await _dbContext.SavedJobs
            .FirstOrDefaultAsync(sj => sj.UserId == userId && sj.JobId == jobId);

        if (existingSavedJob == null)
        {
            // Save the job
            var savedJob = new SavedJob
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                JobId = jobId,
                SavedDate = DateTimeOffset.UtcNow
            };

            await _dbContext.SavedJobs.AddAsync(savedJob);
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    private async Task LoadFilterOptions()
    {
        // Get distinct companies for filter dropdown
        AvailableCompanies = await _dbContext.Jobs
            .Where(j => j.IsActive)
            .Select(j => j.CompanyName)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        // Get distinct job types for filter dropdown
        AvailableJobTypes = await _dbContext.Jobs
            .Where(j => j.IsActive && !string.IsNullOrEmpty(j.JobType))
            .Select(j => j.JobType)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();

        // Get distinct locations for filter dropdown (top 20 most common)
        AvailableLocations = await _dbContext.Jobs
            .Where(j => j.IsActive && !string.IsNullOrEmpty(j.Location))
            .GroupBy(j => j.Location)
            .OrderByDescending(g => g.Count())
            .Take(20)
            .Select(g => g.Key)
            .ToListAsync();
    }
}
