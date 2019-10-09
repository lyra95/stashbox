﻿using Stashbox.Entity;
using Stashbox.Utils;
using System.Linq.Expressions;

namespace Stashbox.Resolution.Resolvers
{
    internal class ScopedInstanceResolver : IResolver
    {
        public Expression GetExpression(IContainerContext containerContext,
            IResolutionStrategy resolutionStrategy,
            TypeInformation typeInfo,
            ResolutionContext resolutionContext) =>
            resolutionContext.CurrentScopeParameter
                .CallMethod(Constants.GetScopedInstanceMethod,
                    typeInfo.Type.AsConstant(),
                    typeInfo.DependencyName.AsConstant())
                .ConvertTo(typeInfo.Type);

        public bool CanUseForResolution(IContainerContext containerContext, TypeInformation typeInfo, ResolutionContext resolutionContext) =>
            resolutionContext.ResolutionScope.HasScopedInstances &&
            resolutionContext.ResolutionScope.GetScopedInstanceOrDefault(typeInfo.Type, typeInfo.DependencyName) != null;
    }
}
