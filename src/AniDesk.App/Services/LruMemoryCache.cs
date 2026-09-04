using System;
using System.Collections.Generic;

namespace AniDesk.App.Services;

/// <summary>
/// Thread-safe bounded LRU memory cache with O(1) lookups, promotions, and evictions.
/// </summary>
public sealed class LruMemoryCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly object _syncLock = new();
    private readonly Dictionary<TKey, LinkedListNode<LruEntry>> _map;
    private readonly LinkedList<LruEntry> _list;

    private readonly struct LruEntry
    {
        public readonly TKey Key;
        public readonly TValue Value;

        public LruEntry(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }

    public LruMemoryCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        _capacity = Math.Max(1, capacity);
        _map = new Dictionary<TKey, LinkedListNode<LruEntry>>(capacity, comparer ?? EqualityComparer<TKey>.Default);
        _list = new LinkedList<LruEntry>();
    }

    public int Count
    {
        get
        {
            lock (_syncLock)
            {
                return _map.Count;
            }
        }
    }

    public bool TryGet(TKey key, out TValue? value)
    {
        lock (_syncLock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _list.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    public TValue? Get(TKey key) => TryGet(key, out var val) ? val : default;

    public void Set(TKey key, TValue value)
    {
        lock (_syncLock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _list.AddFirst(node);
                node.Value = new LruEntry(key, value);
                return;
            }

            if (_map.Count >= _capacity && _list.Last != null)
            {
                var evictNode = _list.Last;
                _list.RemoveLast();
                _map.Remove(evictNode.Value.Key);
            }

            var newNode = new LinkedListNode<LruEntry>(new LruEntry(key, value));
            _list.AddFirst(newNode);
            _map[key] = newNode;
        }
    }

    public bool Remove(TKey key)
    {
        lock (_syncLock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _map.Remove(key);
                return true;
            }
        }
        return false;
    }

    public void Clear()
    {
        lock (_syncLock)
        {
            _map.Clear();
            _list.Clear();
        }
    }
}
