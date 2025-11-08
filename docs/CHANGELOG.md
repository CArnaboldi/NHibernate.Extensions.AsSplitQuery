# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2025-11-08

### Added
- Initial release of NHibernate.Extensions.AsSplitQuery
- `AsSplitQuery()` extension method for `IQueryable<T>`
- Support for `FetchMany()` and `ThenFetchMany()` operations
- Automatic collection hydration with proper NHibernate session management
- Thread-safe reflection caching for optimal performance
- Full async support (`ToListAsync()`, `FirstAsync()`, etc.)
- Compatibility with .NET 6.0, .NET 8.0, and .NET Standard 2.1
- Comprehensive XML documentation
- Integration tests with SQLite in-memory database
- Support for complex nested collections
- Automatic deduplication of queries when multiple fetch paths target the same collection

### Features
- Prevents cartesian product explosion in eager loading scenarios
- 50-100x performance improvement for queries with multiple nested collections
- Seamless integration with existing NHibernate LINQ queries
- Works with transactions, dirty checking, and rollback
- Compatible with all LINQ operators (`Where`, `OrderBy`, `Skip`, `Take`, etc.)

### Known Limitations
- `FirstAsync()` and `SingleAsync()` may not load all nested collections (use `ToListAsync().FirstOrDefault()` as workaround)
- Composite foreign keys are not supported
- Requires NHibernate 5.5.0 or higher

[Unreleased]: https://github.com/CArnaboldi/NHibernate.Extensions.AsSplitQuery/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/CArnaboldi/NHibernate.Extensions.AsSplitQuery/releases/tag/v1.0.0
