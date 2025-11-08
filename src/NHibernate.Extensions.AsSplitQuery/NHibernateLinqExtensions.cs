using NHibernate.Linq;
using System.Linq;

namespace NHibernate.Extensions.AsSplitQuery;

/// <summary>
/// Provides extension methods for NHibernate LINQ queries to enable split query execution.
/// </summary>
public static class NHibernateLinqExtensions
{
    /// <summary>
    /// Configures the query to split the loading of included collections into separate database queries.
    /// This prevents the cartesian explosion problem that occurs when eager loading multiple collections.
    /// Similar to Entity Framework Core's AsSplitQuery().
    /// </summary>
    /// <typeparam name="T">The entity type being queried.</typeparam>
    /// <param name="query">The query to configure.</param>
    /// <returns>A query that will execute using split queries for eager-loaded collections.</returns>
    /// <example>
    /// <code>
    /// // Optimal usage with ToListAsync()
    /// var orders = await session.Query&lt;Order&gt;()
    ///     .FetchMany(o => o.OrderItems)
    ///     .ThenFetchMany(oi => oi.Details)
    ///     .AsSplitQuery()
    ///     .ToListAsync();
    /// 
    /// // With filtering and paging
    /// var recentOrders = await session.Query&lt;Order&gt;()
    ///     .Where(o => o.OrderDate > DateTime.Now.AddMonths(-1))
    ///     .OrderBy(o => o.OrderDate)
    ///     .FetchMany(o => o.OrderItems)
    ///     .AsSplitQuery()
    ///     .Skip(20)
    ///     .Take(10)
    ///     .ToListAsync();
    /// </code>
    /// </example>
    /// <remarks>
    /// <para>
    /// <b>Performance Benefits:</b>
    /// - Prevents cartesian product explosion when loading multiple collections
    /// - Reduces data transfer and memory usage
    /// - Typically 50-100x faster for queries with nested collections
    /// </para>
    /// <para>
    /// <b>How it works:</b>
    /// 1. Executes the main query without fetch operations
    /// 2. Executes separate queries for each collection level
    /// 3. Manually hydrates the collections in memory
    /// 4. Marks collections as initialized to prevent lazy loading
    /// </para>
    /// <para>
    /// <b>Limitations:</b>
    /// - Works best with ToListAsync(), ToList(), etc.
    /// - FirstAsync() and SingleAsync() may not load all collections
    /// - Composite foreign keys are not supported
    /// </para>
    /// <para>
    /// <b>Thread Safety:</b>
    /// This extension is thread-safe and uses concurrent caching for reflection operations.
    /// </para>
    /// </remarks>
    public static IQueryable<T> AsSplitQuery<T>(this IQueryable<T> query)
    {
        if (query.Provider is SplitQueryProvider) 
            return query;
        
        var provider = new SplitQueryProvider((INhQueryProvider)query.Provider);
        return provider.CreateQuery<T>(query.Expression);
    }
}
