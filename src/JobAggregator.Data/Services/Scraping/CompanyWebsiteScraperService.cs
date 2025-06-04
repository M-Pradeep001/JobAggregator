using System.Text.RegularExpressions;
using HtmlAgilityPack;
using JobAggregator.Data.Models;
using Microsoft.Extensions.Logging;

namespace JobAggregator.Data.Services.Scraping;

/// <summary>
/// Company website job scraper service implementation
/// This scraper is designed to handle direct scraping from company career pages
/// </summary>
public class CompanyWebsiteScraperService : BaseJobScraperService
{
    private readonly ILogger<CompanyWebsiteScraperService> _logger;
    private readonly Dictionary<string, CompanyScrapingConfig> _companyConfigs;
    
    public override string SourceName => "Company Website";
    public override string BaseUrl => ""; // No single base URL as this handles multiple company websites
    
    public CompanyWebsiteScraperService(ILogger<CompanyWebsiteScraperService> logger, HttpClient httpClient) 
        : base(logger, httpClient)
    {
        _logger = logger;
        
        // Configure HttpClient with appropriate headers to mimic a browser
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        
        // Initialize company-specific scraping configurations
        _companyConfigs = InitializeCompanyConfigs();
    }
    
    private Dictionary<string, CompanyScrapingConfig> InitializeCompanyConfigs()
    {
        return new Dictionary<string, CompanyScrapingConfig>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "Microsoft", new CompanyScrapingConfig
                {
                    CompanyName = "Microsoft",
                    CareerUrl = "https://careers.microsoft.com/us/en/search-results",
                    JobCardSelector = "//div[contains(@class, 'job-card')]",
                    TitleSelector = ".//h3[contains(@class, 'job-title')]",
                    LocationSelector = ".//span[contains(@class, 'job-location')]",
                    UrlSelector = ".//a[contains(@class, 'job-link')]",
                    UrlAttribute = "href",
                    DescriptionSelector = "//div[contains(@class, 'job-description')]",
                    DateSelector = ".//span[contains(@class, 'job-date')]"
                }
            },
            {
                "Google", new CompanyScrapingConfig
                {
                    CompanyName = "Google",
                    CareerUrl = "https://careers.google.com/jobs/results/",
                    JobCardSelector = "//li[contains(@class, 'job-search-results__list-item')]",
                    TitleSelector = ".//h2[contains(@class, 'gc-card__title')]",
                    LocationSelector = ".//span[contains(@class, 'gc-job-tags__location')]",
                    UrlSelector = ".//a[contains(@class, 'gc-card__link')]",
                    UrlAttribute = "href",
                    DescriptionSelector = "//div[contains(@class, 'gc-job-detail__description')]",
                    DateSelector = null // Google doesn't typically show posting dates
                }
            },
            {
                "Amazon", new CompanyScrapingConfig
                {
                    CompanyName = "Amazon",
                    CareerUrl = "https://www.amazon.jobs/en/search",
                    JobCardSelector = "//div[contains(@class, 'job-tile')]",
                    TitleSelector = ".//h3[contains(@class, 'job-title')]",
                    LocationSelector = ".//p[contains(@class, 'location')]",
                    UrlSelector = ".//a[contains(@class, 'job-link')]",
                    UrlAttribute = "href",
                    DescriptionSelector = "//div[contains(@class, 'job-description')]",
                    DateSelector = ".//span[contains(@class, 'posting-date')]"
                }
            }
        };
    }
    
    public override async Task<IEnumerable<Job>> ScrapeJobsAsync(IEnumerable<string> keywords, int maxResults = 100)
    {
        var jobs = new List<Job>();
        
        // For each company in our configuration
        foreach (var companyConfig in _companyConfigs.Values)
        {
            try
            {
                _logger.LogInformation($"Scraping jobs from {companyConfig.CompanyName} career page");
                
                // For each keyword, search on the company's career page
                foreach (var keyword in keywords)
                {
                    try
                    {
                        // Construct search URL - this may need to be customized per company
                        var searchUrl = $"{companyConfig.CareerUrl}?q={Uri.EscapeDataString(keyword)}";
                        
                        var response = await _httpClient.GetAsync(searchUrl);
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning($"Failed to get {companyConfig.CompanyName} search results for {keyword}. Status code: {response.StatusCode}");
                            continue;
                        }
                        
                        var content = await response.Content.ReadAsStringAsync();
                        var jobsFromSearch = ParseCompanySearchResults(content, companyConfig);
                        
                        // Limit results per keyword and company
                        var keywordCompanyMaxResults = maxResults / (_companyConfigs.Count * keywords.Count());
                        foreach (var job in jobsFromSearch.Take(keywordCompanyMaxResults))
                        {
                            // Get detailed job info if we have the URL
                            if (!string.IsNullOrEmpty(job.JobUrl))
                            {
                                try
                                {
                                    var detailedJob = await ScrapeJobByUrlAsync(job.JobUrl, companyConfig);
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
                        HandleScrapingException(ex, $"Error scraping {companyConfig.CompanyName} jobs for keyword {keyword}");
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
                HandleScrapingException(ex, $"Error scraping {companyConfig.CompanyName} career page");
            }
        }
        
        return jobs;
    }
    
    public override async Task<Job?> ScrapeJobByUrlAsync(string jobUrl)
    {
        // Determine which company this URL belongs to
        var companyConfig = _companyConfigs.Values.FirstOrDefault(c => jobUrl.Contains(c.CompanyName, StringComparison.OrdinalIgnoreCase));
        
        if (companyConfig == null)
        {
            _logger.LogWarning($"No company configuration found for URL: {jobUrl}");
            return null;
        }
        
        return await ScrapeJobByUrlAsync(jobUrl, companyConfig);
    }
    
    private async Task<Job?> ScrapeJobByUrlAsync(string jobUrl, CompanyScrapingConfig companyConfig)
    {
        try
        {
            _logger.LogInformation($"Scraping {companyConfig.CompanyName} job details from: {jobUrl}");
            
            var response = await _httpClient.GetAsync(jobUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to get {companyConfig.CompanyName} job details from {jobUrl}. Status code: {response.StatusCode}");
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            return ParseCompanyJobDetails(content, jobUrl, companyConfig);
        }
        catch (Exception ex)
        {
            HandleScrapingException(ex, $"Error scraping job details from {jobUrl}");
            return null;
        }
    }
    
    private IEnumerable<Job> ParseCompanySearchResults(string html, CompanyScrapingConfig config)
    {
        var jobs = new List<Job>();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        // Use the company-specific job card selector
        var jobCards = doc.DocumentNode.SelectNodes(config.JobCardSelector);
        
        if (jobCards == null)
        {
            _logger.LogWarning($"No job cards found in {config.CompanyName} search results");
            return jobs;
        }
        
        foreach (var card in jobCards)
        {
            try
            {
                var job = new Job
                {
                    Id = Guid.NewGuid(),
                    SourcePlatform = $"{SourceName} - {config.CompanyName}",
                    CompanyName = config.CompanyName
                };
                
                // Extract job title using company-specific selector
                var titleNode = card.SelectSingleNode(config.TitleSelector);
                job.Title = titleNode?.InnerText.Trim() ?? "Unknown Title";
                
                // Extract job URL using company-specific selector
                var urlNode = card.SelectSingleNode(config.UrlSelector);
                if (urlNode != null)
                {
                    job.JobUrl = urlNode.GetAttributeValue(config.UrlAttribute, "");
                    
                    // Ensure URL is absolute
                    if (!string.IsNullOrEmpty(job.JobUrl) && !job.JobUrl.StartsWith("http"))
                    {
                        // Extract domain from career URL
                        var uri = new Uri(config.CareerUrl);
                        var domain = $"{uri.Scheme}://{uri.Host}";
                        
                        if (job.JobUrl.StartsWith("/"))
                        {
                            job.JobUrl = $"{domain}{job.JobUrl}";
                        }
                        else
                        {
                            job.JobUrl = $"{domain}/{job.JobUrl}";
                        }
                    }
                }
                
                // Extract location using company-specific selector
                var locationNode = card.SelectSingleNode(config.LocationSelector);
                job.Location = locationNode?.InnerText.Trim();
                
                // Check if remote based on location text
                job.IsRemote = job.Location?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true;
                
                // Extract posting date if available
                if (config.DateSelector != null)
                {
                    var dateNode = card.SelectSingleNode(config.DateSelector);
                    if (dateNode != null)
                    {
                        var dateText = dateNode.InnerText.Trim();
                        
                        // Try to parse various date formats
                        if (dateText.Contains("day", StringComparison.OrdinalIgnoreCase))
                        {
                            var match = Regex.Match(dateText, @"(\d+)");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int days))
                            {
                                job.PostingDate = DateTimeOffset.UtcNow.AddDays(-days);
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
                        else if (DateTimeOffset.TryParse(dateText, out var postDate))
                        {
                            job.PostingDate = postDate;
                        }
                    }
                }
                
                // Set scraping metadata
                job.ScrapedDate = DateTimeOffset.UtcNow;
                job.LastSeenDate = DateTimeOffset.UtcNow;
                job.IsActive = true;
                
                // Generate a job ID from source if we have enough info
                if (!string.IsNullOrEmpty(job.JobUrl) && !string.IsNullOrEmpty(job.Title))
                {
                    job.JobIdFromSource = GenerateJobIdFromSource(job.JobUrl, job.Title, job.CompanyName);
                }
                
                jobs.Add(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing {config.CompanyName} job card");
            }
        }
        
        return jobs;
    }
    
    private Job? ParseCompanyJobDetails(string html, string jobUrl, CompanyScrapingConfig config)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        try
        {
            var job = new Job
            {
                Id = Guid.NewGuid(),
                SourcePlatform = $"{SourceName} - {config.CompanyName}",
                CompanyName = config.CompanyName,
                JobUrl = jobUrl
            };
            
            // Extract job title - try to find a more specific title on the details page
            var titleNode = doc.DocumentNode.SelectSingleNode("//h1") ?? 
                           doc.DocumentNode.SelectSingleNode("//h2[contains(@class, 'job-title')]") ??
                           doc.DocumentNode.SelectSingleNode("//h2");
            
            job.Title = titleNode?.InnerText.Trim() ?? "Unknown Title";
            
            // Extract location - try to find a more specific location on the details page
            var locationNode = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'location')]") ??
                              doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'location')]");
            
            job.Location = locationNode?.InnerText.Trim();
            
            // Extract job description using company-specific selector
            var descriptionNode = doc.DocumentNode.SelectSingleNode(config.DescriptionSelector);
            job.Description = descriptionNode?.InnerHtml.Trim() ?? "";
            
            // Check if remote based on various indicators
            job.IsRemote = job.Location?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true || 
                          job.Title?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true ||
                          job.Description?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true;
            
            // Extract job type if it can be determined from the description
            if (job.Description?.Contains("Full-time", StringComparison.OrdinalIgnoreCase) == true)
            {
                job.JobType = "Full-time";
            }
            else if (job.Description?.Contains("Part-time", StringComparison.OrdinalIgnoreCase) == true)
            {
                job.JobType = "Part-time";
            }
            else if (job.Description?.Contains("Contract", StringComparison.OrdinalIgnoreCase) == true)
            {
                job.JobType = "Contract";
            }
            else if (job.Description?.Contains("Internship", StringComparison.OrdinalIgnoreCase) == true)
            {
                job.JobType = "Internship";
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
            _logger.LogError(ex, $"Error parsing {config.CompanyName} job details from {jobUrl}");
            return null;
        }
    }
    
    /// <summary>
    /// Configuration class for company-specific scraping settings
    /// </summary>
    private class CompanyScrapingConfig
    {
        public string CompanyName { get; set; } = "";
        public string CareerUrl { get; set; } = "";
        public string JobCardSelector { get; set; } = "";
        public string TitleSelector { get; set; } = "";
        public string LocationSelector { get; set; } = "";
        public string UrlSelector { get; set; } = "";
        public string UrlAttribute { get; set; } = "href";
        public string DescriptionSelector { get; set; } = "";
        public string? DateSelector { get; set; }
    }
}
