﻿using Sendstorm.Infrastructure;
using Stashbox.Entity;
using Stashbox.Extensions;
using Stashbox.MetaInfo;
using System;

namespace Stashbox.Infrastructure
{
    public interface IContainerContext
    {
        IRegistrationRepository RegistrationRepository { get; }
        IStashboxContainer Container { get; }
        IResolutionStrategy ResolutionStrategy { get; }
        IMessagePublisher MessagePublisher { get; }
        ExtendedImmutableTree<MetaInfoCache> MetaInfoRepository { get; }
        ExtendedImmutableTree<Func<ResolutionInfo, object>> DelegateRepository { get; }
    }
}
