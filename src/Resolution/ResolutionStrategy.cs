﻿using Stashbox.Expressions;
using Stashbox.Registration;
using Stashbox.Resolution.Extensions;
using Stashbox.Utils;
using Stashbox.Utils.Data.Immutable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Stashbox.Resolution
{
    internal class ResolutionStrategy : IResolutionStrategy
    {
        private readonly ExpressionBuilder expressionBuilder;
        private readonly ServiceRegistrator serviceRegistrator;
        private ImmutableBucket<IResolver> resolverRepository = ImmutableBucket<IResolver>.Empty;
        private ImmutableBucket<IResolver> lastChanceResolverRepository = ImmutableBucket<IResolver>.Empty;

        public ResolutionStrategy(ExpressionBuilder expressionBuilder, ServiceRegistrator serviceRegistrator)
        {
            this.expressionBuilder = expressionBuilder;
            this.serviceRegistrator = serviceRegistrator;
        }

        public Expression BuildExpressionForType(ResolutionContext resolutionContext, TypeInformation typeInformation)
        {
            if (typeInformation.Type == Constants.ResolverType)
                return resolutionContext.CurrentScopeParameter;

#if HAS_SERVICEPROVIDER
            if (typeInformation.Type == Constants.ServiceProviderType)
                return resolutionContext.CurrentScopeParameter;
#endif
            if (resolutionContext.ParameterExpressions.Length > 0)
            {
                var type = typeInformation.Type;
                var length = resolutionContext.ParameterExpressions.Length;
                for (var i = length; i-- > 0;)
                {
                    var parameters = resolutionContext.ParameterExpressions[i]
                        .WhereOrDefault(p => p.I2.Type == type ||
                                             p.I2.Type.Implements(type));

                    if (parameters == null) continue;
                    var selected =
                        parameters.FirstOrDefault(parameter => !parameter.I1) ?? parameters[parameters.Length - 1];
                    selected.I1 = true;
                    return selected.I2;
                }
            }

            var decorators = resolutionContext.Decorators.GetOrDefault(typeInformation.Type, false);
            if (decorators != null)
                return this.BuildExpressionForDecorator(decorators.Pop(),
                    resolutionContext.BeginDecoratingContext(typeInformation.Type, decorators), typeInformation.Type, decorators);

            var exprOverride = resolutionContext.GetExpressionOverrideOrDefault(typeInformation.Type, typeInformation.DependencyName);
            if (exprOverride != null)
                return exprOverride;

            var registration = resolutionContext
                .CurrentContainerContext
                .RegistrationRepository
                .GetRegistrationOrDefault(typeInformation, resolutionContext);

            return registration != null
                ? this.BuildExpressionForRegistration(registration, resolutionContext, typeInformation)
                : this.BuildResolutionExpressionUsingResolvers(typeInformation, resolutionContext);
        }

        public IEnumerable<Expression> BuildExpressionsForEnumerableRequest(ResolutionContext resolutionContext, TypeInformation typeInformation)
        {
            var registrations = resolutionContext
                .CurrentContainerContext
                .RegistrationRepository
                .GetRegistrationsOrDefault(typeInformation, resolutionContext);

            if (registrations == null)
                return this.BuildAllResolverExpressionsUsingResolvers(typeInformation, resolutionContext);

            return registrations.Select(reg =>
            {
                var decorators = resolutionContext.Decorators.GetOrDefault(typeInformation.Type, false);
                if (decorators == null)
                    return this.BuildExpressionForRegistration(reg, resolutionContext, typeInformation);

                decorators.ReplaceBack(reg);
                return this.BuildExpressionForDecorator(decorators.Pop(),
                    resolutionContext.BeginDecoratingContext(typeInformation.Type, decorators), typeInformation.Type,
                    decorators);
            });
        }

        public Expression BuildExpressionForTopLevelRequest(Type type, object name, ResolutionContext resolutionContext)
        {
            if (type == Constants.ResolverType)
                return resolutionContext.CurrentScopeParameter;

#if HAS_SERVICEPROVIDER
            if (type == Constants.ServiceProviderType)
                return resolutionContext.CurrentScopeParameter;
#endif

            var exprOverride = resolutionContext.GetExpressionOverrideOrDefault(type, name);
            if (exprOverride != null)
                return exprOverride;

            var registration = resolutionContext
                .CurrentContainerContext
                .RegistrationRepository
                .GetRegistrationOrDefault(type, resolutionContext, name);

            return registration != null
                ? this.BuildExpressionForRegistration(registration, resolutionContext, new TypeInformation(type, name))
                : this.BuildResolutionExpressionUsingResolvers(new TypeInformation(type, name), resolutionContext);
        }

        public Expression BuildExpressionForRegistration(ServiceRegistration serviceRegistration,
            ResolutionContext resolutionContext, TypeInformation typeInformation)
        {
            var requestedType = typeInformation.Type;
            if (serviceRegistration.RegistrationType == RegistrationType.OpenGeneric)
                serviceRegistration = this.PrepareOpenGenericRegistration(serviceRegistration, resolutionContext, requestedType);

            var decorators = resolutionContext
                .CurrentContainerContext
                .DecoratorRepository
                .GetDecoratorsOrDefault(serviceRegistration.ImplementationType, typeInformation, resolutionContext);

            if (decorators == null)
                return this.expressionBuilder.BuildExpressionAndApplyLifetime(serviceRegistration, resolutionContext, requestedType);

            var stack = decorators.AsStack();
            stack.PushBack(serviceRegistration);
            return this.BuildExpressionForDecorator(stack.Pop(),
                resolutionContext.BeginDecoratingContext(requestedType, stack), requestedType, stack);
        }

        public Expression BuildResolutionExpressionUsingResolvers(TypeInformation typeInfo, ResolutionContext resolutionContext)
        {
            var expression = this.resolverRepository.BuildResolutionExpression(typeInfo, resolutionContext, this);
            if (expression != null) return expression;

            return this.lastChanceResolverRepository.BuildResolutionExpression(typeInfo,
                resolutionContext, this);
        }

        public bool CanResolveType(TypeInformation typeInfo, ResolutionContext resolutionContext) =>
            this.resolverRepository.CanResolve(typeInfo, resolutionContext) ||
            this.lastChanceResolverRepository.CanResolve(typeInfo, resolutionContext);

        public void RegisterResolver(IResolver resolver) =>
            Swap.SwapValue(ref this.resolverRepository, (t1, t2, t3, t4, repo) =>
               repo.Add(t1), resolver, Constants.DelegatePlaceholder, Constants.DelegatePlaceholder, Constants.DelegatePlaceholder);

        public void RegisterLastChanceResolver(IResolver resolver) =>
            Swap.SwapValue(ref this.lastChanceResolverRepository, (t1, t2, t3, t4, repo) =>
                repo.Add(t1), resolver, Constants.DelegatePlaceholder, Constants.DelegatePlaceholder, Constants.DelegatePlaceholder);

        private IEnumerable<Expression> BuildAllResolverExpressionsUsingResolvers(TypeInformation typeInfo,
            ResolutionContext resolutionContext) =>
            this.resolverRepository.BuildAllResolutionExpressions(typeInfo, resolutionContext, this) ??
            this.lastChanceResolverRepository.BuildAllResolutionExpressions(typeInfo, resolutionContext, this);

        private Expression BuildExpressionForDecorator(ServiceRegistration serviceRegistration,
            ResolutionContext resolutionContext, Type requestedType, Utils.Data.Stack<ServiceRegistration> decorators)
        {
            if (serviceRegistration.RegistrationType == RegistrationType.OpenGeneric)
                serviceRegistration = this.PrepareOpenGenericRegistration(serviceRegistration, resolutionContext, requestedType);

            return this.expressionBuilder.BuildExpressionAndApplyLifetime(serviceRegistration, resolutionContext,
                requestedType, decorators.PeekBack()?.RegistrationContext.Lifetime);
        }

        private ServiceRegistration PrepareOpenGenericRegistration(ServiceRegistration serviceRegistration,
            ResolutionContext resolutionContext, Type requestedType)
        {
            var genericType = serviceRegistration.ImplementationType.MakeGenericType(requestedType.GetGenericArguments());
            var newRegistration = serviceRegistration.Clone(genericType, RegistrationType.Default);
            newRegistration.RegistrationContext.Name = null;

            this.serviceRegistrator.Register(resolutionContext.CurrentContainerContext, newRegistration, requestedType, false);
            return newRegistration;
        }
    }
}
