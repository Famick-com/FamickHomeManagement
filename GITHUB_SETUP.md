# GitHub Repository Setup Guide

This guide provides step-by-step instructions for creating the three GitHub repositories and pushing your local repositories.

## Prerequisites

- GitHub account with organization access (recommended: create "famick" organization)
- GitHub CLI (`gh`) installed, or use GitHub web interface
- Git configured with your credentials

## Repository Overview

| Repository | Visibility | License | Purpose |
|------------|-----------|---------|---------|
| `homemanagement-shared` | Public | Elastic License 2.0 | Shared NuGet packages for both versions |
| `homemanagement` | Public | Elastic License 2.0 | Self-hosted version (source-available) |
| `homemanagement-cloud` | **Private** | Proprietary | Multi-tenant cloud SaaS version |

---

## Option 1: Using GitHub CLI (Recommended)

### 1. Install GitHub CLI (if not already installed)

```bash
# macOS
brew install gh

# Windows (via winget)
winget install --id GitHub.cli

# Linux
# See: https://github.com/cli/cli/blob/trunk/docs/install_linux.md
```

### 2. Authenticate with GitHub

```bash
gh auth login
```

### 3. Create Shared Repository (Public)

```bash
# Navigate to shared repository
cd /Users/miketherien/Projects/git_projects/Famick/homemanagement-shared

# Create GitHub repository (public)
gh repo create famick/homemanagement-shared \
  --public \
  --source=. \
  --description="Shared libraries and core business logic for Famick HomeManagement" \
  --homepage="https://github.com/famick/homemanagement"

# Push to GitHub
git branch -M main
git remote add origin https://github.com/famick/homemanagement-shared.git
git push -u origin main
```

### 4. Create Self-Hosted Repository (Public)

```bash
# Navigate to self-hosted repository
cd /Users/miketherien/Projects/git_projects/Famick/homemanagement

# Create GitHub repository (public)
gh repo create famick/homemanagement \
  --public \
  --source=. \
  --description="🏠 Household management system for inventory, recipes, chores, and tasks" \
  --homepage="https://famick.com"

# Push to GitHub
git branch -M main
git remote add origin https://github.com/famick/homemanagement.git
git push -u origin main
```

### 5. Create Cloud Repository (Private)

```bash
# Navigate to cloud repository
cd /Users/miketherien/Projects/git_projects/Famick/homemanagement-cloud

# Create GitHub repository (PRIVATE)
gh repo create famick/homemanagement-cloud \
  --private \
  --source=. \
  --description="Multi-tenant cloud SaaS version of Famick HomeManagement (Proprietary)" \
  --internal  # Use --internal if you have a GitHub organization

# Push to GitHub
git branch -M main
git remote add origin https://github.com/famick/homemanagement-cloud.git
git push -u origin main
```

---

## Option 2: Using GitHub Web Interface

### 1. Create Shared Repository

1. Go to https://github.com/new
2. **Owner**: Select your organization or personal account
3. **Repository name**: `homemanagement-shared`
4. **Description**: "Shared libraries and core business logic for Famick HomeManagement"
5. **Visibility**: ✅ Public
6. **Initialize**: ❌ Do NOT initialize (we have existing code)
7. Click **Create repository**

Then push your local repository:

```bash
cd /Users/miketherien/Projects/git_projects/Famick/homemanagement-shared
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/homemanagement-shared.git
git push -u origin main
```

### 2. Create Self-Hosted Repository

1. Go to https://github.com/new
2. **Owner**: Select your organization or personal account
3. **Repository name**: `homemanagement`
4. **Description**: "🏠 Household management system for inventory, recipes, chores, and tasks"
5. **Visibility**: ✅ Public
6. **Initialize**: ❌ Do NOT initialize
7. Click **Create repository**

Then push your local repository:

```bash
cd /Users/miketherien/Projects/git_projects/Famick/homemanagement
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/homemanagement.git
git push -u origin main
```

### 3. Create Cloud Repository

1. Go to https://github.com/new
2. **Owner**: Select your organization or personal account
3. **Repository name**: `homemanagement-cloud`
4. **Description**: "Multi-tenant cloud SaaS version of Famick HomeManagement (Proprietary)"
5. **Visibility**: ✅ **Private** ⚠️
6. **Initialize**: ❌ Do NOT initialize
7. Click **Create repository**

