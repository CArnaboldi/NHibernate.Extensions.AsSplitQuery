using NHibernate.Collection;
using NHibernate.Engine;
using NHibernate.Persister.Collection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NHibernate.Extensions.AsSplitQuery.Models;

namespace NHibernate.Extensions.AsSplitQuery;

/// <summary>
/// Handles the hydration (population) of entity collections with loaded child entities.
/// Ensures collections are properly initialized in NHibernate's persistence context.
/// </summary>
internal class CollectionHydrator
{
    /// <summary>
    /// Hydrates collections or single references for all parent entities.
    /// </summary>
    public void Hydrate(
        FetchPath path,
        List<object> parentEntities,
        List<object> childEntities,
        ISessionImplementor sessionImpl)
    {
        if (!path.IsCollection)
        {
            _hydrateSingleReferences(path, parentEntities, childEntities, sessionImpl);
            return;
        }

        _hydrateCollections(path, parentEntities, childEntities, sessionImpl);
    }

    private void _hydrateCollections(
        FetchPath path,
        List<object> parentEntities,
        List<object> childEntities,
        ISessionImplementor sessionImpl)
    {
        var childrenByParent = _groupChildrenByParent(path, childEntities, sessionImpl);
        var roleName = $"{path.ParentEntityType.FullName}.{path.CollectionProperty.Name}";
        var collectionPersister = sessionImpl.Factory.GetCollectionPersister(roleName);

        foreach (var parent in parentEntities)
        {
            _hydrateParentCollection(parent, path, childrenByParent, collectionPersister, sessionImpl);
        }
    }

    private Dictionary<object, List<object>> _groupChildrenByParent(
        FetchPath path,
        List<object> childEntities,
        ISessionImplementor sessionImpl)
    {
        var grouped = new Dictionary<object, List<object>>();
        var parentIdProperty = ReflectionCache.GetIdProperty(path.ParentEntityType, sessionImpl);

        foreach (var child in childEntities)
        {
            var backRef = path.BackReferenceProperty.GetValue(child);
            if (backRef == null) continue;

            var parentId = parentIdProperty.GetValue(backRef);
            if (!grouped.ContainsKey(parentId))
                grouped[parentId] = new List<object>();

            grouped[parentId].Add(child);
        }

        return grouped;
    }

    private void _hydrateParentCollection(
        object parent,
        FetchPath path,
        Dictionary<object, List<object>> childrenByParent,
        ICollectionPersister collectionPersister,
        ISessionImplementor sessionImpl)
    {
        var parentIdProperty = ReflectionCache.GetIdProperty(path.ParentEntityType, sessionImpl);
        var parentId = parentIdProperty.GetValue(parent);
        
        var collectionKey = new CollectionKey(collectionPersister, parentId);
        var persistentCollection = _getPersistentCollection(collectionKey, parent, path, sessionImpl);
        
        if (persistentCollection == null) return;

        if (!persistentCollection.WasInitialized)
        {
            _initializeCollection(persistentCollection, path, parentId, childrenByParent, collectionPersister, sessionImpl);
        }
        else
        {
            _updateExistingCollection(persistentCollection, parentId, childrenByParent);
        }
    }

    private IPersistentCollection _getPersistentCollection(
        CollectionKey collectionKey,
        object parent,
        FetchPath path,
        ISessionImplementor sessionImpl)
    {
        var persistentCollection = sessionImpl.PersistenceContext.GetCollection(collectionKey) as IPersistentCollection;
        
        if (persistentCollection == null)
        {
            var tempCollection = path.CollectionProperty.GetValue(parent);
            persistentCollection = tempCollection as IPersistentCollection;
        }
        
        return persistentCollection;
    }

    private void _initializeCollection(
        IPersistentCollection persistentCollection,
        FetchPath path,
        object parentId,
        Dictionary<object, List<object>> childrenByParent,
        ICollectionPersister collectionPersister,
        ISessionImplementor sessionImpl)
    {
        var wrappedSet = _createAndPopulateWrappedSet(path, parentId, childrenByParent);
        _setWrappedSetField(persistentCollection, wrappedSet);
        _registerCollectionAsInitialized(persistentCollection, collectionPersister, parentId, sessionImpl);
        _callAfterInitialize(persistentCollection, collectionPersister);
    }

