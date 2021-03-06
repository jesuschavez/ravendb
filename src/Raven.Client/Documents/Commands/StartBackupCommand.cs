﻿using System.Net.Http;
using Raven.Client.Http;
using Raven.Client.Json.Converters;
using Sparrow.Json;

namespace Raven.Client.Documents.Commands
{
    public class StartBackupCommand : RavenCommand<CommandResult>
    {
        public override bool IsReadRequest => true;

        private readonly bool _isFullBackup;
        private readonly string _databaseName;
        private readonly long _taskId;

        public StartBackupCommand(bool isFullBackup, string databaseName, long taskId)
        {
            _isFullBackup = isFullBackup;
            _databaseName = databaseName;
            _taskId = taskId;
        }

        public override HttpRequestMessage CreateRequest(JsonOperationContext ctx, ServerNode node, out string url)
        {
            url = $"{node.Url}/admin/backup/database?name={_databaseName}&isFullBackup={_isFullBackup}&taskId={_taskId}";
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post
            };

            return request;
        }

        public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
        {
            if (response == null)
                ThrowInvalidResponse();

            Result = JsonDeserializationClient.CommandResult(response);
        }
    }
}
