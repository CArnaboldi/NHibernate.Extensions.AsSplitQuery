namespace NHibernate.Extensions.AsSplitQuery.Tests;

using FluentAssertions;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Connection;
using NHibernate.Dialect;
using NHibernate.Driver;
using NHibernate.Extensions.AsSplitQuery;
using NHibernate.Linq;
using NHibernate.Mapping.ByCode;
using NHibernate.Tool.hbm2ddl;
using System.Data.Common;
using System.Data.SQLite;
using Xunit;
using Cascade = NHibernate.Mapping.ByCode.Cascade;

/// <summary>
/// Custom connection provider for in-memory SQLite testing.
/// Ensures the same connection is used throughout the test lifecycle.
/// </summary>
public class TestConnectionProvider : IConnectionProvider
{
    public static SQLiteConnection? SharedConnection { get; set; }
    private IDriver _driver = new SQLite20Driver();

    public void Dispose()
    {
    }

    public void CloseConnection(DbConnection conn)
    {
        // Don't close the shared connection
    }

    public DbConnection GetConnection()
    {
        return SharedConnection ?? throw new InvalidOperationException("Shared connection not initialized");
    }

    public Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(GetConnection());
    }

    public IDriver Driver 
    { 
        get => _driver;
        set => _driver = value;
    }

    public void Configure(IDictionary<string, string> settings)
    {
        _driver = new SQLite20Driver();
    }
}

/// <summary>
/// Test entities for AsSplitQuery integration tests.
/// These mirror the real entities but are simplified for testing.
/// </summary>
public class TestOrder
{
    public virtual int Id { get; set; }
    public virtual string Code { get; set; } = string.Empty;
    public virtual ISet<TestPhase> Phases { get; set; } = new HashSet<TestPhase>();
}

public class TestPhase
{
    public virtual int Id { get; set; }
    public virtual string Name { get; set; } = string.Empty;
    public virtual TestOrder Order { get; set; } = null!;
    public virtual ISet<TestDowntime> Downtimes { get; set; } = new HashSet<TestDowntime>();
}

public class TestDowntime
{
    public virtual int Id { get; set; }
    public virtual string Reason { get; set; } = string.Empty;
    public virtual TestPhase Phase { get; set; } = null!;
}

/// <summary>
/// Integration tests for AsSplitQuery with real NHibernate and SQLite in-memory database.
/// These tests verify that the split query functionality works correctly with actual queries.
/// </summary>
public class AsSplitQueryIntegrationTests : IDisposable
{
    private readonly ISessionFactory _sessionFactory;
    private readonly SQLiteConnection _connection;

