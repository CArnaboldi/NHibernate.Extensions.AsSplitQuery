namespace NHibernate.Extensions.AsSplitQuery;

using NHibernate;
using NHibernate.Impl;
using NHibernate.Persister.Collection;
using NHibernate.Persister.Entity;
using System;
using System.Linq;
using System.Reflection;

/// <summary>
/// Maps NHibernate metadata to find relationships between entities.
/// Handles the discovery of back-reference properties for collections.
/// </summary>
internal class EntityMapper
{
    private readonly ISession _session;

    public EntityMapper(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Finds the back-reference property on a child entity that points to the parent.
    /// </summary>
    public PropertyInfo FindBackReferenceProperty(Type parentType, PropertyInfo collectionProperty, Type childType)
    {
        var sessionFactory = (SessionFactoryImpl)_session.SessionFactory;
        var fkColumnName = _getCollectionKeyColumnName(sessionFactory, parentType, collectionProperty);
        return _findPropertyByColumnName(sessionFactory, childType, fkColumnName);
    }

    private string _getCollectionKeyColumnName(SessionFactoryImpl sessionFactory, Type parentType, PropertyInfo collectionProperty)
    {
        var roleName = $"{parentType.FullName}.{collectionProperty.Name}";
        var collectionPersister = (AbstractCollectionPersister)sessionFactory.GetCollectionMetadata(roleName);

        if (collectionPersister == null)
            throw new InvalidOperationException(
                $"Could not find collection metadata for '{parentType.Name}.{collectionProperty.Name}'. Is it mapped as a collection?");

        if (collectionPersister.KeyColumnNames.Length > 1)
            throw new NotSupportedException("AsSplitQuery does not support composite foreign keys for collections.");

        return collectionPersister.KeyColumnNames[0];
    }

    private PropertyInfo _findPropertyByColumnName(SessionFactoryImpl sessionFactory, Type entityType, string columnName)
    {
        var persister = (AbstractEntityPersister)sessionFactory.GetEntityPersister(entityType.FullName);

        foreach (var propertyName in persister.PropertyNames)
        {
            var propertyColumnNames = persister.GetPropertyColumnNames(propertyName);
            if (propertyColumnNames.Contains(columnName, StringComparer.OrdinalIgnoreCase))
            {
                return entityType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            }
        }

        throw new InvalidOperationException(
            $"Could not find a property on entity '{entityType.Name}' that maps to the foreign key column '{columnName}'.");
    }
}