Then push your local repository:

```bash
cd /Users/miketherien/Projects/git_projects/Famick/homemanagement-cloud
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/homemanagement-cloud.git
git push -u origin main
```

---

## Quick Start - All Three Repositories

```bash
# Set base directory
FAMICK_DIR="/Users/miketherien/Projects/git_projects/Famick"

# 1. Shared Repository (Public)
cd "${FAMICK_DIR}/homemanagement-shared"
gh repo create famick/homemanagement-shared --public --source=. \
  --description="Shared libraries and core business logic for Famick HomeManagement"
git push -u origin main

# 2. Self-Hosted Repository (Public)
cd "${FAMICK_DIR}/homemanagement"
gh repo create famick/homemanagement --public --source=. \
  --description="🏠 Household management system for inventory, recipes, chores, and tasks"
git push -u origin main

# 3. Cloud Repository (PRIVATE)
cd "${FAMICK_DIR}/homemanagement-cloud"
gh repo create famick/homemanagement-cloud --private --source=. \
  --description="Multi-tenant cloud SaaS version of Famick HomeManagement (Proprietary)"
git push -u origin main
```

---

## Post-Creation Tasks

### 1. Configure Repository Settings

#### Shared Repository (`homemanagement-shared`)

```bash
# Set repository topics
gh repo edit famick/homemanagement-shared --add-topic "dotnet,csharp,nuget,household-management,elastic-license"

# Add repository description
gh repo edit famick/homemanagement-shared --description "Shared libraries and core business logic for Famick HomeManagement - supporting both self-hosted and cloud versions"
```

#### Self-Hosted Repository (`homemanagement`)

```bash
# Set repository topics
gh repo edit famick/homemanagement --add-topic "dotnet,csharp,docker,household-management,inventory,recipes,source-available,elastic-license"

# Enable Issues, Discussions, and Wiki
gh repo edit famick/homemanagement --enable-issues --enable-wiki --enable-discussions

# Add repository description
gh repo edit famick/homemanagement --description "🏠 Household management system for inventory tracking, recipe management, chores, and task organization. Self-hosted with Docker."
```

#### Cloud Repository (`homemanagement-cloud`)

```bash
# Set repository topics
gh repo edit famick/homemanagement-cloud --add-topic "dotnet,csharp,saas,multi-tenant,cloud,proprietary"

# Ensure it's private
gh repo edit famick/homemanagement-cloud --visibility private

# Disable issues/wiki (use internal tools)
gh repo edit famick/homemanagement-cloud --enable-issues=false --enable-wiki=false
```

### 2. Set Up Branch Protection (Recommended)

```bash
# Protect main branch for shared repository
gh api repos/famick/homemanagement-shared/branches/main/protection \
  --method PUT \
  --field required_status_checks='{"strict":true,"contexts":["build","test"]}' \
  --field enforce_admins=true \
  --field required_pull_request_reviews='{"required_approving_review_count":1}'

# Protect main branch for self-hosted repository
gh api repos/famick/homemanagement/branches/main/protection \
  --method PUT \
  --field required_status_checks='{"strict":true,"contexts":["build","test"]}' \
  --field enforce_admins=true \
  --field required_pull_request_reviews='{"required_approving_review_count":1}'

# Protect main branch for cloud repository (stricter)
gh api repos/famick/homemanagement-cloud/branches/main/protection \
  --method PUT \
  --field required_status_checks='{"strict":true,"contexts":["build","test","security-scan"]}' \
  --field enforce_admins=true \
  --field required_pull_request_reviews='{"required_approving_review_count":2}'
```

### 3. Add GitHub Actions Workflows

#### Shared Repository - NuGet Publishing

Create `.github/workflows/publish-nuget.yml` in `homemanagement-shared`:

```yaml
name: Publish NuGet Packages

on:
  push:
    tags:
      - 'v*.*.*'

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Test
        run: dotnet test --configuration Release --no-build

      - name: Pack
        run: dotnet pack --configuration Release --no-build --output packages

      - name: Publish to GitHub Packages
        run: dotnet nuget push "packages/*.nupkg" --source "https://nuget.pkg.github.com/famick/index.json" --api-key ${{ secrets.GITHUB_TOKEN }}
```

