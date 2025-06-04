# Job Aggregator - Final Project Report

## Project Overview

The Job Aggregator is a comprehensive web application designed specifically for Computer Science students to aggregate, filter, and track job and internship opportunities across multiple platforms. The application provides a personalized dashboard where users can view job listings tailored to their interests and qualifications.

## Key Features Implemented

### 1. Real-Time Job Aggregation
- Implemented modular scrapers for multiple platforms:
  - LinkedIn
  - Naukri
  - Internshala
  - Official company websites (Microsoft, Google, Amazon)
- Extracted comprehensive job details:
  - Job title
  - Company name
  - Location
  - Job description
  - Direct application link
  - Posting date

### 2. Customizable Job Feed
- Keyword-based job tracking
- Advanced filtering options:
  - Date posted
  - Company
  - Job type (full-time/internship)
  - Location
  - Remote/on-site filter

### 3. Notifications & Alerts
- Background service for periodic job updates
- Web notifications for new matching jobs
- Notification management interface

### 4. Personal Dashboard
- Resume upload and storage (PDF)
- Automatic keyword extraction from resume
- Saved jobs with notes functionality
- Job statistics and overview

### 5. Technical Implementation
- **Backend**: ASP.NET Core 8 with Razor Pages
- **Database**: SQLite (for easy deployment and student use)
- **ORM**: Entity Framework Core
- **Job Scraping**: Custom HTTP clients with HtmlAgilityPack
- **Background Services**: IHostedService for periodic job scraping
- **Authentication**: ASP.NET Core Identity

## Deployment Information

The application is configured for deployment on Vercel, which offers a free tier suitable for student projects. The deployment configuration includes:

- Vercel configuration file (`vercel.json`)
- GitHub Actions workflow for CI/CD
- Detailed deployment guide

## Project Structure

```
JobAggregator/
├── src/
│   ├── JobAggregator.Api/         # API endpoints
│   ├── JobAggregator.Data/        # Data models and EF Core context
│   ├── JobAggregator.Web/         # Frontend (Razor Pages)
│   └── JobAggregator.Worker/      # Background services
├── .github/workflows/             # CI/CD configuration
├── vercel.json                    # Vercel deployment config
├── deployment_guide.md            # Deployment instructions
└── testing_report.md              # Testing documentation
```

## How to Use the Application

1. **Registration/Login**: Create an account or log in
2. **Add Keywords**: Add job titles or skills you're interested in
3. **Upload Resume**: Upload your resume for keyword extraction
4. **Browse Jobs**: View personalized job listings based on your keywords
5. **Save Jobs**: Save interesting opportunities for later
6. **Check Notifications**: Receive alerts about new matching jobs

## Deployment Instructions

Please refer to the `deployment_guide.md` file for detailed instructions on deploying the application to Vercel for free. The guide includes:

1. Setting up a GitHub repository
2. Connecting to Vercel
3. Configuring environment variables
4. Understanding free tier limitations
5. Updating the application

## Future Enhancements

1. Email notifications for new job alerts
2. Advanced resume parsing with machine learning
3. Job application tracking functionality
4. Mobile app integration
5. Additional job sources and platforms

## Conclusion

The Job Aggregator application successfully meets all the requirements specified in the initial project brief. It provides a comprehensive solution for Computer Science students to discover and track relevant job opportunities across multiple platforms in one centralized location.

The application is designed with scalability in mind, allowing for easy addition of new job sources and features in the future. The free deployment option ensures that students can use the platform without any cost concerns.

Thank you for the opportunity to work on this project. I hope it serves as a valuable tool in your job search journey!
