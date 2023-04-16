## Background
There's a need for an adjustment to the application that handles large volumes of data, and also needs to be fast. 
One strategy to improve the execution speed is to cache regularly-used data in memory.

## Service description
It's a generic in-memory cache component, which other developers can use in their applications.
This component can store arbitrary types of objects, which are added and retrieved
using a unique key (similar to a dictionary).

To avoid the risk of running out of memory, the cache has a **configurable threshold** for
the maximum number of items which it can hold at any one time. If the cache becomes full, any
attempts to add additional items results in another item in the cache being
evicted. The cache should implement the *least recently used* approach when selecting which item
to evict.

The cache component is a **singleton**, **thread-safe** for all methods.
Another feature - there is an event sender which can notify a subscriber to know when items get evicted.

And one more feature - it's possible to set the time in milliseconds to automatically remove the cache for the specific key i.e. define cache time to be persisted.