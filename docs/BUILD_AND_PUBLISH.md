# Building and Publishing Guide

## Prerequisites

- .NET SDK 8.0 or higher
- NuGet account for publishing

## Building the Library

### 1. Restore Dependencies

```bash
cd NHibernate.Extensions.AsSplitQuery
dotnet restore
```

### 2. Build

```bash
dotnet build --configuration Release
```

### 3. Run Tests

```bash
dotnet test --configuration Release
```

## Creating the NuGet Package

### 1. Pack the Library

```bash
cd src/NHibernate.Extensions.AsSplitQuery
dotnet pack --configuration Release --output ../../nupkg
```

This will create:
- `NHibernate.Extensions.AsSplitQuery.1.0.0.nupkg` - The main package
- `NHibernate.Extensions.AsSplitQuery.1.0.0.snupkg` - Symbol package for debugging

### 2. Inspect the Package (Optional)

```bash
# Install NuGet Package Explorer (if not already installed)
dotnet tool install -g NuGetPackageExplorer

# Open the package
nuget-package-explorer ../../nupkg/NHibernate.Extensions.AsSplitQuery.1.0.0.nupkg
```

Or use online tool: https://www.fuget.org/

## Publishing to NuGet.org

### 1. Get API Key

1. Go to https://www.nuget.org/
2. Sign in or create an account
3. Go to Account Settings ? API Keys
4. Create a new API key with "Push" permission
5. Copy the API key

### 2. Publish the Package

```bash
cd ../../nupkg
dotnet nuget push NHibernate.Extensions.AsSplitQuery.1.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

### 3. Publish Symbol Package (Optional but Recommended)

```bash
dotnet nuget push NHibernate.Extensions.AsSplitQuery.1.0.0.snupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

## Versioning

This project follows [Semantic Versioning](https://semver.org/):

- **MAJOR** version for incompatible API changes
- **MINOR** version for backwards-compatible functionality additions
- **PATCH** version for backwards-compatible bug fixes

To update the version, edit `src/NHibernate.Extensions.AsSplitQuery/NHibernate.Extensions.AsSplitQuery.csproj`:

```xml
<Version>1.1.0</Version>
```

## Local Testing Before Publishing

### 1. Create Local NuGet Source

```bash
# Create a local folder for packages
mkdir C:\LocalNuGet

# Add as a NuGet source
dotnet nuget add source C:\LocalNuGet --name LocalPackages
```

### 2. Push to Local Source

```bash
dotnet nuget push NHibernate.Extensions.AsSplitQuery.1.0.0.nupkg --source LocalPackages
```

### 3. Test in Another Project

```bash
# In your test project
dotnet add package NHibernate.Extensions.AsSplitQuery --version 1.0.0 --source LocalPackages
```

## Continuous Integration (Optional)

### GitHub Actions Example

Create `.github/workflows/build-and-publish.yml`:

```yaml
name: Build and Publish

on:
  push:
    tags:
      - 'v*'

jobs:
  build-and-publish:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Test
      run: dotnet test --configuration Release --no-build
    
    - name: Pack
      run: dotnet pack --configuration Release --no-build --output ./nupkg
    
    - name: Publish to NuGet
      run: dotnet nuget push ./nupkg/*.nupkg --api-key ${{secrets.NUGET_API_KEY}} --source https://api.nuget.org/v3/index.json
```

## Troubleshooting

### Issue: Package Already Exists

NuGet.org doesn't allow republishing the same version. Increment the version number and try again.

### Issue: Missing README in Package

Ensure `README.md` exists in `docs/` folder and is referenced in `.csproj`:

```xml
<ItemGroup>
  <None Include="..\..\docs\README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

### Issue: Symbol Package Rejected

Make sure you're using the `.snupkg` format (not `.symbols.nupkg`):

```xml
<SymbolPackageFormat>snupkg</SymbolPackageFormat>
```

## Post-Publishing Checklist

- [ ] Verify package appears on NuGet.org (may take 10-15 minutes)
- [ ] Test installation in a fresh project
- [ ] Update GitHub release notes
- [ ] Update CHANGELOG.md
- [ ] Announce on social media / community forums
- [ ] Update documentation if needed

## Useful Commands

```bash
# Clean all build artifacts
dotnet clean

# Build for specific framework
dotnet build --framework net8.0

# Create package with specific version
dotnet pack /p:Version=1.0.1

# List all packages in a folder
dotnet nuget locals all --list

# Clear NuGet cache
dotnet nuget locals all --clear
```

## Resources

- [NuGet Documentation](https://docs.microsoft.com/en-us/nuget/)
- [Creating NuGet Packages](https://docs.microsoft.com/en-us/nuget/create-packages/overview-and-workflow)
- [Publishing to NuGet](https://docs.microsoft.com/en-us/nuget/nuget-org/publish-a-package)
- [Symbol Packages](https://docs.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg)