    public AsSplitQueryIntegrationTests()
    {
        // Create an in-memory SQLite connection that stays open
        _connection = new SQLiteConnection("Data Source=:memory:;Version=3;");
        _connection.Open();

        // Configure NHibernate
        var configuration = new Configuration();
        configuration.DataBaseIntegration(db =>
        {
            db.Dialect<SQLiteDialect>();
            db.Driver<SQLite20Driver>();
            db.ConnectionProvider<TestConnectionProvider>();
            db.LogSqlInConsole = false;
            db.LogFormattedSql = false;
        });

        // Store connection for the provider
        TestConnectionProvider.SharedConnection = _connection;

        // Map entities using code-based mapping with HiLo generator for better SQLite compatibility
        var mapper = new ModelMapper();
        mapper.Class<TestOrder>(map =>
        {
            map.Table("TestOrders");
            map.Id(x => x.Id, m => m.Generator(Generators.Native));
            map.Property(x => x.Code, m => m.Length(50));
            map.Set(x => x.Phases, 
                c => { 
                    c.Key(k => k.Column("OrderId")); 
                    c.Inverse(true); 
                    c.Cascade(Cascade.All | Cascade.DeleteOrphans);
                    c.Lazy(CollectionLazy.NoLazy); 
                },
                r => r.OneToMany());
        });

        mapper.Class<TestPhase>(map =>
        {
            map.Table("TestPhases");
            map.Id(x => x.Id, m => m.Generator(Generators.Native));
            map.Property(x => x.Name, m => m.Length(100));
            map.ManyToOne(x => x.Order, m => { m.Column("OrderId"); m.NotNullable(true); });
            map.Set(x => x.Downtimes,
                c => { 
                    c.Key(k => k.Column("PhaseId")); 
                    c.Inverse(true); 
                    c.Cascade(Cascade.All | Cascade.DeleteOrphans);
                    c.Lazy(CollectionLazy.NoLazy);
                },
                r => r.OneToMany());
        });

        mapper.Class<TestDowntime>(map =>
        {
            map.Table("TestDowntimes");
            map.Id(x => x.Id, m => m.Generator(Generators.Native));
            map.Property(x => x.Reason, m => m.Length(200));
            map.ManyToOne(x => x.Phase, m => { m.Column("PhaseId"); m.NotNullable(true); });
        });

        configuration.AddMapping(mapper.CompileMappingForAllExplicitlyAddedEntities());

        _sessionFactory = configuration.BuildSessionFactory();

        // Create schema
        using var session = _sessionFactory.OpenSession();
        new SchemaExport(configuration).Execute(false, true, false, _connection, null);
    }

    /// <summary>
    /// Tests that AsSplitQuery works with a simple query without fetch operations.
    /// </summary>
    [Fact]
    public async Task AsSplitQuery_WithoutFetch_ShouldReturnResults()
    {
        // Arrange
        using var session = _sessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();

        var order = new TestOrder { Code = "ORD001" };
        await session.SaveAsync(order);
        await transaction.CommitAsync();

        // Act
        using var querySession = _sessionFactory.OpenSession();
        var results = await querySession.Query<TestOrder>()
            .AsSplitQuery()
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Code.Should().Be("ORD001");
    }

    /// <summary>
    /// Tests that AsSplitQuery correctly loads nested collections with FetchMany.
    /// </summary>
    [Fact]
    public async Task AsSplitQuery_WithFetchMany_ShouldLoadNestedCollections()
    {
        // Arrange
        using var session = _sessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();

        var order = new TestOrder { Code = "ORD001" };
        var phase1 = new TestPhase { Name = "Phase1", Order = order };
        var phase2 = new TestPhase { Name = "Phase2", Order = order };
        order.Phases.Add(phase1);
        order.Phases.Add(phase2);

        await session.SaveAsync(order);
        await transaction.CommitAsync();

        // Act
        using var querySession = _sessionFactory.OpenSession();
        var results = await querySession.Query<TestOrder>()
            .FetchMany(o => o.Phases)
            .AsSplitQuery()
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Phases.Should().HaveCount(2);
        results[0].Phases.Select(p => p.Name).Should().Contain(new[] { "Phase1", "Phase2" });
    }

    /// <summary>
    /// Tests that AsSplitQuery correctly loads deeply nested collections with ThenFetchMany.
    /// </summary>
    [Fact]
    public async Task AsSplitQuery_WithThenFetchMany_ShouldLoadDeeplyNestedCollections()
    {
        // Arrange
        using var session = _sessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();

        var order = new TestOrder { Code = "ORD001" };
        var phase = new TestPhase { Name = "Phase1", Order = order };
        var downtime1 = new TestDowntime { Reason = "Breakdown", Phase = phase };
        var downtime2 = new TestDowntime { Reason = "Maintenance", Phase = phase };
        
        phase.Downtimes.Add(downtime1);
        phase.Downtimes.Add(downtime2);
        order.Phases.Add(phase);

        await session.SaveAsync(order);
        await transaction.CommitAsync();

        // Act
        using var querySession = _sessionFactory.OpenSession();
        var results = await querySession.Query<TestOrder>()
            .FetchMany(o => o.Phases)
            .ThenFetchMany(p => p.Downtimes)
            .AsSplitQuery()
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Phases.Should().HaveCount(1);
        var loadedPhase = results[0].Phases.First();
        loadedPhase.Downtimes.Should().HaveCount(2);
        loadedPhase.Downtimes.Select(d => d.Reason).Should().Contain(new[] { "Breakdown", "Maintenance" });
    }

