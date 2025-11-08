namespace NHibernate.Extensions.AsSplitQuery;

using NHibernate;
using NHibernate.Engine;
using NHibernate.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NHibernate.Extensions.AsSplitQuery.Models;

/// <summary>
/// Builds child entity queries with proper filtering by parent IDs.
/// </summary>
internal class ChildQueryBuilder
{
    private readonly ISession _session;
    private static readonly MethodInfo GenericFetchMethod = typeof(EagerFetchingExtensionMethods)
        .GetMethods()
        .First(m => m.Name == "Fetch" && m.GetParameters().Length == 2);

    public ChildQueryBuilder(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Creates a query to load child entities filtered by parent IDs.
    /// </summary>
    public IQueryable BuildQuery(FetchPath path, List<object> parentEntities, ISessionImplementor sessionImpl)
    {
        var baseQuery = _createBaseQuery(path.ChildEntityType);
        var filteredQuery = _applyParentIdFilter(baseQuery, path, parentEntities, sessionImpl);
        return _applyNestedFetches(filteredQuery, path.NestedFetches);
    }

    private IQueryable _createBaseQuery(Type childEntityType)
    {
        var queryMethod = ReflectionCache.GetQueryMethod(childEntityType);
        return (IQueryable)queryMethod.Invoke(_session, null);
    }

    private IQueryable _applyParentIdFilter(
        IQueryable baseQuery,
        FetchPath path,
        List<object> parentEntities,
        ISessionImplementor sessionImpl)
    {
        var parentIdProperty = ReflectionCache.GetIdProperty(path.ParentEntityType, sessionImpl);
        var parentIds = _extractParentIds(parentEntities, parentIdProperty, sessionImpl);
        var filterExpression = _buildContainsExpression(path, parentIdProperty, parentIds);
        
        return _applyWhereClause(baseQuery, path.ChildEntityType, filterExpression);
    }

    private IList _extractParentIds(
        List<object> parentEntities,
        PropertyInfo parentIdProperty,
        ISessionImplementor sessionImpl)
    {
        var parentIdsListType = ReflectionCache.MakeGenericType(typeof(List<>), parentIdProperty.PropertyType);
        var parentIdsList = (IList)Activator.CreateInstance(parentIdsListType);

        foreach (var parent in parentEntities)
        {
            var persister = sessionImpl.GetEntityPersister(parent.GetType().FullName, parent);
            parentIdsList.Add(persister.GetIdentifier(parent));
        }

        return parentIdsList;
    }

    private Expression _buildContainsExpression(
        FetchPath path,
        PropertyInfo parentIdProperty,
        IList parentIds)
    {
        var childParam = Expression.Parameter(path.ChildEntityType, "child");
        var backRefProperty = Expression.Property(childParam, path.BackReferenceProperty);
        var parentIdAccess = Expression.Property(backRefProperty, parentIdProperty);

        var containsMethod = typeof(Enumerable)
            .GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(parentIdProperty.PropertyType);

        var ienumerableType = ReflectionCache.MakeGenericType(typeof(IEnumerable<>), parentIdProperty.PropertyType);
        
        return Expression.Lambda(
            Expression.Call(null, containsMethod, Expression.Constant(parentIds, ienumerableType), parentIdAccess),
            childParam);
    }

    private IQueryable _applyWhereClause(IQueryable query, Type entityType, Expression filterLambda)
    {
        var whereMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == "Where" && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType);

        var whereCall = Expression.Call(null, whereMethod, query.Expression, filterLambda);
        return query.Provider.CreateQuery(whereCall);
    }

    private IQueryable _applyNestedFetches(IQueryable query, List<PropertyInfo> nestedFetches)
    {
        if (!nestedFetches.Any()) 
            return query;

        var currentQuery = query;
        foreach (var fetch in nestedFetches)
        {
            currentQuery = _applyFetch(currentQuery, fetch);
        }
        return currentQuery;
    }

    private IQueryable _applyFetch(IQueryable query, PropertyInfo fetchProperty)
    {
        var param = Expression.Parameter(query.ElementType, "x");
        var property = Expression.Property(param, fetchProperty);
        var lambda = Expression.Lambda(property, param);

        var fetchMethod = GenericFetchMethod.MakeGenericMethod(query.ElementType, fetchProperty.PropertyType);
        var fetchCall = Expression.Call(null, fetchMethod, query.Expression, lambda);
        
        return query.Provider.CreateQuery(fetchCall);
    }
}
