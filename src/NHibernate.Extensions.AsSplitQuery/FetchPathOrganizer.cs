using System.Collections.Generic;
using System.Linq;
using NHibernate.Extensions.AsSplitQuery.Models;

namespace NHibernate.Extensions.AsSplitQuery;

/// <summary>
/// Organizes fetch paths into execution levels based on their depth.
/// This ensures that parent entities are loaded before their children.
/// </summary>
internal class FetchPathOrganizer
{
    /// <summary>
    /// Organizes fetch paths into levels based on depth.
    /// Level 0 contains root-level fetches, Level 1 contains their children, etc.
    /// </summary>
    public List<List<FetchPath>> OrganizeByLevel(List<FetchPath> paths)
    {
        return paths
            .GroupBy(p => p.Depth)
            .OrderBy(g => g.Key)
            .Select(g => g.ToList())
            .ToList();
    }

    /// <summary>
    /// Finds all parent entities for a given fetch path.
    /// </summary>
    public List<object> FindParentEntities(
        FetchPath path,
        List<object> rootResults,
        Dictionary<object, object> allEntities)
    {
        return path.ParentPath == null
            ? rootResults
            : allEntities.Values.Where(e => e.GetType() == path.ParentEntityType).ToList();
    }
}
