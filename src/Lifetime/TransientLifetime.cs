﻿using Stashbox.Registration;
using Stashbox.Resolution;
using System;
using System.Linq.Expressions;

namespace Stashbox.Lifetime;

/// <summary>
/// Represents a transient lifetime.
/// </summary>
public class TransientLifetime : ExpressionLifetimeDescriptor
{
    /// <inheritdoc />
    protected override Expression ApplyLifetime(Expression expression,
        ServiceRegistration serviceRegistration, ResolutionContext resolutionContext, TypeInformation typeInformation) =>
        expression;
}