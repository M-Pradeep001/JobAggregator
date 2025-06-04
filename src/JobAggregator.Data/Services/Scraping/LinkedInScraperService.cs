using System.Text.RegularExpressions;
using HtmlAgilityPack;
using JobAggregator.Data.Models;
using Microsoft.Extensions.Logging;

namespace JobAggregator.Data.Services.Scraping;

/// <summary>
/// LinkedIn job scraper service implementation
/// </summary>
public class LinkedInScraperService : BaseJobScraperService
{
    private readonly ILogger<LinkedInScraperService> _logger;
    
    public override string SourceName => "LinkedIn";
    public override string BaseUrl => "https://www.linkedin.com/jobs";
    
    public LinkedInScraperService(ILogger<LinkedInScraperService> logger, HttpClient httpClient) 
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
                // LinkedIn search URL format
                var searchUrl = $"{BaseUrl}/search?keywords={Uri.EscapeDataString(keyword)}&position=1&pageNum=0";
                
                _logger.LogInformation($"Scraping LinkedIn jobs for keyword: {keyword}");
                
                var response = await _httpClient.GetAsync(searchUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to get LinkedIn search results for {keyword}. Status code: {response.StatusCode}");
                    continue;
                }
                
                var content = await response.Content.ReadAsStringAsync();
                var jobsFromSearch = ParseLinkedInSearchResults(content);
                
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
                HandleScrapingException(ex, $"Error scraping LinkedIn jobs for keyword {keyword}");
            }
        }
        
        return jobs;
    }
    
    public override async Task<Job?> ScrapeJobByUrlAsync(string jobUrl)
    {
        try
        {
            _logger.LogInformation($"Scraping LinkedIn job details from: {jobUrl}");
            
            var response = await _httpClient.GetAsync(jobUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to get LinkedIn job details from {jobUrl}. Status code: {response.StatusCode}");
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            return ParseLinkedInJobDetails(content, jobUrl);
        }
        catch (Exception ex)
        {
            HandleScrapingException(ex, $"Error scraping job details from {jobUrl}");
            return null;
        }
    }
    
    private IEnumerable<Job> ParseLinkedInSearchResults(string html)
    {
        var jobs = new List<Job>();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        // LinkedIn job cards are typically in a list
        var jobCards = doc.DocumentNode.SelectNodes("//li[contains(@class, 'job-search-card')]");
        
        if (jobCards == null)
        {
            _logger.LogWarning("No job cards found in LinkedIn search results");
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
                var titleNode = card.SelectSingleNode(".//h3[contains(@class, 'base-search-card__title')]");
                job.Title = titleNode?.InnerText.Trim() ?? "Unknown Title";
                
                // Extract company name
                var companyNode = card.SelectSingleNode(".//h4[contains(@class, 'base-search-card__subtitle')]");
                job.CompanyName = companyNode?.InnerText.Trim() ?? "Unknown Company";
                
                // Extract location
                var locationNode = card.SelectSingleNode(".//span[contains(@class, 'job-search-card__location')]");
                job.Location = locationNode?.InnerText.Trim();
                
                // Extract job URL
                var linkNode = card.SelectSingleNode(".//a[contains(@class, 'base-card__full-link')]");
                job.JobUrl = linkNode?.GetAttributeValue("href", "")?.Split('?').FirstOrDefault();
                
                // Extract posting date
                var dateNode = card.SelectSingleNode(".//time");
                if (dateNode != null)
                {
                    var dateStr = dateNode.GetAttributeValue("datetime", "");
                    if (DateTimeOffset.TryParse(dateStr, out var postDate))
                    {
                        job.PostingDate = postDate;
                    }
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
                _logger.LogError(ex, "Error parsing LinkedIn job card");
            }
        }
        
        return jobs;
    }
    
    private Job? ParseLinkedInJobDetails(string html, string jobUrl)
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
            var titleNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'job-title')]");
            job.Title = titleNode?.InnerText.Trim() ?? "Unknown Title";
            
            // Extract company name
            var companyNode = doc.DocumentNode.SelectSingleNode("//a[contains(@class, 'company-name')]");
            job.CompanyName = companyNode?.InnerText.Trim() ?? "Unknown Company";
            
            // Extract location
            var locationNode = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'job-location')]");
            job.Location = locationNode?.InnerText.Trim();
            
            // Extract job description
            var descriptionNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'description__text')]");
            job.Description = descriptionNode?.InnerHtml.Trim() ?? "";
            
            // Extract job type if available
            var jobTypeNode = doc.DocumentNode.SelectSingleNode("//span[contains(text(), 'Employment type')]/following-sibling::span");
            job.JobType = jobTypeNode?.InnerText.Trim();
            
            // Check if remote
            job.IsRemote = job.Location?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true || 
                          job.Title?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true ||
                          job.Description?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true;
            
            // Extract posting date
            var dateNode = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'posted-date')]");
            if (dateNode != null)
            {
                var dateText = dateNode.InnerText.Trim();
                // Parse relative dates like "Posted 3 days ago"
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
            _logger.LogError(ex, $"Error parsing LinkedIn job details from {jobUrl}");
            return null;
        }
    }
}
