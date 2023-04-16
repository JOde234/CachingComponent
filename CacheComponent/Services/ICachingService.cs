namespace CacheComponent.Services;

public interface ICachingService<TKey, TValue>
{
    public TValue? Get( TKey key );
    public void Set( TKey key, TValue value, int persistTimeSpanMs );
}