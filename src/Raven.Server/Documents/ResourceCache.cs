﻿using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Sparrow;
using Sparrow.Collections;

namespace Raven.Server.Documents
{
    [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
    public class ResourceCache<TResource> : IEnumerable<KeyValuePair<StringSegment, Task<TResource>>>
    {
        readonly ConcurrentDictionary<StringSegment, Task<TResource>> _caseInsensitive = 
                    new ConcurrentDictionary<StringSegment, Task<TResource>>(CaseInsensitiveStringSegmentEqualityComparer.Instance);
        readonly ConcurrentDictionary<StringSegment, Task<TResource>> _caseSensitive 
            = new ConcurrentDictionary<StringSegment, Task<TResource>>(StringSegmentEqualityComparer.Instance);

        private readonly ConcurrentDictionary<StringSegment, ConcurrentSet<StringSegment>> _mappings =
            new ConcurrentDictionary<StringSegment, ConcurrentSet<StringSegment>>(CaseInsensitiveStringSegmentEqualityComparer.Instance);

        /// <summary>
        /// This locks the entire cache. Use carefully.
        /// </summary>
        public IEnumerable<Task<TResource>> Values => _caseSensitive.Values;

        public int Count => _caseSensitive.Count;

        public void Clear()
        {
            _caseSensitive.Clear();
            _caseInsensitive.Clear();
        }

        public bool TryGetValue(StringSegment resourceName, out Task<TResource> resourceTask)
        {
            if (_caseSensitive.TryGetValue(resourceName, out resourceTask))
                return true;
            
            return UnlikelyTryGet(resourceName, out resourceTask);
                
        }

        private bool UnlikelyTryGet(StringSegment resourceName, out Task<TResource> resourceTask)
        {
            if (_caseInsensitive.TryGetValue(resourceName, out resourceTask) == false)
                return false;

            lock (this)
            {
                //we have a case insensitive match, let us optimize that
                if (_mappings.TryGetValue(resourceName, out ConcurrentSet<StringSegment> mappingsForResource))
                {
                    mappingsForResource.Add(resourceName);
                    _caseSensitive.TryAdd(resourceName, resourceTask);
                }
            }
            return true;
                
        }

        public bool TryRemove(StringSegment resourceName, out Task<TResource> resourceTask)
        {
            if (_caseInsensitive.TryRemove(resourceName, out resourceTask) == false)
                return false;

            lock (this)
            {
                if (_mappings.TryGetValue(resourceName, out ConcurrentSet<StringSegment> mappings))
                {
                    foreach (var mapping in mappings)
                    {
                        _caseSensitive.TryRemove(mapping, out Task<TResource> _);
                    }
                }
            }

            return true;
        }

        public IEnumerator<KeyValuePair<StringSegment, Task<TResource>>> GetEnumerator()
        {
            return _caseInsensitive.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Task<TResource> GetOrAdd(StringSegment databaseName, Task<TResource> task)
        {
            if (_caseSensitive.TryGetValue(databaseName, out Task<TResource> value))
                return value;

            if (_caseInsensitive.TryGetValue(databaseName, out value))
                return value;

            lock (this)
            {
                if (_caseInsensitive.TryGetValue(databaseName, out value))
                    return value;

                value = _caseInsensitive.GetOrAdd(databaseName, task);
                _caseSensitive[databaseName] = value;
                _mappings[databaseName] = new ConcurrentSet<StringSegment>
                {
                    databaseName
                };
                return value;
            }
        }

        public Task<TResource> Replace(string databaseName, Task<TResource> task)
        {
            lock (this)
            {
                Task<TResource> existingTask = null;
                _caseInsensitive.AddOrUpdate(databaseName, segment => task, (key, existing) =>
                {
                    existingTask = existing;
                    return task;
                });
                if (_mappings.TryGetValue(databaseName, out ConcurrentSet<StringSegment> mappings))
                {
                    foreach (var mapping in mappings)
                    {
                        _caseSensitive.TryRemove(mapping, out Task<TResource> _);
                    }
                }
                return existingTask;
            }
        }
    }
}
