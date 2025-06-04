using JobAggregator.Data.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JobAggregator.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Job> Jobs { get; set; }
    public DbSet<Keyword> Keywords { get; set; }
    public DbSet<SavedJob> SavedJobs { get; set; }
    public DbSet<Resume> Resumes { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<ScrapeSource> ScrapeSources { get; set; }
    public DbSet<ScrapeLog> ScrapeLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure Job entity
        builder.Entity<Job>(entity =>
        {
            entity.HasIndex(j => j.JobUrl).IsUnique();
            entity.HasIndex(j => j.Title);
            entity.HasIndex(j => j.CompanyName);
            entity.HasIndex(j => j.Location);
            entity.HasIndex(j => j.PostingDate);
            entity.HasIndex(j => j.ScrapedDate);
            entity.HasIndex(j => j.SourcePlatform);
            entity.HasIndex(j => j.JobType);
            entity.HasIndex(j => j.IsRemote);
        });

        // Configure Keyword entity
        builder.Entity<Keyword>(entity =>
        {
            entity.HasIndex(k => new { k.UserId, k.KeywordText }).IsUnique();
            entity.HasIndex(k => k.KeywordText);
        });

        // Configure SavedJob entity
        builder.Entity<SavedJob>(entity =>
        {
            entity.HasIndex(sj => new { sj.UserId, sj.JobId }).IsUnique();
        });

        // Configure Resume entity
        builder.Entity<Resume>(entity =>
        {
            entity.HasIndex(r => r.UserId).IsUnique(); // One resume per user
        });

        // Configure Notification entity
        builder.Entity<Notification>(entity =>
        {
            entity.HasIndex(n => n.UserId);
            entity.HasIndex(n => n.JobId);
            entity.HasIndex(n => n.SentDate);
            entity.HasIndex(n => n.IsRead);
        });

        // Configure ScrapeSource entity
        builder.Entity<ScrapeSource>(entity =>
        {
            entity.HasIndex(ss => ss.Name).IsUnique();
        });

        // Configure ScrapeLog entity
        builder.Entity<ScrapeLog>(entity =>
        {
            entity.HasIndex(sl => sl.ScrapeSourceId);
            entity.HasIndex(sl => sl.StartTime);
            entity.HasIndex(sl => sl.Status);
        });

        // Configure relationships (many handled by convention/attributes, add specifics if needed)
        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Resume)
            .WithOne(r => r.User)
            .HasForeignKey<Resume>(r => r.UserId);

        builder.Entity<ApplicationUser>()
            .HasMany(u => u.Keywords)
            .WithOne(k => k.User)
            .HasForeignKey(k => k.UserId);

        builder.Entity<ApplicationUser>()
            .HasMany(u => u.SavedJobs)
            .WithOne(sj => sj.User)
            .HasForeignKey(sj => sj.UserId);

        builder.Entity<Job>()
            .HasMany(j => j.SavedByUsers)
            .WithOne(sj => sj.Job)
            .HasForeignKey(sj => sj.JobId);

        builder.Entity<ApplicationUser>()
            .HasMany(u => u.Notifications)
            .WithOne(n => n.User)
            .HasForeignKey(n => n.UserId);

        builder.Entity<Job>()
            .HasMany(j => j.Notifications)
            .WithOne(n => n.Job)
            .HasForeignKey(n => n.JobId);

        builder.Entity<ScrapeSource>()
            .HasMany(ss => ss.ScrapeLogs)
            .WithOne(sl => sl.ScrapeSource)
            .HasForeignKey(sl => sl.ScrapeSourceId);
    }
}

