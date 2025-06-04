using System.Text.RegularExpressions;
using HtmlAgilityPack;
using JobAggregator.Data.Models;
using Microsoft.Extensions.Logging;

namespace JobAggregator.Data.Services.Scraping;

/// <summary>
/// Naukri job scraper service implementation
/// </summary>
public class NaukriScraperService : BaseJobScraperService
{
    private readonly ILogger<NaukriScraperService> _logger;
    
    public override string SourceName => "Naukri";
    public override string BaseUrl => "https://www.naukri.com";
    
    public NaukriScraperService(ILogger<NaukriScraperService> logger, HttpClient httpClient) 
        : base(logger, httpClient)
    {
        _logger = logger;
        
        // Configure HttpClient with appropriate headers to mimic a browser
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
    }
    
    public override async Task<IEnumerable<Job>> ScrapeJobsAsync(IEnumerable<string> keywords, int maxResults = 100)
    {
        var jobs = new List<Job>();
        
        foreach (var keyword in keywords)
        {
            try
            {
                // Naukri search URL format
                var searchUrl = $"{BaseUrl}/jobs-{Uri.EscapeDataString(keyword)}";
                
                _logger.LogInformation($"Scraping Naukri jobs for keyword: {keyword}");
                
                var response = await _httpClient.GetAsync(searchUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to get Naukri search results for {keyword}. Status code: {response.StatusCode}");
                    continue;
                }
                
                var content = await response.Content.ReadAsStringAsync();
                var jobsFromSearch = ParseNaukriSearchResults(content);
                
                // Limit results per keyword
                var keywordMaxResults = maxResults / keywords.Count();
                foreach (var job in jobsFromSearch.Take(keywordMaxResults))
                {
                    // Get detailed job info if we have the URL
                    if (!string.IsNullOrEmpty(job.JobUrl))
                    {
                        try
                        {
                            var detailedJob = await ScrapeJobByUrlAsync(job.JobUrl);
                            if (detailedJob != null)
                            {
                                jobs.Add(detailedJob);
                            }
                            else
                            {
                                jobs.Add(job);
                            }
                            
                            // Add a small delay to avoid rate limiting
                            await Task.Delay(TimeSpan.FromSeconds(1));
                        }
                        catch (Exception ex)
                        {
                            HandleScrapingException(ex, $"Error scraping job details from {job.JobUrl}");
                            jobs.Add(job); // Add the basic job info anyway
                        }
                    }
                    else
                    {
                        jobs.Add(job);
                    }
                    
                    // Check if we've reached the overall maximum
                    if (jobs.Count >= maxResults)
                    {
                        break;
                    }
                }
                
                // Check if we've reached the overall maximum
                if (jobs.Count >= maxResults)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                HandleScrapingException(ex, $"Error scraping Naukri jobs for keyword {keyword}");
            }
        }
        
        return jobs;
    }
    
