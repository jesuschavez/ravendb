// -----------------------------------------------------------------------
//  <copyright file="DatabaseSmugglerRemoteDestination.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Raven.Abstractions.Database.Smuggler.Database;
using Raven.Abstractions.Exceptions;
using Raven.Abstractions.Extensions;
using Raven.Abstractions.Util;
using Raven.Client.Document;
using Raven.Json.Linq;
using Raven.Smuggler.Helpers;

namespace Raven.Smuggler.Database.Remote
{
    public class DatabaseSmugglerRemoteDestination : DatabaseSmugglerRemoteBase, IDatabaseSmugglerDestination
    {
        private DocumentStore _store;

        private readonly DatabaseSmugglerRemoteDestinationOptions _options;

        private DatabaseSmugglerNotifications _notifications;

        private DatabaseSmugglerOptions _globalOptions;

        private readonly bool _ownsStore;

        private readonly Func<DocumentStore> _storeFactory;

        public DatabaseSmugglerRemoteDestination(DocumentStore store, DatabaseSmugglerRemoteDestinationOptions options = null)
        {
            _options = options ?? new DatabaseSmugglerRemoteDestinationOptions();

            _storeFactory = () =>
            {
                store.JsonRequestFactory.DisableRequestCompression = _options.DisableCompression; // TODO [ppekrol] should it be reverted to the original value?
                return store;
            };
            
            _ownsStore = false;
        }

        public DatabaseSmugglerRemoteDestination(DatabaseSmugglerRemoteConnectionOptions connectionOptions, DatabaseSmugglerRemoteDestinationOptions options = null)
        {
            _options = options ?? new DatabaseSmugglerRemoteDestinationOptions();

            _storeFactory = () =>
            {
                var store = new DocumentStore
                {
                    ApiKey = connectionOptions.ApiKey,
                    DefaultDatabase = connectionOptions.Database,
                    Url = connectionOptions.Url,
                    Credentials = connectionOptions.Credentials
                };

                if (string.IsNullOrWhiteSpace(connectionOptions.ConnectionStringName) == false)
                    store.ConnectionStringName = connectionOptions.ConnectionStringName;

                return store;
            };

            _ownsStore = true;
        }

        public void Dispose()
        {
            if (_ownsStore)
                _store?.Dispose();
        }

        public bool SupportsOperationState => true;

        public bool SupportsWaitingForIndexing => true;

        public async Task InitializeAsync(DatabaseSmugglerOptions options, DatabaseSmugglerNotifications notifications, CancellationToken cancellationToken)
        {
            _globalOptions = options;
            _notifications = notifications;
            _store = _storeFactory();

            if (_ownsStore)
                _store.Initialize(ensureDatabaseExists: false);

            _store.JsonRequestFactory.DisableRequestCompression = _options.DisableCompression;

            await ServerValidation.ValidateThatServerIsUpAndDatabaseExistsAsync(_store, cancellationToken).ConfigureAwait(false);

            await InitializeBatchSizeAsync(_store, _globalOptions).ConfigureAwait(false);
        }

        public IDatabaseSmugglerIndexActions IndexActions()
        {
            return new DatabaseSmugglerRemoteIndexActions(_store);
        }

        public IDatabaseSmugglerDocumentActions DocumentActions()
        {
            return new DatabaseSmugglerRemoteDocumentActions(_globalOptions, _options, _notifications, _store);
        }

        public IDatabaseSmugglerTransformerActions TransformerActions()
        {
            return new DatabaseSmugglerRemoteTransformerActions(_store);
        }

        public IDatabaseSmugglerDocumentDeletionActions DocumentDeletionActions()
        {
            return new DatabaseSmugglerRemoteDocumentDeletionActions(_store);
        }

        public IDatabaseSmugglerIdentityActions IdentityActions()
        {
            return new DatabaseSmugglerRemoteIdentityActions(_store);
        }

        public async Task<DatabaseSmugglerOperationState> LoadOperationStateAsync(DatabaseSmugglerOptions options, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.ContinuationToken) == false)
            {
                var continuationDocId = "Raven/Smuggler/Continuation/" + _options.ContinuationToken;

                try
                {
                    var continuationDocument = await _store
                        .AsyncDatabaseCommands
                        .GetAsync(continuationDocId, cancellationToken)
                        .ConfigureAwait(false);

                    if (continuationDocument != null)
                        return continuationDocument.DataAsJson.JsonDeserialization<DatabaseSmugglerOperationState>();
                }
                catch (Exception e)
                {
                    if (options.IgnoreErrorsAndContinue == false)
                        throw;

                    _notifications.ShowProgress("Failed loading continuation state. Message: {0}", e.Message);
                }
            }

            return null;
        }

        public async Task SaveOperationStateAsync(DatabaseSmugglerOptions options, DatabaseSmugglerOperationState state, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.ContinuationToken) == false)
            {
                var continuationDocId = "Raven/Smuggler/Continuation/" + _options.ContinuationToken;

                try
                {
                    await _store
                        .AsyncDatabaseCommands
                        .PutAsync(continuationDocId, null, RavenJObject.FromObject(state), null, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    if (options.IgnoreErrorsAndContinue == false)
                        throw;

                    _notifications.ShowProgress("Failed saving continuation state. Message: {0}", e.Message);
                }
            }
        }

        public async Task WaitForIndexingAsOfLastWriteAsync(CancellationToken cancellationToken)
        {
            if (_options.WaitForIndexing == false)
                return;

            var stopwatch = Stopwatch.StartNew();
            var justIndexingWait = Stopwatch.StartNew();

            var stats = await _store
                .AsyncDatabaseCommands
                .GetStatisticsAsync(cancellationToken)
                .ConfigureAwait(false);

            var tries = 0;
            var cutOffEtag = stats.LastDocEtag;
            while (true)
            {
                if (stats.Indexes.All(x => x.LastIndexedEtag.CompareTo(cutOffEtag) >= 0))
                {
                    _notifications.ShowProgress("\rWaited {0} for indexing ({1} total).", justIndexingWait.Elapsed, stopwatch.Elapsed);
                    break;
                }

                if (tries++ % 10 == 0)
                    _notifications.ShowProgress("\rWaiting {0} for indexing ({1} total).", justIndexingWait.Elapsed, stopwatch.Elapsed);

                Thread.Sleep(1000);
                stats = await _store
                    .AsyncDatabaseCommands
                    .GetStatisticsAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            stopwatch.Stop();
            justIndexingWait.Stop();
        }

        public Task AfterExecuteAsync(DatabaseSmugglerOperationState state)
        {
            return new CompletedTask();
        }

        public void OnException(SmugglerException exception)
        {
        }
    }
}
