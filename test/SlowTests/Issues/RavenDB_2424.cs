// -----------------------------------------------------------------------
//  <copyright file="RavenDB_2424.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using FastTests;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_2424 : RavenTestBase
    {
        [Fact]
        public void HasChangedWorkProperly()
        {
            using (var store = GetDocumentStore())
            {
                const string initialIndexDef = "from doc in docs select new { doc.Date}";
                Assert.True(store.Admin.Send(new IndexHasChangedOperation(new IndexDefinition
                {
                    Name = "Index1",
                    Maps = { initialIndexDef }
                })));

                store.Admin.Send(new PutIndexesOperation(new IndexDefinition
                {
                    Name = "Index1",
                    Maps = { initialIndexDef }
                }));

                Assert.False(store.Admin.Send(new IndexHasChangedOperation(new IndexDefinition
                {
                    Name = "Index1",
                    Maps = { initialIndexDef }
                })));

                Assert.True(store.Admin.Send(new IndexHasChangedOperation(new IndexDefinition
                {
                    Name = "Index1",
                    Maps = { "from doc1 in docs select new { doc1.Date }" }
                })));
            }
        }
    }
}
