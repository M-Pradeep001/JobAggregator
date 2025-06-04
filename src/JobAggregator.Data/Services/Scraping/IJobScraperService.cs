using System.Threading.Tasks;
using JobAggregator.Data.Models;

namespace JobAggregator.Data.Services.Scraping;

/// <summary>
/// Base interface for all job scraping services
/// </summary>
public interface IJobScraperService
{
    /// <summary>
    /// Gets the name of the job source (e.g., "LinkedIn", "Naukri")
    /// </summary>
    string SourceName { get; }
    
    /// <summary>
    /// Gets the base URL of the job source
    /// </summary>
    string BaseUrl { get; }
    
    /// <summary>
    /// Indicates whether this scraper is enabled
    /// </summary>
    bool IsEnabled { get; }
    
    /// <summary>
    /// Scrapes jobs based on the provided search criteria
    /// </summary>
    /// <param name="keywords">Keywords to search for</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <returns>A collection of scraped jobs</returns>
    Task<IEnumerable<Job>> ScrapeJobsAsync(IEnumerable<string> keywords, int maxResults = 100);
    
    /// <summary>
    /// Scrapes a specific job by its URL
    /// </summary>
    /// <param name="jobUrl">URL of the job to scrape</param>
    /// <returns>The scraped job, or null if not found</returns>
    Task<Job?> ScrapeJobByUrlAsync(string jobUrl);
    
    /// <summary>
    /// Tests the connection to the job source
    /// </summary>
    /// <returns>True if the connection is successful, false otherwise</returns>
    Task<bool> TestConnectionAsync();
}
