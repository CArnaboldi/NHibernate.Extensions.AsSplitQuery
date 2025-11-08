# Examples

## Basic Usage

### Simple Eager Loading

```csharp
using NHibernate;
using NHibernate.Linq;
using NHibernate.Extensions.AsSplitQuery;

// Without AsSplitQuery - Cartesian Product
var orders = await session.Query<Order>()
    .FetchMany(o => o.OrderItems)
    .ToListAsync();
// Result: 1 query, N×M rows (if 10 orders with 5 items each = 50 rows)

// With AsSplitQuery - Separate Queries
var orders = await session.Query<Order>()
    .FetchMany(o => o.OrderItems)
    .AsSplitQuery()
    .ToListAsync();
// Result: 2 queries, N+M rows (10 orders + 50 items = 60 rows, but more efficient)
```

## Nested Collections

### Three-Level Hierarchy

```csharp
var customers = await session.Query<Customer>()
    .FetchMany(c => c.Orders)
    .ThenFetchMany(o => o.OrderItems)
    .ThenFetchMany(i => i.Product)
    .AsSplitQuery()
    .ToListAsync();

// Executes 4 separate queries:
// 1. SELECT * FROM Customers
// 2. SELECT * FROM Orders WHERE CustomerId IN (...)
// 3. SELECT * FROM OrderItems WHERE OrderId IN (...)
// 4. SELECT * FROM Products WHERE OrderItemId IN (...)
```

## Complex Scenarios

### Multiple Independent Collections

```csharp
var order = await session.Query<Order>()
    .Where(o => o.Id == orderId)
    .FetchMany(o => o.OrderItems)
    .ThenFetchMany(i => i.ProductReviews)
    .FetchMany(o => o.Shipments)
    .ThenFetchMany(s => s.TrackingEvents)
    .FetchMany(o => o.Payments)
    .AsSplitQuery()
    .ToListAsync();

// Result: 6 queries instead of 1 massive cartesian product
```

### With Filtering and Paging

```csharp
var recentOrders = await session.Query<Order>()
    .Where(o => o.OrderDate > DateTime.Now.AddMonths(-3))
    .Where(o => o.Status == OrderStatus.Completed)
    .OrderBy(o => o.OrderDate)
    .FetchMany(o => o.OrderItems)
    .ThenFetchMany(i => i.Product)
    .AsSplitQuery()
    .Skip(20)
    .Take(10)
    .ToListAsync();
```

## Performance Comparison

### Scenario: E-Commerce Order System

**Setup:**
- 100 Orders
- Each order has 10 OrderItems
- Each item has 5 ProductTags

#### Without AsSplitQuery

```csharp
var orders = await session.Query<Order>()
    .FetchMany(o => o.OrderItems)
    .ThenFetchMany(i => i.ProductTags)
    .Take(100)
    .ToListAsync();

// Result: 1 query returning 5,000 rows (100 × 10 × 5)
// Memory: ~50 MB (with duplicated order and item data)
// Time: ~2,500ms
```

#### With AsSplitQuery

```csharp
var orders = await session.Query<Order>()
    .FetchMany(o => o.OrderItems)
    .ThenFetchMany(i => i.ProductTags)
    .AsSplitQuery()
    .Take(100)
    .ToListAsync();

// Result: 3 queries
//   Query 1: 100 rows (orders)
//   Query 2: 1,000 rows (order items)
//   Query 3: 5,000 rows (product tags)
// Total: 6,100 rows (but no duplicates!)
// Memory: ~10 MB
// Time: ~150ms
// Improvement: 16x faster, 5x less memory!
```

## Edge Cases

### Empty Collections

```csharp
// Works correctly with empty collections
var orders = await session.Query<Order>()
    .Where(o => o.OrderItems.Count == 0) // Orders with no items
    .FetchMany(o => o.OrderItems)
    .AsSplitQuery()
    .ToListAsync();

// Result: OrderItems collection will be empty but initialized
```

### Single Result Workaround

```csharp
// ? Not recommended (may not load all collections)
var order = await session.Query<Order>()
    .Where(o => o.Id == orderId)
    .FetchMany(o => o.OrderItems)
    .AsSplitQuery()
    .FirstAsync();

// ? Recommended workaround
var orders = await session.Query<Order>()
    .Where(o => o.Id == orderId)
    .FetchMany(o => o.OrderItems)
    .AsSplitQuery()
    .ToListAsync();
var order = orders.FirstOrDefault();
```

## Transaction Safety

### Within Transaction

```csharp
using (var transaction = session.BeginTransaction())
{
    // AsSplitQuery works seamlessly within transactions
    var orders = await session.Query<Order>()
        .FetchMany(o => o.OrderItems)
        .AsSplitQuery()
        .ToListAsync();

    // Modify entities
    foreach (var order in orders)
    {
        order.Status = OrderStatus.Processing;
    }

    await transaction.CommitAsync(); // All changes are tracked correctly
}
```

### Dirty Checking

```csharp
using (var session = sessionFactory.OpenSession())
using (var transaction = session.BeginTransaction())
{
    var orders = await session.Query<Order>()
        .FetchMany(o => o.OrderItems)
        .AsSplitQuery()
        .ToListAsync();

    // Modifications are tracked
    orders[0].Status = OrderStatus.Shipped;
    orders[0].OrderItems.First().Quantity = 5;

    // NHibernate's dirty checking works correctly
    await session.FlushAsync(); // Generates appropriate UPDATE statements
    await transaction.CommitAsync();
}
```

## Best Practices

### 1. Use with Collections, Not Single References

```csharp
// ? Good - Multiple collections
var orders = await session.Query<Order>()
    .FetchMany(o => o.OrderItems)      // Collection
    .FetchMany(o => o.Shipments)       // Collection
    .AsSplitQuery()
    .ToListAsync();

// ? Not needed - Single reference
var orderItems = await session.Query<OrderItem>()
    .Fetch(i => i.Product)             // Single reference - no cartesian explosion
    .AsSplitQuery()                    // Not needed here
    .ToListAsync();
```

### 2. Always Use ToListAsync() for Best Results

```csharp
// ? Optimal
var orders = await query.AsSplitQuery().ToListAsync();

// ?? May have limitations
var order = await query.AsSplitQuery().FirstAsync();
```

### 3. Profile Your Queries

```csharp
// Enable NHibernate SQL logging to see the generated queries
configuration.DataBaseIntegration(db =>
{
    db.LogSqlInConsole = true;
    db.LogFormattedSql = true;
});

// You should see multiple separate SELECT statements
```

## Real-World Example: Blog System

```csharp
public async Task<BlogPost> GetBlogPostWithComments(int postId)
{
    var posts = await _session.Query<BlogPost>()
        .Where(p => p.Id == postId)
        .FetchMany(p => p.Comments)
        .ThenFetchMany(c => c.Replies)
        .FetchMany(p => p.Tags)
        .FetchMany(p => p.Images)
        .AsSplitQuery()
        .ToListAsync();

    return posts.FirstOrDefault();
}

// Instead of 1 query with thousands of duplicated rows,
// you get 4 clean queries:
// - BlogPosts
// - Comments + Replies
// - Tags
// - Images
```

## Performance Monitoring

```csharp
var stopwatch = System.Diagnostics.Stopwatch.StartNew();

var orders = await session.Query<Order>()
    .FetchMany(o => o.OrderItems)
    .ThenFetchMany(i => i.Product)
    .AsSplitQuery()
    .ToListAsync();

stopwatch.Stop();
Console.WriteLine($"Query executed in {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine($"Loaded {orders.Count} orders with items");
```
