namespace CacheComponent.CustomEventArgs
{
    public class CachedItemEventArgs<T> : EventArgs
    {
        public CachedItemEventArgs(T key)
        {
            Key = key;
        }

        public T Key { get; }
    }
        
}