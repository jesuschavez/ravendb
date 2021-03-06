//-----------------------------------------------------------------------
// <copyright file="ISyncAdvancedSessionOperation.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System.IO;
using Raven.Client.Documents.Operations;

namespace Raven.Client.Documents.Session
{
    /// <summary>
    ///     Advanced synchronous session operations
    /// </summary>
    public partial interface IAdvancedSessionOperations
    {
        /// <summary>
        /// Returns the attachments info of a document.
        /// </summary>
        AttachmentName[] GetAttachmentNames(object entity);

        /// <summary>
        /// Check if attachment exists
        /// </summary>
        bool AttachmentExists(string documentId, string name);

        /// <summary>
        /// Returns the attachment by the document id and attachment name.
        /// </summary>
        AttachmentResult GetAttachment(string documentId, string name);

        /// <summary>
        /// Returns the attachment by the document id and attachment name.
        /// </summary>
        AttachmentResult GetAttachment(object entity, string name);

        /// <summary>
        /// Returns the revision attachment by the document id and attachment name.
        /// </summary>
        AttachmentResult GetRevisionAttachment(string documentId, string name, string changeVector);

        /// <summary>
        /// Stores attachment to be sent in the session.
        /// </summary>
        void StoreAttachment(string documentId, string name, Stream stream, string contentType = null);
        
        /// <summary>
        /// Stores attachment to be sent in the session.
        /// </summary>
        void StoreAttachment(object entity, string name, Stream stream, string contentType = null);

        /// <summary>
        ///     Marks the specified document's attachment for deletion. The attachment will be deleted when
        ///     <see cref="IDocumentSession.SaveChanges" /> is called.
        /// </summary>
        /// <param name="documentId">the document which holds the attachment</param>
        /// <param name="name">the attachment name</param>
        void DeleteAttachment(string documentId, string name);

        /// <summary>
        ///     Marks the specified document's attachment for deletion. The attachment will be deleted when
        ///     <see cref="IDocumentSession.SaveChanges" /> is called.
        /// </summary>
        /// <param name="entity">instance of entity of the document which holds the attachment</param>
        /// <param name="name">the attachment name</param>
        void DeleteAttachment(object entity, string name);
    }
}