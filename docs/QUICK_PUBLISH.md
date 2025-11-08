# Quick Publishing Commands

## TL;DR - Fast Track to Publishing

```powershell
# 1. Navigate to solution root
cd C:\Progetti.GIT\CArnaboldi\NHibernate.Extensions.AsSplitQuery

# 2. Clean, build, and test
dotnet clean -c Release
dotnet build -c Release
dotnet test -c Release --no-build

# 3. Create package
cd src\NHibernate.Extensions.AsSplitQuery
dotnet pack -c Release --no-build -o ../../artifacts

# 4. Publish (replace YOUR_API_KEY)
cd ../../artifacts
dotnet nuget push NHibernate.Extensions.AsSplitQuery.1.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json

# 5. Create and push Git tag
cd ..
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0
```

## Expected Results

? **Build**: Should succeed with 0 errors, 0 warnings
? **Tests**: 26 tests should pass (0 failed, 0 skipped)
? **Package**: Should create `.nupkg` and `.snupkg` files
? **Push**: Should show "Your package was pushed"

## Verification

After publishing (wait 10-15 minutes):
1. Visit: https://www.nuget.org/packages/NHibernate.Extensions.AsSplitQuery
2. Test install: `dotnet add package NHibernate.Extensions.AsSplitQuery`

## Troubleshooting

**"Package already exists"**
? Version 1.0.0 exists. Increment version in .csproj

**"API key invalid"**
? Check key at https://www.nuget.org/account/apikeys

**"Tests failed"**
? Review test output and fix issues before publishing

---

For detailed instructions, see [PUBLISHING.md](PUBLISHING.md)
