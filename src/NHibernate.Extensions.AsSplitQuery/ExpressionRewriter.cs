using NHibernate.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace NHibernate.Extensions.AsSplitQuery;

/// <summary>
/// Handles expression tree manipulation for removing fetch operations.
/// </summary>
internal class ExpressionRewriter
{
    private static readonly HashSet<string> FetchMethodNames = new()
    {
        nameof(EagerFetchingExtensionMethods.Fetch),
        nameof(EagerFetchingExtensionMethods.FetchMany),
        nameof(EagerFetchingExtensionMethods.ThenFetch),
        nameof(EagerFetchingExtensionMethods.ThenFetchMany)
    };

    /// <summary>
    /// Removes all fetch operations from the expression tree.
    /// </summary>
    public Expression StripFetchOperations(Expression expression)
    {
        var visitor = new FetchStrippingVisitor();
        return visitor.Visit(expression);
    }

    private class FetchStrippingVisitor : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (_isFetchMethod(node.Method))
                return Visit(node.Arguments[0]);
            
            return base.VisitMethodCall(node);
        }

        private bool _isFetchMethod(System.Reflection.MethodInfo method)
        {
            return method.IsGenericMethod 
                && FetchMethodNames.Contains(method.Name)
                && method.DeclaringType == typeof(EagerFetchingExtensionMethods);
        }
    }
}
