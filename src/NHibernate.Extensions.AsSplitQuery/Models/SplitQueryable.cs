namespace NHibernate.Extensions.AsSplitQuery.Models;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

/// <summary>
/// A queryable wrapper that uses the SplitQueryProvider for execution.
/// Implements both IQueryable&lt;T&gt; and IOrderedQueryable&lt;T&gt; to support LINQ operations.
/// </summary>
/// <typeparam name="T">The type of entities in the query.</typeparam>
internal class SplitQueryable<T> : IQueryable<T>, IOrderedQueryable<T>
{
    public SplitQueryable(IQueryProvider provider, Expression expression)
    {
        Provider = provider;
        Expression = expression;
    }

    public Expression Expression { get; }
    public Type ElementType => typeof(T);
    public IQueryProvider Provider { get; }
    
    public IEnumerator<T> GetEnumerator() 
        => Provider.Execute<IEnumerable<T>>(Expression).GetEnumerator();
    
    IEnumerator IEnumerable.GetEnumerator() 
        => Provider.Execute<IEnumerable>(Expression).GetEnumerator();
}
