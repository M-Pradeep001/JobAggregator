using System.Net.Http;
using System.Threading.Tasks;
using JobAggregator.Data.Models;
using Microsoft.Extensions.Logging;

namespace JobAggregator.Data.Services.Scraping;

/// <summary>
/// Base abstract class for job scraper services that implements common functionality
/// </summary>
public abstract class BaseJobScraperService : IJobScraperService
{
    protected readonly ILogger _logger;
    protected readonly HttpClient _httpClient;
    
    public abstract string SourceName { get; }
    public abstract string BaseUrl { get; }
    public virtual bool IsEnabled { get; set; } = true;
    
    protected BaseJobScraperService(ILogger logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }
    
    public abstract Task<IEnumerable<Job>> ScrapeJobsAsync(IEnumerable<string> keywords, int maxResults = 100);
    
    public abstract Task<Job?> ScrapeJobByUrlAsync(string jobUrl);
    
    public virtual async Task<bool> TestConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(BaseUrl);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error testing connection to {SourceName}: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Helper method to normalize job data
    /// </summary>
    protected virtual Job NormalizeJobData(Job job)
    {
        // Ensure the job has the correct source platform
        job.SourcePlatform = SourceName;
        
        // Set dates if not already set
        if (job.ScrapedDate == default)
        {
            job.ScrapedDate = DateTimeOffset.UtcNow;
        }
        
        if (job.LastSeenDate == default)
        {
            job.LastSeenDate = DateTimeOffset.UtcNow;
        }
        
        // Ensure job is active by default
        job.IsActive = true;
        
        return job;
    }
    
    /// <summary>
    /// Helper method to generate a unique job ID from source
    /// </summary>
    protected virtual string GenerateJobIdFromSource(string url, string title, string company)
    {
        // Create a deterministic ID based on URL, title, and company
        var input = $"{url}|{title}|{company}";
        using var md5 = System.Security.Cryptography.MD5.Create();
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLower();
    }
    
    /// <summary>
    /// Helper method to handle common exceptions during scraping
    /// </summary>
    protected virtual void HandleScrapingException(Exception ex, string context)
    {
        _logger.LogError(ex, $"Error scraping {SourceName} - {context}: {ex.Message}");
        
        // Additional handling for specific exception types
        if (ex is HttpRequestException)
        {
            _logger.LogWarning($"Network error while scraping {SourceName}. Possible rate limiting or connectivity issues.");
        }
        else if (ex is TaskCanceledException)
        {
            _logger.LogWarning($"Request timeout while scraping {SourceName}.");
        }
    }
}
