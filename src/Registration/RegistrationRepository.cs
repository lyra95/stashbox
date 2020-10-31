﻿using Stashbox.Configuration;
using Stashbox.Exceptions;
using Stashbox.Registration.Extensions;
using Stashbox.Registration.SelectionRules;
using Stashbox.Resolution;
using Stashbox.Utils;
using Stashbox.Utils.Data.Immutable;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stashbox.Registration
{
    internal class RegistrationRepository : IRegistrationRepository
    {
        private ImmutableTree<Type, ImmutableBucket<object, ServiceRegistration>> serviceRepository = ImmutableTree<Type, ImmutableBucket<object, ServiceRegistration>>.Empty;
        private readonly ContainerConfiguration containerConfiguration;

        private readonly IRegistrationSelectionRule[] filters =
        {
            RegistrationSelectionRules.GenericFilter,
            RegistrationSelectionRules.NameFilter,
            RegistrationSelectionRules.ScopeNameFilter,
            RegistrationSelectionRules.ConditionFilter
        };

        private readonly IRegistrationSelectionRule[] topLevelFilters =
        {
            RegistrationSelectionRules.GenericFilter,
            RegistrationSelectionRules.NameFilter,
            RegistrationSelectionRules.ScopeNameFilter
        };

        private readonly IRegistrationSelectionRule[] enumerableFilters =
        {
            RegistrationSelectionRules.GenericFilter,
            RegistrationSelectionRules.ScopeNameFilter,
            RegistrationSelectionRules.ConditionFilter
        };

        public RegistrationRepository(ContainerConfiguration containerConfiguration)
        {
            this.containerConfiguration = containerConfiguration;
        }

        public bool AddOrUpdateRegistration(ServiceRegistration registration, Type serviceType)
        {
            if (registration.RegistrationContext.ReplaceExistingRegistrationOnlyIfExists)
                return Swap.SwapValue(ref this.serviceRepository, (reg, type, t3, t4, repo) =>
                    repo.UpdateIfExists(type, regs => regs.ReplaceIfExists(reg.RegistrationDiscriminator, reg, false,
                        (old, @new) =>
                        {
                            @new.Replaces(old);
                            return @new;
                        })),
                    registration,
                    serviceType,
                    Constants.DelegatePlaceholder,
                    Constants.DelegatePlaceholder);

            return Swap.SwapValue(ref this.serviceRepository, (reg, type, newRepo, regBehavior, repo) =>
                repo.AddOrUpdate(type, newRepo,
                    (oldValue, newValue) =>
                    {
                        var allowUpdate = reg.RegistrationContext.ReplaceExistingRegistration ||
                                          regBehavior == Rules.RegistrationBehavior.ReplaceExisting;

                        if (!allowUpdate && regBehavior == Rules.RegistrationBehavior.PreserveDuplications)
                            return oldValue.Add(reg.RegistrationDiscriminator, reg);

                        return oldValue.AddOrUpdate(reg.RegistrationDiscriminator, reg, false,
                            (old, @new) =>
                            {
                                if (!allowUpdate && regBehavior == Rules.RegistrationBehavior.ThrowException)
                                    throw new ServiceAlreadyRegisteredException(old.ImplementationType);

                                if (!allowUpdate)
                                    return old;

                                @new.Replaces(old);
                                return @new;
                            });
                    }),
                    registration,
                    serviceType,
                    ImmutableBucket<object, ServiceRegistration>.Empty.Add(registration.RegistrationDiscriminator, registration),
                    this.containerConfiguration.RegistrationBehavior);
        }

        public bool AddOrReMapRegistration(ServiceRegistration registration, Type serviceType) =>
            registration.RegistrationContext.ReplaceExistingRegistrationOnlyIfExists 
                ? Swap.SwapValue(ref this.serviceRepository, (type, newRepo, t3, t4, repo) =>
                    repo.UpdateIfExists(type, newRepo), serviceType,
                    ImmutableBucket<object, ServiceRegistration>.Empty.Add(registration.RegistrationDiscriminator, registration),
                    Constants.DelegatePlaceholder,
                    Constants.DelegatePlaceholder)
                : Swap.SwapValue(ref this.serviceRepository, (type, newRepo, t3, t4, repo) =>
                    repo.AddOrUpdate(type, newRepo, true), serviceType,
                    ImmutableBucket<object, ServiceRegistration>.Empty.Add(registration.RegistrationDiscriminator, registration),
                    Constants.DelegatePlaceholder,
                    Constants.DelegatePlaceholder);            

        public bool ContainsRegistration(Type type, object name) =>
            serviceRepository.ContainsRegistration(type, name);

        public IEnumerable<KeyValuePair<Type, ServiceRegistration>> GetRegistrationMappings() =>
             serviceRepository.Walk().SelectMany(reg => reg.Value.Select(r => new KeyValuePair<Type, ServiceRegistration>(reg.Key, r)));

        public ServiceRegistration GetRegistrationOrDefault(Type type, ResolutionContext resolutionContext, object name = null) =>
            this.GetRegistrationsForType(type)?.SelectOrDefault(new TypeInformation(type, name), resolutionContext, this.topLevelFilters);

        public ServiceRegistration GetRegistrationOrDefault(TypeInformation typeInfo, ResolutionContext resolutionContext) =>
            this.GetRegistrationsForType(typeInfo.Type)?.SelectOrDefault(typeInfo, resolutionContext, this.filters);

        public IEnumerable<ServiceRegistration> GetRegistrationsOrDefault(TypeInformation typeInfo, ResolutionContext resolutionContext) =>
            this.GetRegistrationsForType(typeInfo.Type)?.FilterExclusiveOrDefault(typeInfo, resolutionContext, this.enumerableFilters)?.OrderBy(reg => reg.RegistrationId);

        private IEnumerable<ServiceRegistration> GetRegistrationsForType(Type type)
        {
            var registrations = serviceRepository.GetOrDefault(type);
            if (!type.IsClosedGenericType()) return registrations;

            var openGenerics = serviceRepository.GetOrDefault(type.GetGenericTypeDefinition());

            if (openGenerics == null) return registrations;
            return registrations == null ? openGenerics : openGenerics.Concat(registrations);
        }
    }
}
