using System.Reflection;
using CacheComponent.Services;
using CacheComponent.CustomEventArgs;
using Shouldly;

namespace CacheComponentTests;

public class InMemoryCacheServiceTests
{
    private readonly InMemoryCachingService<string, List<string>> cachingService;
    private readonly List<string> dummyData = new() { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
    private const string dummyKey = "months";
    private List<string> removedKeys = new();

    public InMemoryCacheServiceTests()
    {
        cachingService = InMemoryCachingService<string, List<string>>.Instance;
    }
    
    [Fact]
    public void CachingServiceGetNotExistingKeyCacheEmpty()
    {
        var result = cachingService.Get( $"{dummyKey}Test" );
        result.ShouldBeNull();
    }
    
    [Fact]
    public void CachingServiceAddNewCacheSuccess()
    {
        cachingService.SetMaxCapacity( 10 );
        cachingService.Set( dummyKey, dummyData );
        var result = cachingService.Get( dummyKey );
        result!.ShouldAllBe( item => dummyData.Contains( item ) );
    }

    //arbitrary types of objects, which are added and retrieved using a unique key (similar to a dictionary)
    [Fact]
    public void CachingServiceUpdateCacheOfExistingKeySuccess()
    {
        cachingService.SetMaxCapacity( 10 );
        cachingService.Set( dummyKey, dummyData );
        dummyData[ 0 ] = "New Jan";
        cachingService.Set( dummyKey, dummyData );
        var result = cachingService.Get( dummyKey );
        result![0].ShouldBe( "New Jan" );
    }
    
    //If the cache becomes full, any attempts to add additional items results in another item in the cache being evicted
    [Fact]
    public void CachingServiceAddItemsOverMaxThresholdSuccess()
    {
        cachingService.SetMaxCapacity( 10 );
        for ( int i = 0; i < 15; i++ )
        {
            cachingService.Set( $"{dummyKey}{i}", dummyData );
        }
        cachingService.Get( $"{dummyKey}0" ).ShouldBeNull();
        cachingService.Get( $"{dummyKey}5" )!.ShouldAllBe( item => dummyData.Contains( item ) );
    }
    
    //The cache should implement the *least recently used* approach when selecting which item to evict.
    [Fact]
    public void CachingServiceAddItemsOverMaxThresholdLastUsedEvicted()
    {
        cachingService.SetMaxCapacity( 10 );
        for ( int i = 0; i < 15; i++ )
        {
            cachingService.Set( $"{dummyKey}{i}", dummyData );
            cachingService.Get( $"{dummyKey}0" );
        }
        cachingService.Get( $"{dummyKey}0" ).ShouldNotBeNull();
        cachingService.Get( $"{dummyKey}5" )?.ShouldBeNull();
    }

    [Fact]
    public void CachingServiceThrowsOnSettingCacheWithoutThresholdSpecified()
    {
        var exception = Record.Exception(() => {
            cachingService.GetType().GetField( "maxCapacity", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue( cachingService, 0 );
            cachingService.Set( dummyKey, dummyData );
        } );
        exception!.Message.ShouldContain( "Max capacity is not set for the caching service, cannot add an item." );
    }
    
    [Fact]
    public void CachingServiceDoesntChangeMaxThresholdOnceSet()
    {
        cachingService.SetMaxCapacity( 10 );
        cachingService.SetMaxCapacity( 5 );
        var t = cachingService.GetType(); 
        var result = t.GetField( "maxCapacity", BindingFlags.Static | BindingFlags.NonPublic);
        result!.GetValue( cachingService ).ShouldBe(10);
    }
    
    //The cache component is a **singleton**, **thread-safe** for all methods
    [Fact]
    public void CachingServiceHasSingleInstance()
    {
        var cachingService2 = InMemoryCachingService<string, List<string>>.Instance;
        cachingService2.ShouldBeSameAs( cachingService );
        cachingService.Count.ShouldBe( 1 );
    }
    
    [Fact]
    public void CacheServiceMultipleThreadsAllItemsAreAddedToSameCache()
    {
        cachingService.SetMaxCapacity( 10 );
        var t1 = Task.Run( () => {
            cachingService.Set( $"{dummyKey}_t1", dummyData );
        } );
        var t2 = Task.Run( () => {
            cachingService.Set( $"{dummyKey}_t2", dummyData );
        } );
        Task.WaitAll( t1, t2 );
        cachingService.Get( $"{dummyKey}_t1" ).ShouldNotBeNull();
        cachingService.Get( $"{dummyKey}_t2" ).ShouldNotBeNull();
    }

    //allows the consumer to know when items get evicted
    [Fact]
    public void CacheServiceSendMessageWhenItemGetEvicted()
    {
        cachingService.SetMaxCapacity( 10 );
        cachingService.InMemoryCacheRemoved += InMemoryCacheRemoved;
        for ( int i = 0; i < 11; i++ )
        {
            cachingService.Set( $"{dummyKey}{i}", dummyData );
        }
        removedKeys.ShouldNotBeEmpty();
        cachingService.InMemoryCacheRemoved -= InMemoryCacheRemoved;
    }
    

    [Fact]
    public void CachingServiceItemRemovedWhenExpired()
    {
        cachingService.SetMaxCapacity( 10 );
        cachingService.SetCacheRefreshFrequency( 100 );
        cachingService.Set( dummyKey, dummyData, 50 );
        Thread.Sleep( 200 );

        cachingService.Get( dummyKey ).ShouldBeNull();
    }

    [Fact]
    public void CachingServiceThrowsOnEmptyRefreshFreqWithDefinedPersistTime()
    {
        var exception = Record.Exception(() => {
            cachingService.SetMaxCapacity( 10 );
            cachingService.GetType().GetField( "cacheRefreshFrequency", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue( cachingService, 0 );
            cachingService.Set( dummyKey, dummyData, 20 );
        } );
        exception!.Message.ShouldContain( "Refresh frequency must be set for the caching service to add an item with the defined time to be persisted." );    
    }

    [Fact]
    public void CachingServiceThrowsOnNegativePersistTime()
    {
        var exception = Record.Exception(() => {
            cachingService.SetMaxCapacity( 10 );
            cachingService.SetCacheRefreshFrequency( 100 );
            cachingService.Set( dummyKey, dummyData, -20 );
        } );
        exception!.Message.ShouldContain( "Time span to persist the cache cannot be negative." );
        
    }
    private void InMemoryCacheRemoved( object source, CachedItemEventArgs<string> args )
    {
        removedKeys.Add( args.Key );
    }

}