    public override async Task<Job?> ScrapeJobByUrlAsync(string jobUrl)
    {
        try
        {
            _logger.LogInformation($"Scraping Naukri job details from: {jobUrl}");
            
            var response = await _httpClient.GetAsync(jobUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to get Naukri job details from {jobUrl}. Status code: {response.StatusCode}");
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            return ParseNaukriJobDetails(content, jobUrl);
        }
        catch (Exception ex)
        {
            HandleScrapingException(ex, $"Error scraping job details from {jobUrl}");
            return null;
        }
    }
    
    private IEnumerable<Job> ParseNaukriSearchResults(string html)
    {
        var jobs = new List<Job>();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        // Naukri job cards are typically in article elements
        var jobCards = doc.DocumentNode.SelectNodes("//article[contains(@class, 'jobTuple')]");
        
        if (jobCards == null)
        {
            _logger.LogWarning("No job cards found in Naukri search results");
            return jobs;
        }
        
        foreach (var card in jobCards)
        {
            try
            {
                var job = new Job
                {
                    Id = Guid.NewGuid(),
                    SourcePlatform = SourceName
                };
                
                // Extract job title
                var titleNode = card.SelectSingleNode(".//a[contains(@class, 'title')]");
                job.Title = titleNode?.InnerText.Trim() ?? "Unknown Title";
                
                // Extract job URL
                job.JobUrl = titleNode?.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(job.JobUrl) && !job.JobUrl.StartsWith("http"))
                {
                    job.JobUrl = $"{BaseUrl}{job.JobUrl}";
                }
                
                // Extract company name
                var companyNode = card.SelectSingleNode(".//a[contains(@class, 'companyName')]");
                job.CompanyName = companyNode?.InnerText.Trim() ?? "Unknown Company";
                
                // Extract location
                var locationNode = card.SelectSingleNode(".//span[contains(@class, 'location')]");
                job.Location = locationNode?.InnerText.Trim();
                
                // Extract job type if available
                var jobTypeNode = card.SelectSingleNode(".//span[contains(@class, 'jobType')]");
                job.JobType = jobTypeNode?.InnerText.Trim();
                
                // Check if remote
                var remoteNode = card.SelectSingleNode(".//span[contains(text(), 'Remote')]");
                job.IsRemote = remoteNode != null;
                
                // Extract posting date
                var dateNode = card.SelectSingleNode(".//span[contains(@class, 'postedDate')]");
                if (dateNode != null)
                {
                    var dateText = dateNode.InnerText.Trim();
                    // Parse relative dates like "3 days ago"
                    if (dateText.Contains("day", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(dateText, @"(\d+)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int days))
                        {
                            job.PostingDate = DateTimeOffset.UtcNow.AddDays(-days);
                        }
                    }
                    else if (dateText.Contains("hour", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(dateText, @"(\d+)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int hours))
                        {
                            job.PostingDate = DateTimeOffset.UtcNow.AddHours(-hours);
                        }
                    }
                    else if (dateText.Contains("week", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(dateText, @"(\d+)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int weeks))
                        {
                            job.PostingDate = DateTimeOffset.UtcNow.AddDays(-weeks * 7);
                        }
                    }
                    else if (dateText.Contains("month", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(dateText, @"(\d+)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int months))
                        {
                            job.PostingDate = DateTimeOffset.UtcNow.AddMonths(-months);
                        }
                    }
                }
                
                // Extract brief description if available
                var descNode = card.SelectSingleNode(".//div[contains(@class, 'job-description')]");
                if (descNode != null)
                {
                    job.Description = descNode.InnerText.Trim();
                }
                
                // Set scraping metadata
                job.ScrapedDate = DateTimeOffset.UtcNow;
                job.LastSeenDate = DateTimeOffset.UtcNow;
                job.IsActive = true;
                
                // Generate a job ID from source if we have enough info
                if (!string.IsNullOrEmpty(job.JobUrl) && !string.IsNullOrEmpty(job.Title) && !string.IsNullOrEmpty(job.CompanyName))
                {
                    job.JobIdFromSource = GenerateJobIdFromSource(job.JobUrl, job.Title, job.CompanyName);
                }
                
                jobs.Add(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Naukri job card");
            }
        }
        
        return jobs;
    }
    
    private Job? ParseNaukriJobDetails(string html, string jobUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        try
        {
            var job = new Job
            {
                Id = Guid.NewGuid(),
                SourcePlatform = SourceName,
                JobUrl = jobUrl
            };
            
            // Extract job title
            var titleNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'jd-header-title')]");
            job.Title = titleNode?.InnerText.Trim() ?? "Unknown Title";
            
            // Extract company name
            var companyNode = doc.DocumentNode.SelectSingleNode("//a[contains(@class, 'company-name')]");
            job.CompanyName = companyNode?.InnerText.Trim() ?? "Unknown Company";
            
            // Extract location
            var locationNode = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'location')]");
            job.Location = locationNode?.InnerText.Trim();
            
            // Extract job description
            var descriptionNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'job-description')]");
            job.Description = descriptionNode?.InnerHtml.Trim() ?? "";
            
            // Extract job type if available
            var jobTypeNode = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'jobType')]");
            job.JobType = jobTypeNode?.InnerText.Trim();
            
            // Check if remote
            var remoteNode = doc.DocumentNode.SelectSingleNode("//span[contains(text(), 'Remote')]");
            job.IsRemote = remoteNode != null || 
                          job.Location?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true || 
                          job.Title?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true ||
                          job.Description?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true;
            
            // Extract posting date
            var dateNode = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'posted-date')]");
            if (dateNode != null)
            {
                var dateText = dateNode.InnerText.Trim();
                // Parse relative dates
                if (dateText.Contains("day", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(dateText, @"(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int days))
                    {
                        job.PostingDate = DateTimeOffset.UtcNow.AddDays(-days);
                    }
                }
                else if (dateText.Contains("hour", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(dateText, @"(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int hours))
                    {
                        job.PostingDate = DateTimeOffset.UtcNow.AddHours(-hours);
                    }
                }
                else if (dateText.Contains("week", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(dateText, @"(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int weeks))
                    {
                        job.PostingDate = DateTimeOffset.UtcNow.AddDays(-weeks * 7);
                    }
                }
                else if (dateText.Contains("month", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(dateText, @"(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int months))
                    {
                        job.PostingDate = DateTimeOffset.UtcNow.AddMonths(-months);
                    }
                }
            }
            
            // Set scraping metadata
            job.ScrapedDate = DateTimeOffset.UtcNow;
            job.LastSeenDate = DateTimeOffset.UtcNow;
            job.IsActive = true;
            
            // Generate a job ID from source
            job.JobIdFromSource = GenerateJobIdFromSource(job.JobUrl, job.Title, job.CompanyName);
            
            return NormalizeJobData(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error parsing Naukri job details from {jobUrl}");
            return null;
        }
    }
}
