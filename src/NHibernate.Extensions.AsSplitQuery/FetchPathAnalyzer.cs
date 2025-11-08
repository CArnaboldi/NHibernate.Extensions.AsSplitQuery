namespace NHibernate.Extensions.AsSplitQuery;

using NHibernate;
using NHibernate.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NHibernate.Extensions.AsSplitQuery.Models;

/// <summary>
/// Analyzes expression trees to extract fetch paths and their relationships.
/// </summary>
internal class FetchPathAnalyzer
{
    private readonly ISession _session;
    private readonly EntityMapper _entityMapper;

    private static readonly HashSet<string> FetchMethodNames = new()
    {
        nameof(EagerFetchingExtensionMethods.Fetch),
        nameof(EagerFetchingExtensionMethods.FetchMany),
        nameof(EagerFetchingExtensionMethods.ThenFetch),
        nameof(EagerFetchingExtensionMethods.ThenFetchMany)
    };

    public FetchPathAnalyzer(ISession session)
    {
        _session = session;
        _entityMapper = new EntityMapper(session);
    }

    /// <summary>
    /// Extracts all fetch paths from the expression tree.
    /// </summary>
    public List<FetchPath> ExtractFetchPaths(Expression expression)
    {
        var visitor = new FetchPathVisitor(_session, _entityMapper);
        visitor.Visit(expression);
        return visitor.GetFetchPaths();
    }

    private class FetchPathVisitor : ExpressionVisitor
    {
        private readonly ISession _session;
        private readonly EntityMapper _entityMapper;
        private readonly List<FetchPath> _fetchPaths = new();
        private readonly Stack<FetchPath> _pathStack = new();

        public FetchPathVisitor(ISession session, EntityMapper entityMapper)
        {
            _session = session;
            _entityMapper = entityMapper;
        }

        public List<FetchPath> GetFetchPaths() => _fetchPaths;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (!_isFetchMethod(node.Method))
                return base.VisitMethodCall(node);

            Visit(node.Arguments[0]);

            var fetchPath = _buildFetchPath(node);
            _fetchPaths.Add(fetchPath);
            _pathStack.Push(fetchPath);

            return node;
        }

        private bool _isFetchMethod(MethodInfo method)
        {
            return method.IsGenericMethod
                && FetchMethodNames.Contains(method.Name)
                && method.DeclaringType == typeof(EagerFetchingExtensionMethods);
        }

        private FetchPath _buildFetchPath(MethodCallExpression node)
        {
            var method = node.Method;
            var fetchedProperty = _extractFetchedProperty(node);
            var isThenFetch = method.Name.StartsWith("Then");
            var parentPath = _pathStack.Count > 0 ? _pathStack.Peek() : null;

            if (isThenFetch && parentPath == null)
                throw new InvalidOperationException("ThenFetch/ThenFetchMany must follow a Fetch/FetchMany");

            var parentType = _determineParentType(method, isThenFetch, parentPath);
            var childType = _determineChildType(fetchedProperty);

            return new FetchPath
            {
                IsCollection = method.Name.Contains("Many"),
                CollectionProperty = fetchedProperty,
                ChildEntityType = childType,
                ParentEntityType = parentType,
                ParentPath = isThenFetch ? parentPath : null,
                Depth = isThenFetch && parentPath != null ? parentPath.Depth + 1 : 0,
                BackReferenceProperty = _entityMapper.FindBackReferenceProperty(parentType, fetchedProperty, childType)
            };
        }

        private PropertyInfo _extractFetchedProperty(MethodCallExpression node)
        {
            var quote = (UnaryExpression)node.Arguments[1];
            var fetchLambda = (LambdaExpression)quote.Operand;
            var memberExpr = _getMemberExpression(fetchLambda);
            return (PropertyInfo)memberExpr.Member;
        }

        private MemberExpression _getMemberExpression(LambdaExpression lambda)
        {
            if (lambda.Body is MemberExpression memberExpr) 
                return memberExpr;
            
            if (lambda.Body is UnaryExpression unaryExpr && unaryExpr.Operand is MemberExpression innerMemberExpr)
                return innerMemberExpr;
            
            throw new NotSupportedException("AsSplitQuery fetch expression must be a simple property access.");
        }

        private Type _determineParentType(MethodInfo method, bool isThenFetch, FetchPath? parentPath)
        {
            if (isThenFetch)
            {
                if (parentPath == null)
                    throw new InvalidOperationException("ThenFetch requires a parent path");
                return parentPath.ChildEntityType;
            }
            return method.GetGenericArguments()[0];
        }

        private Type _determineChildType(PropertyInfo fetchedProperty)
        {
            var propertyType = fetchedProperty.PropertyType;
            
            if (typeof(IEnumerable).IsAssignableFrom(propertyType) && propertyType.IsGenericType)
                return propertyType.GetGenericArguments()[0];
            
            return propertyType;
        }
    }
}
