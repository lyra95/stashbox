﻿using Stashbox.Exceptions;
using Stashbox.Registration;
using Stashbox.Resolution;
using Stashbox.Utils;
using System;
using System.Linq.Expressions;

namespace Stashbox.Lifetime;

/// <summary>
/// Represents a scoped lifetime.
/// </summary>
public class ScopedLifetime : FactoryLifetimeDescriptor
{
    /// <inheritdoc />
    protected override int LifeSpan => 10;

    /// <inheritdoc />
    private protected override bool StoreResultInLocalVariable => true;

    /// <inheritdoc />
    protected override Expression ApplyLifetime(Func<IResolutionScope, IRequestContext, object> factory,
        ServiceRegistration serviceRegistration, ResolutionContext resolutionContext, Type resolveType)
    {
        if (resolutionContext.CurrentContainerContext.ContainerConfiguration.LifetimeValidationEnabled &&
            resolutionContext.IsRequestedFromRoot)
            throw new LifetimeValidationFailedException(serviceRegistration.ImplementationType,
                $"Resolution of {serviceRegistration.ImplementationType} ({this.Name}) from the root scope is not allowed, " +
                $"that would promote the service's lifetime to singleton.");

        return resolutionContext.CurrentScopeParameter.CallMethod(Constants.GetOrAddScopedObjectMethod,
                serviceRegistration.RegistrationId.AsConstant(),
                factory.AsConstant(),
                resolutionContext.RequestContextParameter,
                serviceRegistration.ImplementationType.AsConstant());
    }
}