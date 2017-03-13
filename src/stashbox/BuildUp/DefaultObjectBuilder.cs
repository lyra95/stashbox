﻿using System;
using Stashbox.BuildUp.Expressions;
using Stashbox.Entity;
using Stashbox.Entity.Resolution;
using Stashbox.Exceptions;
using Stashbox.Infrastructure;
using Stashbox.Infrastructure.ContainerExtension;
using System.Linq.Expressions;

namespace Stashbox.BuildUp
{
    internal class DefaultObjectBuilder : IObjectBuilder
    {
        private readonly IContainerExtensionManager containerExtensionManager;
        private readonly IMetaInfoProvider metaInfoProvider;
        private readonly InjectionParameter[] injectionParameters;
        private readonly IContainerContext containerContext;
        private readonly IExpressionBuilder expressionBuilder;

        public DefaultObjectBuilder(IContainerContext containerContext, IMetaInfoProvider metaInfoProvider,
            IContainerExtensionManager containerExtensionManager, IExpressionBuilder expressionBuilder, InjectionParameter[] injectionParameters = null)
        {
            if (injectionParameters != null)
                this.injectionParameters = injectionParameters;

            this.containerExtensionManager = containerExtensionManager;
            this.metaInfoProvider = metaInfoProvider;
            this.containerContext = containerContext;
            this.expressionBuilder = expressionBuilder;
        }

        public Expression GetExpression(ResolutionInfo resolutionInfo, Type resolveType)
        {
            if (!this.containerContext.ContainerConfigurator.ContainerConfiguration.CircularDependencyTrackingEnabled)
                return this.GetExpressionInternal(resolutionInfo, resolveType);

            using (new CircularDependencyBarrier(resolutionInfo.CircularDependencyBarrier, this.metaInfoProvider.TypeTo))
                return this.GetExpressionInternal(resolutionInfo, resolveType);
        }

        public bool HandlesObjectDisposal => false;

        private Expression GetExpressionInternal(ResolutionInfo resolutionInfo, Type resolveType)
        {
            if (!this.metaInfoProvider.TryChooseConstructor(out ResolutionConstructor constructor, 
                resolutionInfo, this.injectionParameters))
                    throw new ResolutionFailedException(this.metaInfoProvider.TypeTo.FullName);
            return this.CreateExpression(constructor, resolutionInfo, resolveType);
        }

        private Expression CreateExpression(ResolutionConstructor constructor, ResolutionInfo resolutionInfo, Type resolveType)
        {
            return this.expressionBuilder.CreateExpression(this.containerExtensionManager, this.containerContext,
                    constructor, resolutionInfo, resolveType, this.injectionParameters,
                    this.metaInfoProvider.GetResolutionMembers(resolutionInfo, this.injectionParameters),
                    this.metaInfoProvider.GetResolutionMethods(resolutionInfo, this.injectionParameters));
        }

        public void CleanUp()
        { }
    }
}