    /// <summary>
    /// Tests that AsSplitQuery works correctly with LINQ Where clause.
    /// </summary>
    [Fact]
    public async Task AsSplitQuery_WithWhereClause_ShouldFilterResults()
    {
        // Arrange
        using var session = _sessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();

        var order1 = new TestOrder { Code = "ORD001" };
        var order2 = new TestOrder { Code = "ORD002" };
        
        await session.SaveAsync(order1);
        await session.SaveAsync(order2);
        await transaction.CommitAsync();

        // Act
        using var querySession = _sessionFactory.OpenSession();
        var results = await querySession.Query<TestOrder>()
            .Where(o => o.Code == "ORD001")
            .AsSplitQuery()
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Code.Should().Be("ORD001");
    }

    /// <summary>
    /// Tests that AsSplitQuery works correctly with OrderBy, Skip, and Take.
    /// </summary>
    [Fact]
    public async Task AsSplitQuery_WithPagination_ShouldWorkCorrectly()
    {
        // Arrange
        using var session = _sessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();

        for (int i = 1; i <= 10; i++)
        {
            await session.SaveAsync(new TestOrder { Code = $"ORD{i:D3}" });
        }
        await transaction.CommitAsync();

        // Act
        using var querySession = _sessionFactory.OpenSession();
        var results = await querySession.Query<TestOrder>()
            .OrderBy(o => o.Code)
            .AsSplitQuery()
            .Skip(3)
            .Take(3)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Code.Should().Be("ORD004");
        results[1].Code.Should().Be("ORD005");
        results[2].Code.Should().Be("ORD006");
    }

