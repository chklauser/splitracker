using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Raven.Client.Documents.Session;

namespace Splitracker.Persistence.Generic;

interface IRepositorySubscription : ISubscription
{
    event EventHandler? Added;
    event EventHandler? Deleted;
}

[SuppressMessage("ReSharper", "TypeParameterCanBeVariant")]
interface IRepositorySubscriptionBase<TValue, TDbModel>
{
    public static abstract Task<IEnumerable<TValue>> ToDomainAsync(
        IAsyncDocumentSession session,
        IReadOnlyList<TDbModel> models
    );
}