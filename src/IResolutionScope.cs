﻿using Stashbox.Configuration;
using System;
using System.Collections.Generic;

namespace Stashbox
{
    /// <summary>
    /// Represents a resolution scope.
    /// </summary>
    public interface IResolutionScope : IDependencyResolver
    {
        /// <summary>
        /// The parent scope.
        /// </summary>
        IResolutionScope ParentScope { get; }

        /// <summary>
        /// The name of the scope, if it's null then it's a regular nameless scope.
        /// </summary>
        object Name { get; }

        /// <summary>
        /// Adds a service for further disposable tracking.
        /// </summary>
        /// <typeparam name="TDisposable">The type parameter.</typeparam>
        /// <param name="disposable">The <see cref="IDisposable"/> object.</param>
        /// <returns>The <see cref="IDisposable"/> object.</returns>
        TDisposable AddDisposableTracking<TDisposable>(TDisposable disposable);

        /// <summary>
        /// Adds a service with a cleanup delegate.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="finalizable">The object to cleanup.</param>
        /// <param name="finalizer">The cleanup delegate.</param>
        /// <returns>The object to cleanup.</returns>
        TService AddWithFinalizer<TService>(TService finalizable, Action<TService> finalizer);

        /// <summary>
        /// Returns an existing scoped object or adds it into the scope if it doesn't exist.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="factory">The value factory used to create the object if it doesn't exist yet.</param>
        /// <param name="requestedType">The type of the requested service.</param>
        /// <param name="validateLifetimeFromRootScope">If true, and if the lifetime validation is enabled the scope will validate whether the requested scoped service is resolved from root or not.</param>
        /// <returns>The scoped object.</returns>
        object GetOrAddScopedObject(int key, Func<IResolutionScope, object> factory, Type requestedType, bool validateLifetimeFromRootScope = false);

        /// <summary>
        /// Invalidates the delegate cache.
        /// </summary>
        void InvalidateDelegateCache();

        /// <summary>
        /// Gets the names of the already opened scopes.
        /// </summary>
        /// <returns>The scope names.</returns>
        IEnumerable<object> GetActiveScopeNames();

        /// <summary>
        /// Called by every node of the internal graph when the <see cref="ContainerConfiguration.RuntimeCircularDependencyTrackingEnabled"/> is true.
        /// Checks for runtime circular dependencies in the compiled delegates.
        /// </summary>
        /// <param name="key">The key of the dependency.</param>
        /// <param name="type">The type of the dependency.</param>
        void CheckRuntimeCircularDependencyBarrier(int key, Type type);

        /// <summary>
        /// Called by every node of the internal graph when the <see cref="ContainerConfiguration.RuntimeCircularDependencyTrackingEnabled"/> is true.
        /// Resets the runtime circular dependency checks state for a node.
        /// </summary>
        /// <param name="key"></param>
        void ResetRuntimeCircularDependencyBarrier(int key);
    }
}
