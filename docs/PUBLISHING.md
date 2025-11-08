# Publishing Guide for NHibernate.Extensions.AsSplitQuery

This guide provides step-by-step instructions for publishing the first version of the NuGet package following best practices.

## Prerequisites

1. **NuGet Account**: Create an account at [nuget.org](https://www.nuget.org/)
2. **API Key**: Generate an API key from your NuGet account settings
   - Go to https://www.nuget.org/account/apikeys
   - Click "Create"
   - Give it a name like "NHibernate.Extensions.AsSplitQuery"
   - Select appropriate expiration (365 days recommended)
   - Scope: Select "Push" and optionally limit to specific package patterns
3. **.NET SDK**: Ensure you have .NET 8 SDK installed
4. **Git**: Ensure repository is clean with all changes committed

## Pre-Publishing Checklist

Before publishing, verify:

- [ ] All tests pass: `dotnet test`
- [ ] Build succeeds: `dotnet build -c Release`
- [ ] README.md is up to date
- [ ] Version number is correct in `.csproj` (currently `1.0.0`)
- [ ] Package metadata is accurate (Authors, Description, Tags, etc.)
- [ ] License is specified (MIT)
- [ ] Repository URL is correct
- [ ] All code is committed to Git
- [ ] Git tag for version exists (optional but recommended)

## Publishing Steps

### Step 1: Clean and Build in Release Mode

```powershell
# Navigate to solution root
cd C:\Progetti.GIT\CArnaboldi\NHibernate.Extensions.AsSplitQuery

# Clean previous builds
dotnet clean -c Release

# Build in Release configuration
dotnet build -c Release
```

### Step 2: Run Tests

```powershell
# Run all tests to ensure everything works
dotnet test -c Release --no-build

# Expected output: All 26 tests should pass
```

### Step 3: Create NuGet Package

```powershell
# Navigate to project directory
cd src\NHibernate.Extensions.AsSplitQuery

# Create the package
dotnet pack -c Release --no-build -o ../../artifacts

# This creates:
# - NHibernate.Extensions.AsSplitQuery.1.0.0.nupkg (main package)
# - NHibernate.Extensions.AsSplitQuery.1.0.0.snupkg (symbols package for debugging)
```

### Step 4: Inspect the Package (Optional but Recommended)

```powershell
# Install NuGet Package Explorer (if not already installed)
# Download from: https://github.com/NuGetPackageExplorer/NuGetPackageExplorer

# Or use command line to inspect
dotnet nuget verify ../../artifacts/NHibernate.Extensions.AsSplitQuery.1.0.0.nupkg
```

**Verify the package contains:**
- ? All .dll files (net8.0)
- ? XML documentation file
- ? README.md
- ? LICENSE file (if included)
- ? Source link information
- ? Symbols (.snupkg)

### Step 5: Test Package Locally (Optional but Recommended)

```powershell
# Create a test project to validate the package
mkdir ../../../TestNuGetPackage
cd ../../../TestNuGetPackage
dotnet new console
dotnet add package NHibernate.Extensions.AsSplitQuery --source "C:\Progetti.GIT\CArnaboldi\NHibernate.Extensions.AsSplitQuery\artifacts"

# Verify it installs correctly and IntelliSense works
# Clean up test project when done
cd ..
rmdir TestNuGetPackage -Recurse -Force
```

### Step 6: Configure NuGet API Key (One-time Setup)

```powershell
# Store your API key securely (replace YOUR_API_KEY with actual key)
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org

# Set API key (do this in a secure terminal session)
dotnet nuget setapikey YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

**Security Best Practices:**
- Never commit API keys to source control
- Use API keys with limited scope and expiration
- Store keys in a secure password manager
- Rotate keys periodically

### Step 7: Publish to NuGet.org

```powershell
# Navigate back to artifacts directory
cd C:\Progetti.GIT\CArnaboldi\NHibernate.Extensions.AsSplitQuery\artifacts

# Push the main package (symbols will be pushed automatically)
dotnet nuget push NHibernate.Extensions.AsSplitQuery.1.0.0.nupkg --source https://api.nuget.org/v3/index.json

# Alternative: Push with explicit API key (if not configured)
# dotnet nuget push NHibernate.Extensions.AsSplitQuery.1.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

**Expected Output:**
```
Pushing NHibernate.Extensions.AsSplitQuery.1.0.0.nupkg to 'https://www.nuget.org/api/v2/package'...
  PUT https://www.nuget.org/api/v2/package/
  Created https://www.nuget.org/api/v2/package/ 2024ms
Your package was pushed.
```

### Step 8: Verify Publication

1. **Check NuGet.org Package Page**:
   - Visit: https://www.nuget.org/packages/NHibernate.Extensions.AsSplitQuery
   - Package may take 10-15 minutes to appear in search
   - Verify all metadata is correct

2. **Verify Package Contents**:
   - Click on package version
   - Check "Dependencies" tab (should show NHibernate 5.5.2)
   - Check "Frameworks" tab (should show net8.0)
   - Verify README renders correctly

3. **Test Installation**:
   ```powershell
   # In a new test project
   dotnet add package NHibernate.Extensions.AsSplitQuery
   ```

### Step 9: Create Git Tag (Recommended)

```powershell
cd C:\Progetti.GIT\CArnaboldi\NHibernate.Extensions.AsSplitQuery

# Create and push a version tag
git tag -a v1.0.0 -m "Release version 1.0.0 - Initial release with full split query support"
git push origin v1.0.0
```

### Step 10: Create GitHub Release (Optional)

1. Go to: https://github.com/CArnaboldi/NHibernate.Extensions.AsSplitQuery/releases
2. Click "Create a new release"
3. Select tag: `v1.0.0`
4. Release title: `v1.0.0 - Initial Release`
5. Description:
   ```markdown
   ## ?? Initial Release

   First stable release of NHibernate.Extensions.AsSplitQuery!

   ### ? Features
   - ? Prevents cartesian explosion in NHibernate LINQ queries
   - ? Works with all query methods: `ToList()`, `First()`, `Single()`, and their async variants
   - ? Supports nested collections with `FetchMany` and `ThenFetchMany`
   - ? 50-100x performance improvement for complex queries
   - ? Full transaction support
   - ? Thread-safe with concurrent reflection caching
   - ? Comprehensive test coverage (26 tests)

   ### ?? Installation
   ```bash
   dotnet add package NHibernate.Extensions.AsSplitQuery
   ```

   ### ?? Documentation
   See the [README](https://github.com/CArnaboldi/NHibernate.Extensions.AsSplitQuery/blob/main/README.md) for usage examples and full documentation.

   ### ?? Links
   - [NuGet Package](https://www.nuget.org/packages/NHibernate.Extensions.AsSplitQuery)
   - [GitHub Repository](https://github.com/CArnaboldi/NHibernate.Extensions.AsSplitQuery)
   ```
6. Attach artifacts (optional):
   - `NHibernate.Extensions.AsSplitQuery.1.0.0.nupkg`
   - `NHibernate.Extensions.AsSplitQuery.1.0.0.snupkg`

## Post-Publishing Tasks

### Update Badges (if not already done)

Update README.md badges to reflect published status:
```markdown
[![NuGet](https://img.shields.io/nuget/v/NHibernate.Extensions.AsSplitQuery.svg)](https://www.nuget.org/packages/NHibernate.Extensions.AsSplitQuery/)
[![Downloads](https://img.shields.io/nuget/dt/NHibernate.Extensions.AsSplitQuery.svg)](https://www.nuget.org/packages/NHibernate.Extensions.AsSplitQuery/)
```

### Announce the Release

Consider announcing on:
- Twitter/X with hashtags: #NHibernate #dotnet #opensource
- Reddit: r/dotnet, r/csharp
- NHibernate community forums
- LinkedIn
- Personal blog (if applicable)

### Monitor Initial Feedback

- Watch GitHub issues for bug reports
- Monitor NuGet package stats
- Respond to questions promptly

## Troubleshooting

### Error: "Package already exists"
- Version 1.0.0 cannot be republished
- Increment version in .csproj (e.g., 1.0.1) and rebuild

### Error: "API key invalid"
- Verify API key is correct
- Check key hasn't expired
- Ensure key has "Push" permissions

### Package Not Appearing in Search
- Wait 10-15 minutes for indexing
- Clear NuGet cache: `dotnet nuget locals all --clear`
- Try exact package name search first

### Symbols Not Working
- Ensure SourceLink is configured (already done)
- Verify .snupkg was uploaded
- Enable "Just My Code" in Visual Studio debugger settings

## Future Releases

For subsequent releases:

1. Update version in `.csproj`:
   - Patch: 1.0.1 (bug fixes)
   - Minor: 1.1.0 (new features, backward compatible)
   - Major: 2.0.0 (breaking changes)

2. Update `PackageReleaseNotes` in `.csproj`

3. Update README.md with new features

4. Follow steps 1-10 above with new version number

## Best Practices for Version 1.0.0

? **You're following these best practices:**
- Semantic versioning (SemVer 2.0)
- Comprehensive XML documentation
- Symbol package for debugging
- Source Link enabled
- README included in package
- MIT license
- Proper package metadata
- Multi-targeting ready (net8.0)
- Comprehensive test coverage

? **Package Quality Indicators:**
- Clear, descriptive package name
- Professional description
- Relevant tags for discoverability
- Links to repository and documentation
- Copyright information
- Release notes

## Support and Maintenance

After publishing:
- Monitor GitHub issues
- Respond to user questions
- Consider creating GitHub Discussions for Q&A
- Keep dependencies up to date (especially NHibernate)
- Add more examples based on user feedback

---

**Congratulations on your first NuGet package release! ??**

Remember: Version 1.0.0 signals production-ready software. Your comprehensive testing and documentation support this!
