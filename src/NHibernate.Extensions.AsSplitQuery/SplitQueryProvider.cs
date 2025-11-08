using NHibernate;
using NHibernate.Engine;
using NHibernate.Impl;
using NHibernate.Linq;
using NHibernate.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NHibernate.Extensions.AsSplitQuery.Models;

namespace NHibernate.Extensions.AsSplitQuery;

/// <summary>
/// Custom LINQ query provider that implements split query execution for NHibernate.
/// Delegates actual execution to SplitQueryExecutor.
/// </summary>
internal class SplitQueryProvider : IQueryProvider, INhQueryProvider
{
    private readonly INhQueryProvider _underlyingProvider;
    private readonly SplitQueryExecutor _splitQueryExecutor;

    public ISessionImplementor Session { get; }

    public SplitQueryProvider(INhQueryProvider underlyingProvider)
    {
        _underlyingProvider = underlyingProvider ?? throw new ArgumentNullException(nameof(underlyingProvider));
        
        var defaultProvider = underlyingProvider as DefaultQueryProvider
            ?? throw new InvalidOperationException("The underlying LINQ provider is not the expected DefaultQueryProvider.");
        
        Session = defaultProvider.Session;
        _splitQueryExecutor = new SplitQueryExecutor((ISession)Session, _underlyingProvider);
    }

    public IQueryable CreateQuery(Expression expression) 
        => new SplitQueryable<object>(this, expression);
    
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) 
        => new SplitQueryable<TElement>(this, expression);
    
    public object? Execute(Expression expression) 
        => _splitQueryExecutor.Execute(expression);
    
    public TResult Execute<TResult>(Expression expression)
    {
        var result = _splitQueryExecutor.Execute(expression);
        if (result == null)
            return default!;
        return (TResult)result;
    }

    public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Execute<TResult>(expression));
    }

    // Delegate all other INhQueryProvider methods to the underlying provider
    public Task<int> ExecuteDmlAsync<T>(QueryMode queryMode, Expression expression, CancellationToken cancellationToken)
        => _underlyingProvider.ExecuteDmlAsync<T>(queryMode, expression, cancellationToken);

#pragma warning disable CS0618 // Type or member is obsolete - Required by INhQueryProvider interface
    public IFutureEnumerable<TResult> ExecuteFuture<TResult>(Expression expression)
        => _underlyingProvider.ExecuteFuture<TResult>(expression);

    public IFutureValue<TResult> ExecuteFutureValue<TResult>(Expression expression)
        => _underlyingProvider.ExecuteFutureValue<TResult>(expression);
#pragma warning restore CS0618

    public void SetResultTransformerAndAdditionalCriteria(
        IQuery query,
        NhLinqExpression nhExpression,
        IDictionary<string, Tuple<object, IType>> parameters)
        => _underlyingProvider.SetResultTransformerAndAdditionalCriteria(query, nhExpression, parameters);

    public int ExecuteDml<T>(QueryMode queryMode, Expression expression)
        => _underlyingProvider.ExecuteDml<T>(queryMode, expression);
}
