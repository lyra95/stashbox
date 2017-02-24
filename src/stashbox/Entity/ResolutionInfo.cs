﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Stashbox.Entity
{
    /// <summary>
    /// Represents information about the actual resolution flow.
    /// </summary>
    public class ResolutionInfo
    {
        internal static ResolutionInfo Empty = new ResolutionInfo();

        /// <summary>
        /// The extra parameter expressions.
        /// </summary>
        public ParameterExpression[] ParameterExpressions { get; set; }

        internal ISet<Type> CircularDependencyBarrier { get; }

        internal ResolutionInfo()
        {
            this.CircularDependencyBarrier = new HashSet<Type>();
        }
    }
}