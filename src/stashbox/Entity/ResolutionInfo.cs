﻿using Stashbox.Infrastructure;
using Stashbox.Utils;
using System;
using System.Linq.Expressions;

namespace Stashbox.Entity
{
    /// <summary>
    /// Represents information about the actual resolution flow.
    /// </summary>
    public class ResolutionInfo
    {
        /// <summary>
        /// Static factory for <see cref="ResolutionInfo"/>.
        /// </summary>
        /// <returns>A new <see cref="ResolutionInfo"/> instance.</returns>
        public static ResolutionInfo New(IResolutionScope scope, bool nullResultAllowed = false) =>
            new ResolutionInfo(scope, nullResultAllowed);

        /// <summary>
        /// The extra parameter expressions.
        /// </summary>
        public ParameterExpression[] ParameterExpressions { get; set; }

        /// <summary>
        /// True if null result is allowed, otherwise false.
        /// </summary>
        public bool NullResultAllowed { get; }

        /// <summary>
        /// The name of the currently resolving scope.
        /// </summary>
        public object CurrentScopeName { get; }

        private AvlTree<int> circularDependencyBarrier;

        private AvlTree<Expression> expressionOverrides;

        private AvlTree<Type> currentlyDecoratingTypes;

        internal IResolutionScope ResolutionScope { get; }

        internal IResolutionScope RootScope { get; }

        internal IContainerContext ChildContext { get; }
        
        internal ResolutionInfo(IResolutionScope scope, bool nullResultAllowed)
            : this(scope, AvlTree<int>.Empty, AvlTree<Expression>.Empty, AvlTree<Type>.Empty, null, null, nullResultAllowed, scope.Name)
        {
        }
        
        private ResolutionInfo(IResolutionScope scope, AvlTree<int> circularDependencyBarrier, AvlTree<Expression> expressionOverrides,
            AvlTree<Type> currentlyDecoratingTypes, ParameterExpression[] parameterExpressions, IContainerContext childContext, bool nullResultAllowed, object scopeName)
        {
            this.circularDependencyBarrier = circularDependencyBarrier;
            this.expressionOverrides = expressionOverrides;
            this.currentlyDecoratingTypes = currentlyDecoratingTypes;
            this.NullResultAllowed = nullResultAllowed;
            this.ResolutionScope = scope;
            this.RootScope = scope.RootScope;
            this.CurrentScopeName = scopeName;
            this.ParameterExpressions = parameterExpressions;
            this.ChildContext = childContext;
        }

        internal bool IsCurrentlyDecorating(Type type) =>
            this.currentlyDecoratingTypes.GetOrDefault(type.GetHashCode()) != null;

        internal void AddCurrentlyDecoratingType(Type type)
        {
            this.currentlyDecoratingTypes = this.currentlyDecoratingTypes.AddOrUpdate(type.GetHashCode(), type);
        }

        internal void ClearCurrentlyDecoratingType(Type type)
        {
            this.currentlyDecoratingTypes = this.currentlyDecoratingTypes.AddOrUpdate(type.GetHashCode(), null, (oldValue, newValue) => newValue);
        }

        internal Expression GetExpressionOverrideOrDefault(Type type) =>
            this.expressionOverrides.GetOrDefault(type.GetHashCode());

        internal void SetExpressionOverride(Type type, Expression expression)
        {
            this.expressionOverrides = this.expressionOverrides.AddOrUpdate(type.GetHashCode(), expression, (oldValue, newValue) => newValue);
        }

        internal void AddCircularDependencyCheck(int regNumber, out bool updated)
        {
            this.circularDependencyBarrier = this.circularDependencyBarrier.AddOrUpdate(regNumber, regNumber, out updated);
        }

        internal void ClearCircularDependencyCheck(int regNumber)
        {
            this.circularDependencyBarrier = this.circularDependencyBarrier.AddOrUpdate(regNumber, 0, (oldValue, newValue) => newValue);
        }

        internal ResolutionInfo CreateNew(IContainerContext childContext = null, object scopeName = null) =>
            new ResolutionInfo(this.ResolutionScope, this.circularDependencyBarrier, this.expressionOverrides,
                this.currentlyDecoratingTypes, this.ParameterExpressions, childContext ?? this.ChildContext, this.NullResultAllowed, scopeName ?? this.CurrentScopeName);
    }
}