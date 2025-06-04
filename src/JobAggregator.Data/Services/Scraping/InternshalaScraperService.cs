using System.Text.RegularExpressions;
using HtmlAgilityPack;
using JobAggregator.Data.Models;
using Microsoft.Extensions.Logging;

namespace JobAggregator.Data.Services.Scraping;

/// <summary>
/// Internshala job scraper service implementation
/// </summary>
public class InternshalaScraperService : BaseJobScraperService
{
    private readonly ILogger<InternshalaScraperService> _logger;
    
    public override string SourceName => "Internshala";
    public override string BaseUrl => "https://internshala.com";
    
    public InternshalaScraperService(ILogger<InternshalaScraperService> logger, HttpClient httpClient) 
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
                // Internshala search URL format
                var searchUrl = $"{BaseUrl}/internships/{Uri.EscapeDataString(keyword)}-internship";
                
                _logger.LogInformation($"Scraping Internshala internships for keyword: {keyword}");
                
                var response = await _httpClient.GetAsync(searchUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to get Internshala search results for {keyword}. Status code: {response.StatusCode}");
                    continue;
                }
                
                var content = await response.Content.ReadAsStringAsync();
                var jobsFromSearch = ParseInternshalaSearchResults(content);
                
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
                HandleScrapingException(ex, $"Error scraping Internshala jobs for keyword {keyword}");
            }
        }
        
        return jobs;
    }
    
    public override async Task<Job?> ScrapeJobByUrlAsync(string jobUrl)
    {
        try
        {
            _logger.LogInformation($"Scraping Internshala job details from: {jobUrl}");
            
            var response = await _httpClient.GetAsync(jobUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to get Internshala job details from {jobUrl}. Status code: {response.StatusCode}");
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            return ParseInternshalaJobDetails(content, jobUrl);
        }
        catch (Exception ex)
        {
            HandleScrapingException(ex, $"Error scraping job details from {jobUrl}");
            return null;
        }
    }
    
    private IEnumerable<Job> ParseInternshalaSearchResults(string html)
    {
        var jobs = new List<Job>();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        // Internshala job cards are typically in divs with specific classes
        var jobCards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'internship_meta')]");
        
        if (jobCards == null)
        {
            _logger.LogWarning("No job cards found in Internshala search results");
            return jobs;
        }
        
        foreach (var card in jobCards)
        {
            try
            {
                var job = new Job
                {
                    Id = Guid.NewGuid(),
                    SourcePlatform = SourceName,
                    JobType = "Internship" // Internshala is primarily for internships
                };
                
                // Extract job title
                var titleNode = card.SelectSingleNode(".//a[contains(@class, 'view_detail_button')]");
                if (titleNode != null)
                {
                    job.Title = titleNode.InnerText.Trim();
                    
                    // Extract job URL
                    job.JobUrl = titleNode.GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(job.JobUrl) && !job.JobUrl.StartsWith("http"))
                    {
                        job.JobUrl = $"{BaseUrl}{job.JobUrl}";
                    }
                }
                else
                {
                    job.Title = "Unknown Internship";
                }
                
                // Extract company name
                var companyNode = card.SelectSingleNode(".//a[contains(@class, 'link_display_like_text')]");
                job.CompanyName = companyNode?.InnerText.Trim() ?? "Unknown Company";
                
                // Extract location
                var locationNode = card.SelectSingleNode(".//div[contains(@class, 'location_meta')]");
                job.Location = locationNode?.InnerText.Trim();
                
                // Check if remote
                job.IsRemote = job.Location?.Contains("Work from Home", StringComparison.OrdinalIgnoreCase) == true;
                
                // Extract stipend/salary if available
                var stipendNode = card.SelectSingleNode(".//span[contains(@class, 'stipend')]");
                if (stipendNode != null)
                {
                    job.Salary = stipendNode.InnerText.Trim();
                }
                
                // Extract posting date or application deadline
                var dateNode = card.SelectSingleNode(".//div[contains(@class, 'apply_by')]");
                if (dateNode != null)
                {
                    var dateText = dateNode.InnerText.Trim();
                    var match = Regex.Match(dateText, @"Apply By: (\d{1,2} [A-Za-z]+ \d{4})");
                    if (match.Success)
                    {
                        if (DateTimeOffset.TryParse(match.Groups[1].Value, out var deadline))
                        {
                            // Estimate posting date as 30 days before deadline
                            job.PostingDate = deadline.AddDays(-30);
                        }
                    }
                }
                
                // Extract duration if available
                var durationNode = card.SelectSingleNode(".//div[contains(@class, 'internship_other_details_container')]/div[contains(@class, 'item_body')][1]");
                if (durationNode != null)
                {
                    job.Duration = durationNode.InnerText.Trim();
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
                _logger.LogError(ex, "Error parsing Internshala job card");
            }
        }
        
        return jobs;
    }
    
    private Job? ParseInternshalaJobDetails(string html, string jobUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        try
        {
            var job = new Job
            {
                Id = Guid.NewGuid(),
                SourcePlatform = SourceName,
                JobUrl = jobUrl,
                JobType = "Internship" // Internshala is primarily for internships
            };
            
            // Extract job title
            var titleNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'profile_on_detail_page')]");
            job.Title = titleNode?.InnerText.Trim() ?? "Unknown Internship";
            
            // Extract company name
            var companyNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'company_name')]");
            job.CompanyName = companyNode?.InnerText.Trim() ?? "Unknown Company";
            
            // Extract location
            var locationNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'location_name')]");
            job.Location = locationNode?.InnerText.Trim();
            
            // Check if remote
            job.IsRemote = job.Location?.Contains("Work from Home", StringComparison.OrdinalIgnoreCase) == true;
            
            // Extract job description
            var descriptionNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'internship_details')]");
            job.Description = descriptionNode?.InnerHtml.Trim() ?? "";
            
            // Extract stipend/salary if available
            var stipendNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'stipend_container')]");
            if (stipendNode != null)
            {
                job.Salary = stipendNode.InnerText.Trim();
            }
            
            // Extract posting date or application deadline
            var dateNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'apply_by')]");
            if (dateNode != null)
            {
                var dateText = dateNode.InnerText.Trim();
                var match = Regex.Match(dateText, @"Apply By: (\d{1,2} [A-Za-z]+ \d{4})");
                if (match.Success)
                {
                    if (DateTimeOffset.TryParse(match.Groups[1].Value, out var deadline))
                    {
                        // Estimate posting date as 30 days before deadline
                        job.PostingDate = deadline.AddDays(-30);
                    }
                }
            }
            
            // Extract duration if available
            var durationNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'internship_details')]/div[contains(@class, 'item_body')][1]");
            if (durationNode != null)
            {
                job.Duration = durationNode.InnerText.Trim();
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
            _logger.LogError(ex, $"Error parsing Internshala job details from {jobUrl}");
            return null;
        }
    }
}
