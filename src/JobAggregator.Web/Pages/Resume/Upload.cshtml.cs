using JobAggregator.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace JobAggregator.Web.Pages.Resume;

[Authorize]
public class UploadModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UploadModel> _logger;
    private readonly IWebHostEnvironment _environment;

    public UploadModel(
        ApplicationDbContext dbContext,
        ILogger<UploadModel> logger,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _logger = logger;
        _environment = environment;
    }

    [TempData]
    public string StatusMessage { get; set; } = string.Empty;

    [TempData]
    public bool IsSuccess { get; set; }

    public Models.Resume? ExistingResume { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        // Check if user already has a resume
        ExistingResume = await _dbContext.Resumes
            .FirstOrDefaultAsync(r => r.UserId == userId);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(IFormFile resumeFile, bool extractKeywords = true)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        if (resumeFile == null || resumeFile.Length == 0)
        {
            StatusMessage = "Error: Please select a file to upload.";
            IsSuccess = false;
            return RedirectToPage();
        }

        // Validate file size (5MB max)
        if (resumeFile.Length > 5 * 1024 * 1024)
        {
            StatusMessage = "Error: File size exceeds the 5MB limit.";
            IsSuccess = false;
            return RedirectToPage();
        }

        // Validate file type (PDF only)
        if (!resumeFile.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Error: Only PDF files are accepted.";
            IsSuccess = false;
            return RedirectToPage();
        }

        try
        {
            // Create directory if it doesn't exist
            var uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads", "Resumes");
            Directory.CreateDirectory(uploadsFolder);

            // Generate a unique filename
            var uniqueFileName = $"{userId}_{Guid.NewGuid()}.pdf";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Save the file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await resumeFile.CopyToAsync(fileStream);
            }

            // Check if user already has a resume
            var existingResume = await _dbContext.Resumes
                .FirstOrDefaultAsync(r => r.UserId == userId);

            if (existingResume != null)
            {
                // Delete old file if it exists
                if (!string.IsNullOrEmpty(existingResume.FilePath) && System.IO.File.Exists(existingResume.FilePath))
                {
                    System.IO.File.Delete(existingResume.FilePath);
                }

                // Update existing resume
                existingResume.FileName = resumeFile.FileName;
                existingResume.FilePath = filePath;
                existingResume.FileSize = resumeFile.Length;
                existingResume.ContentType = resumeFile.ContentType;
                existingResume.UploadDate = DateTimeOffset.UtcNow;
            }
            else
            {
                // Create new resume
                var resume = new Models.Resume
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    FileName = resumeFile.FileName,
                    FilePath = filePath,
                    FileSize = resumeFile.Length,
                    ContentType = resumeFile.ContentType,
                    UploadDate = DateTimeOffset.UtcNow
                };

                await _dbContext.Resumes.AddAsync(resume);
            }

            await _dbContext.SaveChangesAsync();

            // Extract keywords if requested
            if (extractKeywords)
            {
                var keywords = ExtractKeywordsFromPdf(filePath);
                await SaveExtractedKeywords(userId, keywords);
            }

            StatusMessage = "Resume uploaded successfully!";
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading resume");
            StatusMessage = $"Error: {ex.Message}";
            IsSuccess = false;
        }

        return RedirectToPage();
    }

    private List<string> ExtractKeywordsFromPdf(string filePath)
    {
        var extractedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            // Common tech skills and job titles to look for
            var techSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Python", "JavaScript", "TypeScript", "Java", "C#", ".NET", "React", "Angular", "Vue", 
                "Node.js", "SQL", "NoSQL", "MongoDB", "PostgreSQL", "MySQL", "AWS", "Azure", "GCP",
                "Docker", "Kubernetes", "CI/CD", "Git", "REST API", "GraphQL", "Machine Learning",
                "Data Science", "Artificial Intelligence", "DevOps", "Agile", "Scrum"
            };
            
            var jobTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Software Engineer", "Software Developer", "Frontend Developer", "Backend Developer",
                "Full Stack Developer", "Data Scientist", "Machine Learning Engineer", "DevOps Engineer",
                "Cloud Engineer", "Database Administrator", "System Administrator", "QA Engineer",
                "Test Engineer", "Product Manager", "Project Manager", "Scrum Master",
                "UI/UX Designer", "Mobile Developer", "Android Developer", "iOS Developer",
                "Web Developer", "Network Engineer", "Security Engineer", "Data Engineer",
                "Business Analyst", "Solutions Architect", "Technical Lead", "Engineering Manager"
            };

            using (var document = PdfDocument.Open(filePath))
            {
                var allText = new StringBuilder();
                
                // Extract text from all pages
                foreach (var page in document.GetPages())
                {
                    allText.Append(page.Text);
                }
                
                var text = allText.ToString();
                
                // Look for tech skills
                foreach (var skill in techSkills)
                {
                    if (Regex.IsMatch(text, $@"\b{Regex.Escape(skill)}\b", RegexOptions.IgnoreCase))
                    {
                        extractedKeywords.Add(skill);
                    }
                }
                
                // Look for job titles
                foreach (var title in jobTitles)
                {
                    if (Regex.IsMatch(text, $@"\b{Regex.Escape(title)}\b", RegexOptions.IgnoreCase))
                    {
                        extractedKeywords.Add(title);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting keywords from PDF");
        }
        
        return extractedKeywords.Take(10).ToList(); // Limit to 10 keywords
    }

    private async Task SaveExtractedKeywords(string userId, List<string> keywords)
    {
        try
        {
            // Get existing keywords for this user
            var existingKeywords = await _dbContext.Keywords
                .Where(k => k.UserId == userId)
                .Select(k => k.KeywordText.ToLower())
                .ToListAsync();
            
            // Only add keywords that don't already exist
            foreach (var keyword in keywords)
            {
                if (!existingKeywords.Contains(keyword.ToLower()))
                {
                    await _dbContext.Keywords.AddAsync(new Keyword
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        KeywordText = keyword,
                        CreatedDate = DateTimeOffset.UtcNow
                    });
                }
            }
            
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving extracted keywords");
        }
    }
}
