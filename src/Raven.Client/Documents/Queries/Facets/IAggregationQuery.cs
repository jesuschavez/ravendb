using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Raven.Client.Documents.Commands;

namespace Raven.Client.Documents.Queries.Facets
{
    public interface IAggregationQuery<T>
    {
        IAggregationQuery<T> AndAggregateBy(Action<IFacetFactory<T>> factory = null);
        IAggregationQuery<T> AndAggregateBy(FacetBase facet);
        Dictionary<string, FacetResult> Execute();
        Task<Dictionary<string, FacetResult>> ExecuteAsync();
        Lazy<Dictionary<string, FacetResult>> ExecuteLazy(Action<Dictionary<string, FacetResult>> onEval = null);
        Lazy<Task<Dictionary<string, FacetResult>>> ExecuteLazyAsync(Action<Dictionary<string, FacetResult>> onEval = null);
    }
}
