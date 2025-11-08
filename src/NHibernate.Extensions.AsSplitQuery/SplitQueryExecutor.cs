namespace NHibernate.Extensions.AsSplitQuery;

using NHibernate;
using NHibernate.Engine;
using NHibernate.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using NHibernate.Extensions.AsSplitQuery.Models;

/// <summary>
/// Executes split queries by coordinating fetch path analysis, child query execution, and collection hydration.
/// </summary>
internal class SplitQueryExecutor
{
    private readonly ISession _session;
    private readonly INhQueryProvider _underlyingProvider;
    private readonly FetchPathAnalyzer _fetchPathAnalyzer;
    private readonly FetchPathOrganizer _fetchPathOrganizer;
    private readonly ExpressionRewriter _expressionRewriter;
    private readonly ChildQueryBuilder _childQueryBuilder;
    private readonly CollectionHydrator _collectionHydrator;

    public SplitQueryExecutor(ISession session, INhQueryProvider underlyingProvider)
    {
        _session = session;
        _underlyingProvider = underlyingProvider;
        _fetchPathAnalyzer = new FetchPathAnalyzer(session);
        _fetchPathOrganizer = new FetchPathOrganizer();
        _expressionRewriter = new ExpressionRewriter();
        _childQueryBuilder = new ChildQueryBuilder(session);
        _collectionHydrator = new CollectionHydrator();
    }

    /// <summary>
    /// Executes a query using the split query strategy.
    /// </summary>
    public object Execute(Expression expression)
    {
        var allFetchPaths = _fetchPathAnalyzer.ExtractFetchPaths(expression);
        
        if (!allFetchPaths.Any())
            return _underlyingProvider.Execute(expression);

        var rootExpression = _expressionRewriter.StripFetchOperations(expression);
        var rootResult = _underlyingProvider.Execute(rootExpression);

        return _isCollection(rootResult) 
            ? _executeSplitQueryForCollection(rootResult, expression, allFetchPaths)
            : rootResult; // Single entity - can't optimize efficiently
    }

    private bool _isCollection(object result) => result is IEnumerable;

    private object _executeSplitQueryForCollection(object rootResult, Expression originalExpression, List<FetchPath> fetchPaths)
    {
        var rootResults = _convertToObjectList(rootResult);
        
        if (!rootResults.Any())
            return _createEmptyTypedEnumerable(originalExpression);

        var sessionImpl = (ISessionImplementor)_session;
        var processedEntities = _initializeProcessedEntities(rootResults, sessionImpl);
        var fetchLevels = _fetchPathOrganizer.OrganizeByLevel(fetchPaths);
        
        _processAllFetchLevels(fetchLevels, rootResults, processedEntities, sessionImpl);
        
        return _createTypedEnumerable(rootResults, rootResults[0].GetType());
    }

    private List<object> _convertToObjectList(object enumerable)
    {
        return ((IEnumerable)enumerable).Cast<object>().ToList();
    }

    private object _createEmptyTypedEnumerable(Expression expression)
    {
        return _createTypedEnumerable(new List<object>(), _getElementType(expression));
    }

    private Dictionary<object, object> _initializeProcessedEntities(List<object> entities, ISessionImplementor sessionImpl)
    {
        var processed = new Dictionary<object, object>();
        
        foreach (var entity in entities)
        {
            var persister = sessionImpl.GetEntityPersister(entity.GetType().FullName, entity);
            var key = persister.GetIdentifier(entity);
            processed[key] = entity;
        }
        
        return processed;
    }

    private void _processAllFetchLevels(
        List<List<FetchPath>> fetchLevels,
        List<object> rootResults,
        Dictionary<object, object> processedEntities,
        ISessionImplementor sessionImpl)
    {
        foreach (var level in fetchLevels)
        {
            foreach (var path in level)
            {
                _executeAndHydrateFetchPath(path, rootResults, processedEntities, sessionImpl);
            }
        }
    }

    private void _executeAndHydrateFetchPath(
        FetchPath path,
        List<object> rootResults,
        Dictionary<object, object> allEntities,
        ISessionImplementor sessionImpl)
    {
        var parentEntities = _fetchPathOrganizer.FindParentEntities(path, rootResults, allEntities);
        
        if (!parentEntities.Any() || _areCollectionsAlreadyInitialized(path, parentEntities, sessionImpl))
            return;

        var childQuery = _childQueryBuilder.BuildQuery(path, parentEntities, sessionImpl);
        var childResults = _executeChildQuery(childQuery);
        
        if (!childResults.Any())
            return;

        _addChildEntitiesToProcessed(childResults, allEntities, sessionImpl);
        _collectionHydrator.Hydrate(path, parentEntities, childResults, sessionImpl);
    }

    private bool _areCollectionsAlreadyInitialized(
        FetchPath path,
        List<object> parentEntities,
        ISessionImplementor sessionImpl)
    {
        var firstParent = parentEntities.FirstOrDefault();
        if (firstParent == null) return true;

        var roleName = $"{path.ParentEntityType.FullName}.{path.CollectionProperty.Name}";
        var collectionPersister = sessionImpl.Factory.GetCollectionPersister(roleName);
        var parentIdProperty = ReflectionCache.GetIdProperty(path.ParentEntityType, sessionImpl);
        var parentId = parentIdProperty.GetValue(firstParent);
        
        var collectionKey = new NHibernate.Engine.CollectionKey(collectionPersister, parentId);
        var persistentCollection = sessionImpl.PersistenceContext.GetCollection(collectionKey) as NHibernate.Collection.IPersistentCollection;
        
        if (persistentCollection == null)
        {
            var tempCollection = path.CollectionProperty.GetValue(firstParent);
            persistentCollection = tempCollection as NHibernate.Collection.IPersistentCollection;
        }
        
        return persistentCollection?.WasInitialized ?? false;
    }

    private List<object> _executeChildQuery(IQueryable childQuery)
    {
        return ((IEnumerable)_underlyingProvider.Execute(childQuery.Expression)).Cast<object>().ToList();
    }

    private void _addChildEntitiesToProcessed(
        List<object> childResults,
        Dictionary<object, object> allEntities,
        ISessionImplementor sessionImpl)
    {
        foreach (var child in childResults)
        {
            var persister = sessionImpl.GetEntityPersister(child.GetType().FullName, child);
            var key = persister.GetIdentifier(child);
            
            if (!allEntities.ContainsKey(key))
                allEntities[key] = child;
        }
    }

    private Type _getElementType(Expression expression)
    {
        var type = expression.Type;
        return type.IsGenericType && type.GetGenericArguments().Length > 0
            ? type.GetGenericArguments()[0]
            : typeof(object);
    }

    private object _createTypedEnumerable(List<object> results, Type elementType)
    {
        var listType = ReflectionCache.MakeGenericType(typeof(List<>), elementType);
        var typedList = (IList)Activator.CreateInstance(listType);
        
        foreach (var item in results)
            typedList.Add(item);
        
        return typedList;
    }
}
