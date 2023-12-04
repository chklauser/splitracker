using System;
using System.Diagnostics.CodeAnalysis;

namespace Splitracker.Persistence.Generic;

interface IRepositorySubscription : ISubscription
{
    event EventHandler? Added;
    event EventHandler? Deleted;
}

[SuppressMessage("ReSharper", "TypeParameterCanBeVariant")]
interface IRepositorySubscriptionBase<TValue, TDbModel>
{
    public static abstract TValue ToDomain(TDbModel model);
}