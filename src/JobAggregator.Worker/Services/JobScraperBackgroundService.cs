using JobAggregator.Data.Models;
using JobAggregator.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobAggregator.Worker.Services;

/// <summary>
/// Background service for periodic job scraping and notification generation
/// </summary>
public class JobScraperBackgroundService : BackgroundService
{
    private readonly ILogger<JobScraperBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    
    // Run every 6 hours by default
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    public JobScraperBackgroundService(
        ILogger<JobScraperBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job Scraper Background Service is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Job Scraper Background Service is running at: {time}", DateTimeOffset.Now);
            
            try
            {
                await ScrapeJobsAndNotifyUsers(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while scraping jobs");
            }

            _logger.LogInformation("Job Scraper Background Service is sleeping until: {time}", DateTimeOffset.Now.Add(_interval));
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ScrapeJobsAndNotifyUsers(CancellationToken stoppingToken)
    {
        // Create a scope to resolve scoped services
        using var scope = _serviceProvider.CreateScope();
        
        try
        {
            // Get required services
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var jobAggregationService = scope.ServiceProvider.GetRequiredService<JobAggregationService>();
            
            // Get all active keywords from all users
            var allKeywords = await dbContext.Keywords
                .Select(k => k.KeywordText)
                .Distinct()
                .ToListAsync(stoppingToken);
            
            if (!allKeywords.Any())
            {
                _logger.LogInformation("No keywords found for job scraping");
                return;
            }
            
            _logger.LogInformation("Starting job scraping with {count} unique keywords", allKeywords.Count);
            
            // Scrape jobs using all keywords
            var scrapedJobs = await jobAggregationService.AggregateJobsAsync(allKeywords);
            
            // Save jobs to database
            var newJobsCount = await jobAggregationService.SaveAggregatedJobsAsync(scrapedJobs);
            
            _logger.LogInformation("Job scraping completed. Found {total} jobs, {new} are new", 
                scrapedJobs.Count(), newJobsCount);
            
            if (newJobsCount > 0)
            {
                await GenerateNotifications(dbContext, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ScrapeJobsAndNotifyUsers");
        }
    }

    private async Task GenerateNotifications(ApplicationDbContext dbContext, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Generating notifications for users");
            
            // Get all users with keywords
            var usersWithKeywords = await dbContext.Keywords
                .Select(k => k.UserId)
                .Distinct()
                .ToListAsync(stoppingToken);
            
            var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
            
            foreach (var userId in usersWithKeywords)
            {
                // Get user's keywords
                var userKeywords = await dbContext.Keywords
                    .Where(k => k.UserId == userId)
                    .Select(k => k.KeywordText)
                    .ToListAsync(stoppingToken);
                
                if (!userKeywords.Any())
                {
                    continue;
                }
                
                // Find new jobs matching user's keywords
                var keywordPredicates = userKeywords.Select(keyword => 
                    (Expression<Func<Job, bool>>)(j => 
                        j.Title.Contains(keyword) || 
                        j.Description.Contains(keyword)
                    )
                ).ToList();
                
                // Combine predicates with OR
                var predicate = keywordPredicates.First();
                foreach (var additionalPredicate in keywordPredicates.Skip(1))
                {
                    predicate = PredicateBuilder.Or(predicate, additionalPredicate);
                }
                
                // Get new jobs matching user's keywords
                var matchingNewJobs = await dbContext.Jobs
                    .Where(j => j.IsActive && j.ScrapedDate >= yesterday)
                    .Where(predicate)
                    .ToListAsync(stoppingToken);
                
                if (!matchingNewJobs.Any())
                {
                    continue;
                }
                
                _logger.LogInformation("Found {count} new matching jobs for user {userId}", 
                    matchingNewJobs.Count, userId);
                
                // Create a summary notification if there are multiple jobs
                if (matchingNewJobs.Count > 1)
                {
                    var notification = new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Title = $"New Jobs Found: {matchingNewJobs.Count} new matches",
                        Message = $"We found {matchingNewJobs.Count} new jobs matching your keywords: {string.Join(", ", userKeywords.Take(3))}",
                        SentDate = DateTimeOffset.UtcNow,
                        IsRead = false,
                        NotificationType = "JobAlert"
                    };
                    
                    await dbContext.Notifications.AddAsync(notification, stoppingToken);
                }
                else
                {
                    // Create individual notification for a single job
                    var job = matchingNewJobs.First();
                    
                    var notification = new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Title = $"New Job: {job.Title}",
                        Message = $"New job at {job.CompanyName}: {job.Title}",
                        SentDate = DateTimeOffset.UtcNow,
                        IsRead = false,
                        NotificationType = "JobAlert",
                        JobId = job.Id
                    };
                    
                    await dbContext.Notifications.AddAsync(notification, stoppingToken);
                }
            }
            
            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Notifications generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating notifications");
        }
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
