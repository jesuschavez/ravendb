﻿using System;
using Voron.Data.BTrees;
using Voron.Global;
using Voron.Impl;
using Voron.Impl.Paging;

namespace Voron.Data.Compression
{
    public unsafe class DecompressedLeafPage : TreePage, IDisposable
    {
        private readonly TemporaryPage _tempPage;

        private bool _disposed;

        public DecompressedLeafPage(byte* basePtr, int pageSize, DecompressionUsage usage, TreePage original, TemporaryPage tempPage) : base(basePtr, pageSize)
        {
            Original = original;
            Usage = usage;
            _tempPage = tempPage;

            PageNumber = Original.PageNumber;
            TreeFlags = Original.TreeFlags;
            Flags = Original.Flags & ~PageFlags.Compressed;
        }

        public TreePage Original;

        public bool Cached;

        public DecompressionUsage Usage;

        public void Dispose()
        {
            if (Cached)
                return;

            if (_disposed)
                return;
            
            _tempPage.ReturnTemporaryPageToPool.Dispose();

            _disposed = true;
        }

        public void CopyToOriginal(LowLevelTransaction tx, bool defragRequired)
        {
            if (CalcSizeUsed() < Original.PageMaxSpace)
            {
                // no need to compress
                Original.Lower = (ushort)Constants.Tree.PageHeaderSize;
                Original.Upper = (ushort)Original.PageSize;
                Original.Flags &= ~PageFlags.Compressed;

                for (var i = 0; i < NumberOfEntries; i++)
                {
                    var node = GetNode(i);
                    Slice slice;
                    using (TreeNodeHeader.ToSlicePtr(tx.Allocator, node, out slice))
                        Original.CopyNodeDataToEndOfPage(node, slice);
                }
            }
            else
            {
                CompressionResult compressed;
                using (LeafPageCompressor.TryGetCompressedTempPage(tx, this, out compressed, defrag: defragRequired))
                {
                    if (compressed == null)
                        throw new InvalidOperationException("Could not compress a page which was already compressed. Should never happen");

                    LeafPageCompressor.CopyToPage(compressed, Original);
                }
            }
        }
    }
}