#### Self-Hosted Repository - Docker Build

Create `.github/workflows/docker-build.yml` in `homemanagement`:

```yaml
name: Build and Push Docker Image

on:
  push:
    branches: [ main ]
    tags:
      - 'v*.*.*'

jobs:
  docker:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Docker meta
        id: meta
        uses: docker/metadata-action@v4
        with:
          images: famick/homemanagement
          tags: |
            type=ref,event=branch
            type=semver,pattern={{version}}
            type=semver,pattern={{major}}.{{minor}}

      - name: Login to Docker Hub
        uses: docker/login-action@v2
        with:
          username: ${{ secrets.DOCKERHUB_USERNAME }}
          password: ${{ secrets.DOCKERHUB_TOKEN }}

      - name: Build and push
        uses: docker/build-push-action@v4
        with:
          context: .
          file: ./docker/Dockerfile
          push: true
          tags: ${{ steps.meta.outputs.tags }}
```

### 4. Add README Badges

Add these badges to the top of each repository's README:

#### Shared Repository

```markdown
[![NuGet](https://img.shields.io/nuget/v/Famick.HomeManagement.Domain.svg)](https://www.nuget.org/packages/Famick.HomeManagement.Domain/)
[![License: ELv2](https://img.shields.io/badge/License-ELv2-blue.svg)](https://www.elastic.co/licensing/elastic-license)
[![Build](https://github.com/famick/homemanagement-shared/workflows/Build/badge.svg)](https://github.com/famick/homemanagement-shared/actions)
```

#### Self-Hosted Repository

```markdown
[![Docker](https://img.shields.io/docker/v/famick/homemanagement?sort=semver)](https://hub.docker.com/r/famick/homemanagement)
[![License: ELv2](https://img.shields.io/badge/License-ELv2-blue.svg)](https://www.elastic.co/licensing/elastic-license)
[![Build](https://github.com/famick/homemanagement/workflows/Build/badge.svg)](https://github.com/famick/homemanagement/actions)
```

---

## Verification

After pushing all repositories, verify:

1. **Shared Repository**:
   - ✅ Public visibility
   - ✅ Elastic License 2.0 visible
   - ✅ 238 files committed
   - ✅ Topics set correctly

2. **Self-Hosted Repository**:
   - ✅ Public visibility
   - ✅ Elastic License 2.0 visible
   - ✅ Docker files present
   - ✅ README with quick start guide

3. **Cloud Repository**:
   - ✅ **Private visibility** (very important!)
   - ✅ Proprietary license visible
   - ✅ Cloud-specific files present
   - ✅ No public access

---

## Repository Locations

Your local repositories are located at:

- **Shared**: `/Users/miketherien/Projects/git_projects/Famick/homemanagement-shared`
- **Self-Hosted**: `/Users/miketherien/Projects/git_projects/Famick/homemanagement`
- **Cloud**: `/Users/miketherien/Projects/git_projects/Famick/homemanagement-cloud`

## Next Steps

1. **Publish NuGet Packages**: Tag shared repository with `v1.0.0` to trigger NuGet publishing
2. **Build Docker Image**: Tag self-hosted repository with `v1.0.0` to trigger Docker build
3. **Configure GitHub Packages**: Set up authentication for NuGet package consumption
4. **Update Documentation**: Create detailed docs in each repository
5. **Set Up CI/CD**: Configure automated testing and deployment
6. **Add Contributors**: Invite team members to appropriate repositories

---

## Troubleshooting

### "Repository already exists" error

If you get this error, the repository name is taken. Either:
- Use a different name
- Delete the existing repository (if you own it)
- Use your username instead of an organization

### Authentication issues

```bash
# Re-authenticate with GitHub CLI
gh auth logout
gh auth login

# Or use SSH instead of HTTPS
git remote set-url origin git@github.com:famick/homemanagement-shared.git
```

### Permission denied errors

Make sure you have:
- Write access to the organization/account
- Generated a Personal Access Token (PAT) with `repo` scope
- Configured Git credentials correctly

---

## Repository URLs (After Creation)

- **Shared**: https://github.com/famick/homemanagement-shared
- **Self-Hosted**: https://github.com/famick/homemanagement
- **Cloud** (Private): https://github.com/famick/homemanagement-cloud

🎉 **All three repositories are now ready!**
