using System.Timers;
using Timer = System.Timers.Timer;
using CacheComponent.CustomEventArgs;

namespace CacheComponent.Services;

public class InMemoryCachingService<TKey, TValue>: IDisposable, ICachingService<TKey, TValue> where TKey : notnull
{
    public delegate void InMemoryCacheRemovedEventHandler( object source, CachedItemEventArgs<TKey> args );
    public event InMemoryCacheRemovedEventHandler? InMemoryCacheRemoved;
    
    private static readonly Lazy<InMemoryCachingService<TKey, TValue>> threadInstance = new( () => new InMemoryCachingService<TKey, TValue>());
    private static readonly Dictionary<TKey, LinkedListNode<CachedItem>> cache = new();
    private static readonly LinkedList<CachedItem> cacheValueUsageOrderedList = new();
    private static int maxCapacity;
    private static int instanceCount;
    private static Timer? expirationTimer;
    private static int cacheRefreshFrequency;

    private InMemoryCachingService() 
    {
        instanceCount++;
    }

    public static InMemoryCachingService<TKey, TValue> Instance => threadInstance.Value;
    public int Count => instanceCount;

    public void SetMaxCapacity(int capacity)
    {
        if( maxCapacity == 0 )
        {
            maxCapacity = capacity;
            cache.EnsureCapacity( maxCapacity );
        }
        else
        {
            Console.WriteLine($"Max capacity is already set to {maxCapacity}"); // probably not needed more for debugging
        }
    }

    public TValue? Get(TKey key)
    {
        LinkedListNode<CachedItem>? node;
        lock ( cache )
        {
            if( !cache.TryGetValue( key, out node ) ) return default;    
        }
        
        // Refresh the usage of the accessed cached item
        lock ( cacheValueUsageOrderedList )
        {
            cacheValueUsageOrderedList.Remove(node);
            cacheValueUsageOrderedList.AddFirst(node);
        }
        return node.Value.Value;
    }

    public void Set(TKey key, TValue value, int persistTimeSpanMs = 0)
    {
        if( maxCapacity == 0 ) throw new ApplicationException( "Max capacity is not set for the caching service, cannot add an item." );
        if( persistTimeSpanMs > 0 && cacheRefreshFrequency == 0 ) 
            throw new ApplicationException( "Refresh frequency must be set for the caching service to add an item with the defined time to be persisted." );
        if( persistTimeSpanMs < 0 ) 
            throw new ApplicationException( "Time span to persist the cache cannot be negative." );

        lock ( cache )
        {
            if (cache.Count == maxCapacity)
            {
                // Evict least recently used item from the cache
                var lastNode = cacheValueUsageOrderedList.Last;
                RemoveCachedItem( lastNode!.Value.Key );
            }

            // Add or replace new item in the cache and add to the front of the usage list
            var newNode = new LinkedListNode<CachedItem>(new CachedItem(key, value, persistTimeSpanMs));
            AddNewCachedItem( key, newNode );
        }
    }

    public void SetCacheRefreshFrequency(int frequencyMs)
    {
        if( cacheRefreshFrequency == 0 )
        {
            cacheRefreshFrequency = frequencyMs;
            expirationTimer = new Timer( TimeSpan.FromMilliseconds( cacheRefreshFrequency ).TotalMilliseconds );
            expirationTimer.Elapsed += ExpirationTimerElapsed;
            expirationTimer.Start();
        }
        else
        {
            Console.WriteLine($"Refresh frequency is already set to {cacheRefreshFrequency}"); // probably not needed more for debugging
        }
    }

    public void Dispose() {
        if (expirationTimer is not null) {
            expirationTimer.Elapsed -= ExpirationTimerElapsed;
            expirationTimer.Stop();
            expirationTimer.Dispose();
        }
    }

    protected virtual void OnInMemoryCacheRemoved(TKey key)
    {
        if( InMemoryCacheRemoved != null )
        {
            InMemoryCacheRemoved( this, new CachedItemEventArgs<TKey>(key));
        }
    }

    private void AddNewCachedItem( TKey key, LinkedListNode<CachedItem> newNode )
    {
        if( !cache.TryGetValue( key, out var node ) )
        {
            cache.Add( key, newNode );
        }
        else
        {
            cache[ key ] = newNode;
            cacheValueUsageOrderedList.Remove( node );
        }
        cacheValueUsageOrderedList.AddFirst( newNode );
    }

    private void ExpirationTimerElapsed( object? sender, ElapsedEventArgs e )
    {
        lock ( cache )
        {
            var currentTime = DateTime.UtcNow;
            var expiredKeys = ( from item in cache
                where item.Value.Value.ExpirationTime <= currentTime 
                select item.Key ).ToList();
            expiredKeys.ForEach( RemoveCachedItem );
        }
    }

    private void RemoveCachedItem( TKey key )
    {
        if( !cache.TryGetValue( key, out var node ) ) return;
        cacheValueUsageOrderedList.Remove(node);
        cache.Remove(key);
        OnInMemoryCacheRemoved( key );
    }

    private class CachedItem
    {
        public TKey Key { get; }
        public TValue Value { get; }
        public DateTime? ExpirationTime { get; }

        public CachedItem(TKey key, TValue value, int persistTimeSpanMs = 0)
        {
            Key = key;
            Value = value;
            ExpirationTime = persistTimeSpanMs > 0 ? DateTime.UtcNow.Add( TimeSpan.FromMilliseconds(persistTimeSpanMs) ) : null;
        }
    }
}