    private object _createAndPopulateWrappedSet(
        FetchPath path,
        object parentId,
        Dictionary<object, List<object>> childrenByParent)
    {
        var hashSetType = ReflectionCache.MakeGenericType(typeof(HashSet<>), path.ChildEntityType);
        var newSet = Activator.CreateInstance(hashSetType);
        
        if (childrenByParent.TryGetValue(parentId, out var children))
        {
            var addMethod = ReflectionCache.GetAddMethod(hashSetType);
            foreach (var child in children)
            {
                addMethod.Invoke(newSet, new[] { child });
            }
        }
        
        return newSet;
    }

    private void _setWrappedSetField(IPersistentCollection persistentCollection, object wrappedSet)
    {
        var collectionType = persistentCollection.GetType();
        var wrappedSetField = ReflectionCache.GetWrappedSetField(collectionType);
        wrappedSetField.SetValue(persistentCollection, wrappedSet);
    }

    private void _registerCollectionAsInitialized(
        IPersistentCollection persistentCollection,
        ICollectionPersister collectionPersister,
        object parentId,
        ISessionImplementor sessionImpl)
    {
        var collectionEntry = sessionImpl.PersistenceContext.GetCollectionEntry(persistentCollection);
        
        if (collectionEntry == null)
        {
            sessionImpl.PersistenceContext.AddInitializedCollection(collectionPersister, persistentCollection, parentId);
        }
    }

    private void _callAfterInitialize(IPersistentCollection persistentCollection, ICollectionPersister collectionPersister)
    {
        try
        {
            persistentCollection.AfterInitialize(collectionPersister);
        }
        catch
        {
            // Ignora eventuali errori - la collezione è comunque inizializzata
        }
    }

    private void _updateExistingCollection(
        IPersistentCollection persistentCollection,
        object parentId,
        Dictionary<object, List<object>> childrenByParent)
    {
        var collection = persistentCollection as ICollection;
        if (collection == null) return;

        var clearMethod = ReflectionCache.GetClearMethod(collection.GetType());
        clearMethod?.Invoke(collection, null);

        if (childrenByParent.TryGetValue(parentId, out var children))
        {
            var addMethod = ReflectionCache.GetAddMethod(collection.GetType());
            foreach (var child in children)
            {
                addMethod.Invoke(collection, new[] { child });
            }
        }
    }

    private void _hydrateSingleReferences(
        FetchPath path,
        List<object> parentEntities,
        List<object> childEntities,
        ISessionImplementor sessionImpl)
    {
        var childByParentId = _mapChildrenToParentIds(path, childEntities, sessionImpl);
        _assignChildrenToParents(path, parentEntities, childByParentId, sessionImpl);
    }

    private Dictionary<object, object> _mapChildrenToParentIds(
        FetchPath path,
        List<object> childEntities,
        ISessionImplementor sessionImpl)
    {
        var parentIdProperty = ReflectionCache.GetIdProperty(path.ParentEntityType, sessionImpl);
        var childByParentId = new Dictionary<object, object>();

        foreach (var child in childEntities)
        {
            var backRef = path.BackReferenceProperty.GetValue(child);
            if (backRef != null)
            {
                var parentId = parentIdProperty.GetValue(backRef);
                childByParentId[parentId] = child;
            }
        }

        return childByParentId;
    }

    private void _assignChildrenToParents(
        FetchPath path,
        List<object> parentEntities,
        Dictionary<object, object> childByParentId,
        ISessionImplementor sessionImpl)
    {
        var parentIdProperty = ReflectionCache.GetIdProperty(path.ParentEntityType, sessionImpl);

        foreach (var parent in parentEntities)
        {
            var parentId = parentIdProperty.GetValue(parent);
            if (childByParentId.TryGetValue(parentId, out var child))
            {
                path.CollectionProperty.SetValue(parent, child);
            }
        }
    }
}
