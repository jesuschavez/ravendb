﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Raven.Client.Documents.Subscriptions;
using Raven.Client.Exceptions.Documents.Subscriptions;
using Raven.Server.Documents;
using Raven.Server.ServerWide.Context;
using Raven.Tests.Core.Utils.Entities;
using Sparrow;
using Xunit;

namespace FastTests.Client.Subscriptions
{
    public class RavenDB_7384 : RavenTestBase
    {
        private readonly TimeSpan _reasonableWaitTime = Debugger.IsAttached ? TimeSpan.FromSeconds(60 * 10) : TimeSpan.FromSeconds(6);


        [Fact]
        public async Task DisablingDatabaseShouldCutConnection()
        {
            using (var store = GetDocumentStore())
            {
                var subscriptionId = store.Subscriptions.Create<User>(new SubscriptionCreationOptions<User>()
                {
                    Name = "Subs1"
                });

                var subscription = store.Subscriptions.Open<User>(new SubscriptionConnectionOptions("Subs1"));

                var results = new List<User>();
                var mre = new AsyncManualResetEvent();

                using (var session = store.OpenSession())
                {
                    session.Store(new User{});
                    session.SaveChanges();
                }

                var subscriptionTask = subscription.Run(batch =>
                {
                    results.AddRange(batch.Items.Select(i => i.Result).ToArray());
                });

                subscription.AfterAcknowledgment += x =>
                {
                    mre.Set();
                    return Task.CompletedTask;
                };

                Assert.True(await mre.WaitAsync(_reasonableWaitTime));

                var currentDatabase = await Server.ServerStore.DatabasesLandlord.TryGetOrCreateResourceStore(store.Database);

                var operationIndex = await currentDatabase.SubscriptionStorage.PutSubscription(new SubscriptionCreationOptions()
                {
                    Name = "Subs1",
                    ChangeVector = Raven.Client.Constants.Documents.UnchangedSubscriptionsChangeVecotr,
                    Criteria = new SubscriptionCriteria("Users")


                }, subscriptionId, true);

                Assert.Equal(subscriptionTask, await Task.WhenAny(subscriptionTask, Task.Delay(_reasonableWaitTime)));

                await Assert.ThrowsAsync(typeof(SubscriptionClosedException), () => subscriptionTask);
            }
        }

        [Fact]
        public async Task UpdatingSubscriptionScriptShouldNotChangeVectorButShouldDropConnection()
        {
            using (var store = GetDocumentStore())
            {
                var subscriptionId = store.Subscriptions.Create<User>(new SubscriptionCreationOptions<User>()
                {
                    Name = "Subs1",
                    Criteria = new SubscriptionCriteria<User>()
                    {
                        Script = "return {Name:'David'};"
                    }
                });

                var subscription = store.Subscriptions.Open<User>(new SubscriptionConnectionOptions("Subs1"));

                var results = new List<User>();
                var mre = new AsyncManualResetEvent();

                using (var session = store.OpenSession())
                {
                    session.Store(new User{});
                    session.SaveChanges();
                }

                var subscriptionTask = subscription.Run(batch =>
                {
                    results.AddRange(batch.Items.Select(i => i.Result).ToArray());
                });

                subscription.AfterAcknowledgment += x =>
                {
                    mre.Set();
                    return Task.CompletedTask;
                };

                Assert.True(await mre.WaitAsync(_reasonableWaitTime));
                mre.Reset();
                Assert.Equal("David",results[0].Name);
                results.Clear();
                var currentDatabase = await Server.ServerStore.DatabasesLandlord.TryGetOrCreateResourceStore(store.Database);

                string changeVectorBeforeScriptUpdate = GetSubscriptionChangeVector(currentDatabase);

                // updating only subscription script and making sure conneciton drops
                await currentDatabase.SubscriptionStorage.PutSubscription(new SubscriptionCreationOptions()
                {
                    Name = "Subs1",
                    ChangeVector = Raven.Client.Constants.Documents.UnchangedSubscriptionsChangeVecotr,
                    Criteria = new SubscriptionCriteria("Users")
                    {
                        Script = "return {Name:'Jorgen'}"
                    }

                }, subscriptionId, true);

                Assert.Equal(subscriptionTask, await Task.WhenAny(subscriptionTask, Task.Delay(_reasonableWaitTime)));

                await Assert.ThrowsAsync(typeof(SubscriptionClosedException), () => subscriptionTask);

                var changeVectorAfterUpdatingScript = GetSubscriptionChangeVector(currentDatabase);
                Assert.Equal(changeVectorBeforeScriptUpdate, changeVectorAfterUpdatingScript);


                // reconnecting and making sure that the new script is in power
                subscription = store.Subscriptions.Open<User>(new SubscriptionConnectionOptions("Subs1"));

                subscriptionTask = subscription.Run(batch =>
                {
                    results.AddRange(batch.Items.Select(i => i.Result).ToArray());
                });

                subscription.AfterAcknowledgment += x =>
                {
                    mre.Set();
                    return Task.CompletedTask;
                };


                using (var session = store.OpenSession())
                {
                    session.Store(new User { });
                    session.SaveChanges();
                }

                await mre.WaitAsync();

                Assert.True(await mre.WaitAsync(_reasonableWaitTime));
                Assert.Equal("Jorgen", results[0].Name);
            }
        }

        private string GetSubscriptionChangeVector(DocumentDatabase currentDatabase)
        {
            using (Server.ServerStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
            using (context.OpenReadTransaction())
            {
                var subscriptionData = currentDatabase.SubscriptionStorage.GetSubscriptionFromServerStore(context, "Subs1");
                return subscriptionData.ChangeVector;
            }
        }
    }
}
