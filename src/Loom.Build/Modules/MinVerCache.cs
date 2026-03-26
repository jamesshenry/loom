// using System.Collections.Concurrent;

// namespace Loom.Modules;

// /// <summary>
// /// Thread-safe cache of minver-resolved versions keyed by tag prefix.
// /// Deduplicates CLI invocations when multiple modules need the same prefix.
// /// </summary>
// public class MinVerCache
// {
//     private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
//     private readonly ConcurrentDictionary<string, string> _cache = new();

//     /// <summary>
//     /// Returns the cached version for <paramref name="tagPrefix"/>, or invokes
//     /// <paramref name="factory"/> exactly once to compute it.
//     /// </summary>
//     public async Task<string> GetOrAddAsync(string? tagPrefix, Func<Task<string>> factory)
//     {
//         var key = tagPrefix ?? "";

//         if (_cache.TryGetValue(key, out var cached))
//             return cached;

//         var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
//         await semaphore.WaitAsync();
//         try
//         {
//             if (_cache.TryGetValue(key, out cached))
//                 return cached;

//             var value = await factory();
//             _cache[key] = value;
//             return value;
//         }
//         finally
//         {
//             semaphore.Release();
//         }
//     }
// }