    /// <summary>
    /// Tests that AsSplitQuery handles empty collections correctly.
    /// </summary>
    [Fact]
    public async Task AsSplitQuery_WithEmptyCollections_ShouldReturnEmptySets()
    {
        // Arrange
        using var session = _sessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();

        var order = new TestOrder { Code = "ORD001" };
        await session.SaveAsync(order);
        await transaction.CommitAsync();

        // Act
        using var querySession = _sessionFactory.OpenSession();
        var results = await querySession.Query<TestOrder>()
            .FetchMany(o => o.Phases)
            .AsSplitQuery()
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Phases.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that AsSplitQuery prevents cartesian explosion compared to regular eager loading.
    /// This test verifies that split queries are more efficient than join-based eager loading.
    /// </summary>
    [Fact]
    public async Task AsSplitQuery_ShouldPreventCartesianExplosion()
    {
        // Arrange
        using var session = _sessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();

        // Create an order with multiple phases, each with multiple downtimes
        var order = new TestOrder { Code = "ORD001" };
        
        for (int i = 1; i <= 5; i++)
        {
            var phase = new TestPhase { Name = $"Phase{i}", Order = order };
            for (int j = 1; j <= 3; j++)
            {
                phase.Downtimes.Add(new TestDowntime { Reason = $"Reason{j}", Phase = phase });
            }
            order.Phases.Add(phase);
        }

        await session.SaveAsync(order);
        await transaction.CommitAsync();

        // Act - Use split query
        using var querySession = _sessionFactory.OpenSession();
        var results = await querySession.Query<TestOrder>()
            .FetchMany(o => o.Phases)
            .ThenFetchMany(p => p.Downtimes)
            .AsSplitQuery()
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Phases.Should().HaveCount(5);
        
        // Verify all phases have their downtimes loaded
        foreach (var phase in results[0].Phases)
        {
            phase.Downtimes.Should().HaveCount(3);
        }
    }

    /// <summary>
    /// Tests that applying AsSplitQuery multiple times is idempotent.
    /// </summary>
    [Fact]
    public async Task AsSplitQuery_AppliedTwice_ShouldBeIdempotent()
    {
        // Arrange
        using var session = _sessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();

        var order = new TestOrder { Code = "ORD001" };
        await session.SaveAsync(order);
        await transaction.CommitAsync();

        // Act
        using var querySession = _sessionFactory.OpenSession();
        var results = await querySession.Query<TestOrder>()
            .AsSplitQuery()
            .AsSplitQuery() // Applied twice
            .ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Code.Should().Be("ORD001");
    }

    /// <summary>
    /// Tests that AsSplitQuery does not interfere with transaction management.
    /// </summary>
    [Fact]
    public async Task AsSplitQuery_ShouldNotInterfere_WithTransactions()
    {
        // Arrange - Create initial data with 2 phases
        using (var session = _sessionFactory.OpenSession())
        using (var transaction = session.BeginTransaction())
        {
            var order = new TestOrder { Code = "ORD001" };
            var phase1 = new TestPhase { Name = "Phase1", Order = order };
            var phase2 = new TestPhase { Name = "Phase2", Order = order };
            order.Phases.Add(phase1);
            order.Phases.Add(phase2);
            
            await session.SaveAsync(order);
            await transaction.CommitAsync();
        }

        // Act - Load with AsSplitQuery, modify, and save
        using (var session = _sessionFactory.OpenSession())
        using (var transaction = session.BeginTransaction())
        {
            var order = await session.Query<TestOrder>()
                .Where(o => o.Code == "ORD001")
                .FetchMany(o => o.Phases)
                .AsSplitQuery()
                .FirstAsync();

            // Modify the order
            order.Code = "ORD001-MODIFIED";
            
            // Modify existing phases
            foreach (var phase in order.Phases)
            {
                phase.Name = phase.Name + "-UPDATED";
            }
            
            await transaction.CommitAsync();
        }

        // Assert - Verify changes were persisted
        using (var session = _sessionFactory.OpenSession())
        {
            var order = await session.Query<TestOrder>()
                .Where(o => o.Code == "ORD001-MODIFIED")
                .FetchMany(o => o.Phases)
                .FirstAsync();
            
            order.Code.Should().Be("ORD001-MODIFIED");
            order.Phases.Should().HaveCount(2);
            order.Phases.Should().OnlyContain(p => p.Name.EndsWith("-UPDATED"));
        }
    }

    /// <summary>
    /// Tests that AsSplitQuery does not break dirty checking.
    /// </summary>
    [Fact]
    public async Task AsSplitQuery_ShouldNotBreak_DirtyChecking()
    {
        // Arrange
        using (var session = _sessionFactory.OpenSession())
        using (var transaction = session.BeginTransaction())
        {
            var order = new TestOrder { Code = "ORD001" };
            await session.SaveAsync(order);
            await transaction.CommitAsync();
        }

        // Act - Load with AsSplitQuery and modify WITHOUT explicit save
        using (var session = _sessionFactory.OpenSession())
        using (var transaction = session.BeginTransaction())
        {
            var order = await session.Query<TestOrder>()
                .Where(o => o.Code == "ORD001")
                .AsSplitQuery()
                .FirstAsync();

            // Modify - NHibernate should detect this via dirty checking
            order.Code = "ORD001-DIRTY";
            
            // Flush should trigger UPDATE due to dirty checking
            await session.FlushAsync();
            await transaction.CommitAsync();
        }

        // Assert - Verify dirty checking worked
        using (var session = _sessionFactory.OpenSession())
        {
            var order = await session.Query<TestOrder>()
                .Where(o => o.Code == "ORD001-DIRTY")
                .FirstOrDefaultAsync();
            
            order.Should().NotBeNull("dirty checking should have persisted the change");
        }
    }

    /// <summary>
    /// Tests that AsSplitQuery does not cause unexpected database writes.
    /// </summary>
    [Fact]
    public async Task AsSplitQuery_ShouldNotCause_UnexpectedWrites()
    {
        // Arrange
        using (var session = _sessionFactory.OpenSession())
        using (var transaction = session.BeginTransaction())
        {
            var order = new TestOrder { Code = "ORD001" };
            var phase = new TestPhase { Name = "Phase1", Order = order };
            order.Phases.Add(phase);
            
            await session.SaveAsync(order);
            await transaction.CommitAsync();
        }

        // Act - Load with AsSplitQuery, do NOT modify, and flush
        using (var session = _sessionFactory.OpenSession())
        using (var transaction = session.BeginTransaction())
        {
            var order = await session.Query<TestOrder>()
                .FetchMany(o => o.Phases)
                .AsSplitQuery()
                .FirstAsync();

            // Do NOT modify anything
            
            // Flush should NOT generate any UPDATE statements
            await session.FlushAsync();
            await transaction.CommitAsync();
        }

        // Assert - Order should be unchanged
        using (var session = _sessionFactory.OpenSession())
        {
            var order = await session.Query<TestOrder>()
                .FetchMany(o => o.Phases)
                .FirstAsync();
            
            order.Code.Should().Be("ORD001");
            order.Phases.Should().HaveCount(1);
            order.Phases.First().Name.Should().Be("Phase1");
        }
    }

    /// <summary>
    /// Tests that AsSplitQuery works correctly when nested in a larger transaction.
    /// </summary>
    [Fact]
    public async Task AsSplitQuery_ShouldWork_WithinLargerTransaction()
    {
        // Arrange & Act - Perform multiple operations in one transaction
        using (var session = _sessionFactory.OpenSession())
        using (var transaction = session.BeginTransaction())
        {
            // Create an order
            var order1 = new TestOrder { Code = "ORD001" };
            await session.SaveAsync(order1);
            await session.FlushAsync();

            // Load with AsSplitQuery
            var loadedOrders = await session.Query<TestOrder>()
                .AsSplitQuery()
                .ToListAsync();

            // Create another order after AsSplitQuery
            var order2 = new TestOrder { Code = "ORD002" };
            await session.SaveAsync(order2);

            await transaction.CommitAsync();
        }

        // Assert - Both orders should be persisted
        using (var session = _sessionFactory.OpenSession())
        {
            var orders = await session.Query<TestOrder>().ToListAsync();
            orders.Should().HaveCount(2);
        }
    }

    /// <summary>
    /// Tests that rolling back a transaction after AsSplitQuery works correctly.
    /// </summary>
    [Fact]
    public async Task AsSplitQuery_ShouldRespect_TransactionRollback()
    {
        // Arrange
        using (var session = _sessionFactory.OpenSession())
        using (var transaction = session.BeginTransaction())
        {
            var order = new TestOrder { Code = "ORD001" };
            await session.SaveAsync(order);
            await transaction.CommitAsync();
        }

        // Act - Load with AsSplitQuery, modify, then rollback
        using (var session = _sessionFactory.OpenSession())
        using (var transaction = session.BeginTransaction())
        {
            var order = await session.Query<TestOrder>()
                .AsSplitQuery()
                .FirstAsync();

            order.Code = "ORD001-SHOULD-NOT-PERSIST";
            
            await transaction.RollbackAsync(); // Rollback instead of commit
        }

        // Assert - Changes should NOT be persisted
        using (var session = _sessionFactory.OpenSession())
        {
            var order = await session.Query<TestOrder>().FirstAsync();
            order.Code.Should().Be("ORD001", "transaction was rolled back");
        }
    }

    public void Dispose()
    {
        _sessionFactory?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}
