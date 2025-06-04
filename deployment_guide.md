# Deployment Guide for Job Aggregator Application

This guide provides instructions for deploying the Job Aggregator application to Vercel, a free hosting platform that's perfect for student projects.

## Prerequisites

1. A GitHub account
2. A Vercel account (free tier) - Sign up at https://vercel.com using your GitHub account

## Deployment Steps

### 1. Push the Code to GitHub

1. Create a new GitHub repository
2. Push the Job Aggregator code to your repository:
```bash
git init
git add .
git commit -m "Initial commit"
git remote add origin https://github.com/yourusername/job-aggregator.git
git push -u origin main
```

### 2. Deploy to Vercel

1. Log in to your Vercel account
2. Click "New Project"
3. Import your GitHub repository
4. Configure the project:
   - Framework Preset: Other
   - Root Directory: ./
   - Build Command: Leave default
   - Output Directory: Leave default
5. Click "Deploy"

Vercel will automatically detect the .NET project and deploy it using the configuration in `vercel.json`.

### 3. Configure Environment Variables

After deployment, set up the following environment variables in the Vercel project settings:

1. Go to your project in Vercel dashboard
2. Navigate to "Settings" > "Environment Variables"
3. Add the following variables:
   - `ConnectionStrings__DefaultConnection`: The SQLite connection string (will use a file-based database in Vercel's ephemeral storage)
   - `DOTNET_ENVIRONMENT`: `Production`

### 4. Database Considerations

Since Vercel uses ephemeral storage, the SQLite database will be reset on each deployment. For a student project, this might be acceptable for demonstration purposes.

For a more persistent database, consider:
1. Using a free tier of a cloud database service like ElephantSQL (PostgreSQL)
2. Modifying the connection string to point to your cloud database

### 5. Background Services

Vercel doesn't support long-running background services. For the job scraping functionality:

1. Convert the background service to a scheduled function that runs periodically
2. Use Vercel Cron Jobs (limited in free tier) or a free service like Cron-job.org to trigger an API endpoint that runs the job scraping

### 6. Limitations of Free Tier

1. Limited compute resources
2. No persistent file storage (uploaded resumes will be lost on redeployment)
3. Limited execution time for functions
4. Sleep mode for inactive projects

### 7. Accessing Your Deployed Application

After successful deployment, your application will be available at:
`https://job-aggregator-yourusername.vercel.app`

### 8. Updating Your Application

To update your application:
1. Make changes to your code locally
2. Commit and push to GitHub
3. Vercel will automatically redeploy your application

## Alternative Free Hosting Options

If Vercel doesn't meet your needs, consider these alternatives:

1. **Render** - Offers free web services with PostgreSQL database
2. **Railway** - Provides limited free tier with PostgreSQL support
3. **Fly.io** - Offers a generous free tier with persistent storage

Each platform has different strengths and limitations, so choose based on your specific requirements.

## Local Development

For continued development:
1. Clone your GitHub repository
2. Install .NET SDK 8.0
3. Run `dotnet restore` to restore packages
4. Run `dotnet run --project src/JobAggregator.Web/JobAggregator.Web.csproj` to start the application locally
