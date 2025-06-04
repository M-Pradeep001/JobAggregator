using JobAggregator.Data.Models;
using JobAggregator.Data.Services.Scraping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JobAggregator.Data.Services;

/// <summary>
/// Service for aggregating jobs from multiple sources
/// </summary>
public class JobAggregationService
{
    private readonly ILogger<JobAggregationService> _logger;
    private readonly IEnumerable<IJobScraperService> _scraperServices;
    private readonly ApplicationDbContext _dbContext;
    
    public JobAggregationService(
        ILogger<JobAggregationService> logger,
        IEnumerable<IJobScraperService> scraperServices,
        ApplicationDbContext dbContext)
    {
        _logger = logger;
        _scraperServices = scraperServices;
        _dbContext = dbContext;
    }
    
    /// <summary>
    /// Aggregates jobs from all enabled scrapers based on the provided keywords
    /// </summary>
    public async Task<IEnumerable<Job>> AggregateJobsAsync(IEnumerable<string> keywords, int maxResultsPerSource = 50)
    {
        var allJobs = new List<Job>();
        var enabledScrapers = _scraperServices.Where(s => s.IsEnabled).ToList();
        
        _logger.LogInformation($"Starting job aggregation with {enabledScrapers.Count} enabled scrapers for {keywords.Count()} keywords");
        
        foreach (var scraper in enabledScrapers)
        {
            try
            {
                _logger.LogInformation($"Scraping jobs from {scraper.SourceName}");
                var jobs = await scraper.ScrapeJobsAsync(keywords, maxResultsPerSource);
                
                _logger.LogInformation($"Found {jobs.Count()} jobs from {scraper.SourceName}");
                allJobs.AddRange(jobs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error aggregating jobs from {scraper.SourceName}: {ex.Message}");
            }
        }
        
        _logger.LogInformation($"Job aggregation complete. Total jobs found: {allJobs.Count}");
        return allJobs;
    }
    
    /// <summary>
    /// Saves aggregated jobs to the database, avoiding duplicates
    /// </summary>
    public async Task<int> SaveAggregatedJobsAsync(IEnumerable<Job> jobs)
    {
        int newJobsCount = 0;
        
        foreach (var job in jobs)
        {
            try
            {
                // Check if job already exists by JobIdFromSource
                var existingJob = await _dbContext.Jobs
                    .FirstOrDefaultAsync(j => j.JobIdFromSource == job.JobIdFromSource);
                
                if (existingJob == null)
                {
                    // New job, add it
                    await _dbContext.Jobs.AddAsync(job);
                    newJobsCount++;
                }
                else
                {
                    // Update existing job's LastSeenDate and IsActive status
                    existingJob.LastSeenDate = DateTimeOffset.UtcNow;
                    existingJob.IsActive = true;
                    
                    // Update any other fields that might have changed
                    existingJob.Title = job.Title;
                    existingJob.Description = job.Description;
                    existingJob.Location = job.Location;
                    existingJob.Salary = job.Salary;
                    existingJob.JobType = job.JobType;
                    existingJob.IsRemote = job.IsRemote;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving job {job.Title} from {job.SourcePlatform}: {ex.Message}");
            }
        }
        
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation($"Saved {newJobsCount} new jobs to the database");
        
        return newJobsCount;
    }
    
    /// <summary>
    /// Gets personalized job recommendations for a user based on their keywords
    /// </summary>
    public async Task<IEnumerable<Job>> GetPersonalizedJobsAsync(string userId, int maxResults = 100)
    {
        // Get user's keywords
        var userKeywords = await _dbContext.Keywords
            .Where(k => k.UserId == userId)
            .Select(k => k.KeywordText)
            .ToListAsync();
        
        if (!userKeywords.Any())
        {
            _logger.LogInformation($"No keywords found for user {userId}");
            return new List<Job>();
        }
        
        // Build a query for jobs matching any of the user's keywords
        var query = _dbContext.Jobs
            .Where(j => j.IsActive)
            .OrderByDescending(j => j.PostingDate)
            .AsQueryable();
        
        // Filter by keywords in title or description
        var keywordPredicates = userKeywords.Select(keyword => 
            (Expression<Func<Job, bool>>)(j => 
                j.Title.Contains(keyword) || 
                j.Description.Contains(keyword)
            )
        ).ToList();
        
        // Combine predicates with OR
        if (keywordPredicates.Any())
        {
            var predicate = keywordPredicates.First();
            foreach (var additionalPredicate in keywordPredicates.Skip(1))
            {
                predicate = PredicateBuilder.Or(predicate, additionalPredicate);
            }
            
            query = query.Where(predicate);
        }
        
        return await query.Take(maxResults).ToListAsync();
    }
    
    /// <summary>
    /// Filters jobs based on various criteria
    /// </summary>
    public async Task<IEnumerable<Job>> FilterJobsAsync(
        string? searchTerm = null,
        DateTimeOffset? fromDate = null,
        string? company = null,
        string? jobType = null,
        bool? isRemote = null,
        string? location = null,
        int maxResults = 100)
    {
        var query = _dbContext.Jobs
            .Where(j => j.IsActive)
            .AsQueryable();
        
        // Apply search term filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(j => 
                j.Title.Contains(searchTerm) || 
                j.Description.Contains(searchTerm) ||
                j.CompanyName.Contains(searchTerm)
            );
        }
        
        // Apply date filter
        if (fromDate.HasValue)
        {
            query = query.Where(j => j.PostingDate >= fromDate.Value);
        }
        
        // Apply company filter
        if (!string.IsNullOrWhiteSpace(company))
        {
            query = query.Where(j => j.CompanyName.Contains(company));
        }
        
        // Apply job type filter
        if (!string.IsNullOrWhiteSpace(jobType))
        {
            query = query.Where(j => j.JobType == jobType);
        }
        
        // Apply remote filter
        if (isRemote.HasValue)
        {
            query = query.Where(j => j.IsRemote == isRemote.Value);
        }
        
        // Apply location filter
        if (!string.IsNullOrWhiteSpace(location))
        {
            query = query.Where(j => j.Location.Contains(location));
        }
        
        // Order by posting date (newest first)
        query = query.OrderByDescending(j => j.PostingDate);
        
        return await query.Take(maxResults).ToListAsync();
    }
}

/// <summary>
/// Helper class for combining predicates
/// </summary>
public static class PredicateBuilder
{
    public static Expression<Func<T, bool>> Or<T>(Expression<Func<T, bool>> expr1, Expression<Func<T, bool>> expr2)
    {
        var invokedExpr = Expression.Invoke(expr2, expr1.Parameters);
        return Expression.Lambda<Func<T, bool>>(
            Expression.OrElse(expr1.Body, invokedExpr), expr1.Parameters);
    }
}
