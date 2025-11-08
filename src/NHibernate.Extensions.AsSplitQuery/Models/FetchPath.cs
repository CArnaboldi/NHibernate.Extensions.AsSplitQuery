namespace NHibernate.Extensions.AsSplitQuery.Models;

using System;
using System.Reflection;
/// <summary>
/// Represents a fetch path in the query expression tree.
/// Contains information about the parent-child relationship and how to load the data.
/// </summary>
internal class FetchPath
{
    /// <summary>
    /// Indicates if this fetch path loads a collection (true) or a single reference (false).
    /// </summary>
    public bool IsCollection { get; set; }

    /// <summary>
    /// The type of the parent entity that contains the collection or reference.
    /// </summary>
    public Type ParentEntityType { get; set; }

    /// <summary>
    /// The type of the child entity being fetched.
    /// </summary>
    public Type ChildEntityType { get; set; }

    /// <summary>
    /// The property on the parent entity that holds the collection or reference.
    /// </summary>
    public PropertyInfo CollectionProperty { get; set; }

    /// <summary>
    /// The property on the child entity that references back to the parent (foreign key).
    /// </summary>
    public PropertyInfo BackReferenceProperty { get; set; }

    /// <summary>
    /// The parent fetch path if this is a nested fetch (ThenFetch).
    /// Null for root-level fetches.
    /// </summary>
    public FetchPath ParentPath { get; set; }

    /// <summary>
    /// The depth level of this fetch path in the hierarchy.
    /// 0 for root-level fetches, incremented for each ThenFetch.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// List of additional properties to fetch on the child entities.
    /// Used for nested fetch chains.
    /// </summary>
    public List<PropertyInfo> NestedFetches { get; set; } = new List<PropertyInfo>();
}
