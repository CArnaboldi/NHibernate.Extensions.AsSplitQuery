namespace NHibernate.Extensions.AsSplitQuery;

using NHibernate;
using NHibernate.Engine;
using NHibernate.Persister.Entity;
using System;
using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// Thread-safe cache for reflection operations to improve performance.
/// Caches frequently used reflection operations like property lookups and method invocations.
/// </summary>
internal static class ReflectionCache
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo> _idPropertyCache = new();
    private static readonly ConcurrentDictionary<Type, FieldInfo> _wrappedSetFieldCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> _addMethodCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> _clearMethodCache = new();
    private static readonly ConcurrentDictionary<(Type, Type), MethodInfo> _queryMethodCache = new();
    private static readonly ConcurrentDictionary<(Type, Type), Type> _genericTypeCache = new();

    /// <summary>
    /// Gets the ID property for an entity type, using cache when available.
    /// </summary>
    public static PropertyInfo GetIdProperty(Type entityType, ISessionImplementor session)
    {
        return _idPropertyCache.GetOrAdd(entityType, type => _extractIdProperty(type, session));
    }

    /// <summary>
    /// Gets the WrappedSet field from a PersistentCollection type, using cache when available.
    /// </summary>
    public static FieldInfo GetWrappedSetField(Type collectionType)
    {
        return _wrappedSetFieldCache.GetOrAdd(collectionType, _extractWrappedSetField);
    }

    /// <summary>
    /// Gets the Add method for a collection type, using cache when available.
    /// </summary>
    public static MethodInfo GetAddMethod(Type collectionType)
    {
        return _addMethodCache.GetOrAdd(collectionType, _extractAddMethod);
    }

    /// <summary>
    /// Gets the Clear method for a collection type, using cache when available.
    /// </summary>
    public static MethodInfo GetClearMethod(Type collectionType)
    {
        return _clearMethodCache.GetOrAdd(collectionType, _extractClearMethod);
    }

    /// <summary>
    /// Gets the generic Query method from ISession, using cache when available.
    /// </summary>
    public static MethodInfo GetQueryMethod(Type entityType)
    {
        return _queryMethodCache.GetOrAdd((typeof(ISession), entityType), _extractQueryMethod);
    }

    /// <summary>
    /// Creates a generic type (e.g., HashSet&lt;T&gt;) using cache when available.
    /// </summary>
    public static Type MakeGenericType(Type genericTypeDefinition, Type typeArgument)
    {
        return _genericTypeCache.GetOrAdd((genericTypeDefinition, typeArgument), _createGenericType);
    }

    /// <summary>
    /// Clears all reflection caches. Useful for testing or when dealing with dynamic assemblies.
    /// </summary>
    public static void ClearAll()
    {
        _idPropertyCache.Clear();
        _wrappedSetFieldCache.Clear();
        _addMethodCache.Clear();
        _clearMethodCache.Clear();
        _queryMethodCache.Clear();
        _genericTypeCache.Clear();
    }

    private static PropertyInfo _extractIdProperty(Type entityType, ISessionImplementor session)
    {
        var persister = (AbstractEntityPersister)session.Factory.GetEntityPersister(entityType.FullName);
        var idPropertyName = persister.IdentifierPropertyName;
        return entityType.GetProperty(idPropertyName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Could not find ID property '{idPropertyName}' on type '{entityType.Name}'");
    }

    private static FieldInfo _extractWrappedSetField(Type collectionType)
    {
        return collectionType.GetField("WrappedSet", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Could not find WrappedSet field on type '{collectionType.Name}'");
    }

    private static MethodInfo _extractAddMethod(Type collectionType)
    {
        return collectionType.GetMethod("Add")
            ?? throw new InvalidOperationException($"Could not find Add method on type '{collectionType.Name}'");
    }

    private static MethodInfo _extractClearMethod(Type collectionType)
    {
        return collectionType.GetMethod("Clear")
            ?? throw new InvalidOperationException($"Could not find Clear method on type '{collectionType.Name}'");
    }

    private static MethodInfo _extractQueryMethod((Type sessionType, Type entityType) key)
    {
        var (sessionType, entityType) = key;
        var queryMethod = sessionType.GetMethod(nameof(ISession.Query), Type.EmptyTypes)
            ?? throw new InvalidOperationException("Could not find Query method on ISession");
        return queryMethod.MakeGenericMethod(entityType);
    }

    private static Type _createGenericType((Type generic, Type typeArg) key)
    {
        var (generic, typeArg) = key;
        return generic.MakeGenericType(typeArg);
    }
}
