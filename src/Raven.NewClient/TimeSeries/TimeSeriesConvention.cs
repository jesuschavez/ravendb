using System;
using System.Net.Http;
using System.Threading.Tasks;
using Raven.NewClient.Abstractions.Connection;
using Raven.NewClient.Abstractions.Replication;

namespace Raven.NewClient.Client.TimeSeries
{
    /// <summary>
    /// The set of conventions used by the <see cref="TimeSeriesConvention"/> which allow the users to customize
    /// the way the Raven client API behaves
    /// </summary>
    public class TimeSeriesConvention : ConventionBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimeSeriesConvention"/> class.
        /// </summary>
        public TimeSeriesConvention()
        {
            FailoverBehavior = FailoverBehavior.AllowReadsFromSecondaries;
            AllowMultipleAsyncOperations = true;
            ShouldCacheRequest = url => true;
        }
    }
}