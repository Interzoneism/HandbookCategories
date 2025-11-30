using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace Enhanced_Handbook
{
    /// <summary>
    /// Cache entry for a single mod from the online mod database.
    /// </summary>
    public sealed class ModDbCacheEntry
    {
        /// <summary>
        /// The unique mod identifier.
        /// </summary>
        [JsonProperty("modId")]
        public string ModId { get; set; }

        /// <summary>
        /// Display name of the mod.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Author of the mod.
        /// </summary>
        [JsonProperty("author")]
        public string Author { get; set; }

        /// <summary>
        /// Short description of the mod.
        /// </summary>
        [JsonProperty("summary")]
        public string Summary { get; set; }

        /// <summary>
        /// Total download count.
        /// </summary>
        [JsonProperty("downloads")]
        public int Downloads { get; set; }

        /// <summary>
        /// Last updated timestamp from the mod database.
        /// </summary>
        [JsonProperty("lastModified")]
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Latest version string.
        /// </summary>
        [JsonProperty("version")]
        public string Version { get; set; }

        /// <summary>
        /// URL to the mod page.
        /// </summary>
        [JsonProperty("url")]
        public string Url { get; set; }

        /// <summary>
        /// Category/type of the mod.
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// Game version compatibility info.
        /// </summary>
        [JsonProperty("gameVersions")]
        public List<string> GameVersions { get; set; }

        /// <summary>
        /// Trending points for sorting.
        /// </summary>
        [JsonProperty("trendingPoints")]
        public int TrendingPoints { get; set; }
    }

    /// <summary>
    /// Represents cached search results from the mod database.
    /// </summary>
    public sealed class ModDbSearchCache
    {
        /// <summary>
        /// The search query that produced these results.
        /// </summary>
        [JsonProperty("query")]
        public string Query { get; set; }

        /// <summary>
        /// The sort order used for the search.
        /// </summary>
        [JsonProperty("sortBy")]
        public string SortBy { get; set; }

        /// <summary>
        /// When this cache entry was created.
        /// </summary>
        [JsonProperty("cachedAt")]
        public DateTime CachedAt { get; set; }

        /// <summary>
        /// ETag from the server response for cache validation.
        /// </summary>
        [JsonProperty("etag")]
        public string ETag { get; set; }

        /// <summary>
        /// Last-Modified header from the server response.
        /// </summary>
        [JsonProperty("lastModifiedHeader")]
        public string LastModifiedHeader { get; set; }

        /// <summary>
        /// Cached mod entries.
        /// </summary>
        [JsonProperty("mods")]
        public List<ModDbCacheEntry> Mods { get; set; }

        /// <summary>
        /// Total count of mods matching the query (may be more than cached).
        /// </summary>
        [JsonProperty("totalCount")]
        public int TotalCount { get; set; }

        /// <summary>
        /// Hash of the response content for change detection.
        /// </summary>
        [JsonProperty("contentHash")]
        public string ContentHash { get; set; }
    }

    /// <summary>
    /// Configuration for the mod database cache.
    /// </summary>
    public sealed class ModDbCacheConfig
    {
        /// <summary>
        /// How long to consider cached search results valid (in minutes).
        /// Default is 30 minutes.
        /// </summary>
        [JsonProperty("searchCacheDurationMinutes")]
        public int SearchCacheDurationMinutes { get; set; } = 30;

        /// <summary>
        /// How long to consider individual mod details valid (in minutes).
        /// Default is 60 minutes.
        /// </summary>
        [JsonProperty("modDetailsCacheDurationMinutes")]
        public int ModDetailsCacheDurationMinutes { get; set; } = 60;

        /// <summary>
        /// Maximum number of search queries to cache.
        /// Default is 50.
        /// </summary>
        [JsonProperty("maxCachedSearches")]
        public int MaxCachedSearches { get; set; } = 50;

        /// <summary>
        /// Whether to use conditional HTTP requests (If-None-Match, If-Modified-Since).
        /// Default is true.
        /// </summary>
        [JsonProperty("useConditionalRequests")]
        public bool UseConditionalRequests { get; set; } = true;

        /// <summary>
        /// Whether caching is enabled.
        /// Default is true.
        /// </summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Manages caching for online mod database queries to minimize redundant downloads.
    /// </summary>
    public sealed class ModDbCacheManager : IDisposable
    {
        private const string CacheFileName = "EnhancedHandbookModDbCache.json";
        private const string ConfigFileName = "EnhancedHandbookModDbCacheConfig.json";

        private readonly ICoreClientAPI capi;
        private readonly object cacheLock = new object();
        private Dictionary<string, ModDbSearchCache> searchCache;
        private ModDbCacheConfig config;
        private string cacheFilePath;
        private string configFilePath;
        private bool isDirty;
        private DateTime lastSaveTime;

        /// <summary>
        /// Creates a new cache manager instance.
        /// </summary>
        /// <param name="api">The client API instance.</param>
        public ModDbCacheManager(ICoreClientAPI api)
        {
            capi = api ?? throw new ArgumentNullException(nameof(api));
            searchCache = new Dictionary<string, ModDbSearchCache>(StringComparer.OrdinalIgnoreCase);
            lastSaveTime = DateTime.MinValue;

            InitializePaths();
            LoadConfig();
            LoadCache();
        }

        /// <summary>
        /// Gets the current cache configuration.
        /// </summary>
        public ModDbCacheConfig Config => config;

        /// <summary>
        /// Gets whether caching is currently enabled.
        /// </summary>
        public bool IsEnabled => config?.Enabled ?? true;

        /// <summary>
        /// Initializes file paths for cache and config storage.
        /// </summary>
        private void InitializePaths()
        {
            try
            {
                string modConfigPath = capi.GetOrCreateDataPath("ModConfig");
                cacheFilePath = Path.Combine(modConfigPath, CacheFileName);
                configFilePath = Path.Combine(modConfigPath, ConfigFileName);
            }
            catch (Exception ex)
            {
                capi?.Logger?.Warning("[Enhanced Handbook] Failed to initialize cache paths: {0}", ex.Message);
                cacheFilePath = null;
                configFilePath = null;
            }
        }

        /// <summary>
        /// Loads the cache configuration from disk.
        /// </summary>
        private void LoadConfig()
        {
            config = new ModDbCacheConfig();

            if (string.IsNullOrEmpty(configFilePath))
            {
                return;
            }

            try
            {
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath, Encoding.UTF8);
                    ModDbCacheConfig loaded = JsonConvert.DeserializeObject<ModDbCacheConfig>(json);
                    if (loaded != null)
                    {
                        config = loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                capi?.Logger?.Warning("[Enhanced Handbook] Failed to load cache config: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Saves the cache configuration to disk.
        /// </summary>
        public void SaveConfig()
        {
            if (string.IsNullOrEmpty(configFilePath) || config == null)
            {
                return;
            }

            try
            {
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configFilePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                capi?.Logger?.Warning("[Enhanced Handbook] Failed to save cache config: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Loads the search cache from disk.
        /// </summary>
        private void LoadCache()
        {
            if (string.IsNullOrEmpty(cacheFilePath))
            {
                return;
            }

            lock (cacheLock)
            {
                try
                {
                    if (File.Exists(cacheFilePath))
                    {
                        string json = File.ReadAllText(cacheFilePath, Encoding.UTF8);
                        var loaded = JsonConvert.DeserializeObject<Dictionary<string, ModDbSearchCache>>(json);
                        if (loaded != null)
                        {
                            searchCache = new Dictionary<string, ModDbSearchCache>(loaded, StringComparer.OrdinalIgnoreCase);
                            PruneExpiredEntries();
                        }
                    }
                }
                catch (Exception ex)
                {
                    capi?.Logger?.Warning("[Enhanced Handbook] Failed to load mod cache: {0}", ex.Message);
                    searchCache = new Dictionary<string, ModDbSearchCache>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        /// <summary>
        /// Saves the search cache to disk.
        /// </summary>
        public void SaveCache()
        {
            if (string.IsNullOrEmpty(cacheFilePath))
            {
                return;
            }

            lock (cacheLock)
            {
                if (!isDirty)
                {
                    return;
                }

                try
                {
                    PruneExpiredEntries();
                    EnforceCacheSizeLimit();

                    string json = JsonConvert.SerializeObject(searchCache, Formatting.Indented);
                    File.WriteAllText(cacheFilePath, json, Encoding.UTF8);
                    isDirty = false;
                    lastSaveTime = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    capi?.Logger?.Warning("[Enhanced Handbook] Failed to save mod cache: {0}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Generates a cache key for a search query.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="sortBy">The sort order.</param>
        /// <returns>A unique cache key.</returns>
        public static string GenerateCacheKey(string query, string sortBy)
        {
            string normalized = $"{(query ?? string.Empty).Trim().ToLowerInvariant()}|{(sortBy ?? "trendingpoints").ToLowerInvariant()}";
            return normalized;
        }

        /// <summary>
        /// Computes a hash of the content for change detection.
        /// </summary>
        /// <param name="content">The content to hash.</param>
        /// <returns>A hex-encoded hash string.</returns>
        public static string ComputeContentHash(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return string.Empty;
            }

            using var sha256 = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            byte[] hash = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>
        /// Attempts to get cached search results.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="sortBy">The sort order.</param>
        /// <param name="cache">The cached results if found and valid.</param>
        /// <returns>True if valid cached results were found.</returns>
        public bool TryGetCachedSearch(string query, string sortBy, out ModDbSearchCache cache)
        {
            cache = null;

            if (!IsEnabled)
            {
                return false;
            }

            string key = GenerateCacheKey(query, sortBy);

            lock (cacheLock)
            {
                if (!searchCache.TryGetValue(key, out ModDbSearchCache entry))
                {
                    return false;
                }

                if (entry == null || entry.Mods == null)
                {
                    searchCache.Remove(key);
                    isDirty = true;
                    return false;
                }

                // Check if cache has expired
                TimeSpan age = DateTime.UtcNow - entry.CachedAt;
                if (age.TotalMinutes > config.SearchCacheDurationMinutes)
                {
                    // Cache is stale, but we might still use it for conditional requests
                    cache = entry;
                    return false;
                }

                cache = entry;
                return true;
            }
        }

        /// <summary>
        /// Gets cache validation headers for conditional requests.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="sortBy">The sort order.</param>
        /// <param name="etag">The cached ETag if available.</param>
        /// <param name="lastModified">The cached Last-Modified header if available.</param>
        /// <returns>True if validation headers are available.</returns>
        public bool TryGetValidationHeaders(string query, string sortBy, out string etag, out string lastModified)
        {
            etag = null;
            lastModified = null;

            if (!IsEnabled || !config.UseConditionalRequests)
            {
                return false;
            }

            string key = GenerateCacheKey(query, sortBy);

            lock (cacheLock)
            {
                if (!searchCache.TryGetValue(key, out ModDbSearchCache entry) || entry == null)
                {
                    return false;
                }

                etag = entry.ETag;
                lastModified = entry.LastModifiedHeader;
                return !string.IsNullOrEmpty(etag) || !string.IsNullOrEmpty(lastModified);
            }
        }

        /// <summary>
        /// Stores search results in the cache.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="sortBy">The sort order.</param>
        /// <param name="mods">The mod entries to cache.</param>
        /// <param name="totalCount">Total count of matching mods.</param>
        /// <param name="etag">ETag from the response headers.</param>
        /// <param name="lastModified">Last-Modified from the response headers.</param>
        /// <param name="responseContent">The raw response content for hashing.</param>
        public void CacheSearchResults(
            string query,
            string sortBy,
            List<ModDbCacheEntry> mods,
            int totalCount,
            string etag = null,
            string lastModified = null,
            string responseContent = null)
        {
            if (!IsEnabled || mods == null)
            {
                return;
            }

            string key = GenerateCacheKey(query, sortBy);

            lock (cacheLock)
            {
                ModDbSearchCache entry = new ModDbSearchCache
                {
                    Query = query ?? string.Empty,
                    SortBy = sortBy ?? "trendingpoints",
                    CachedAt = DateTime.UtcNow,
                    ETag = etag,
                    LastModifiedHeader = lastModified,
                    Mods = new List<ModDbCacheEntry>(mods),
                    TotalCount = totalCount,
                    ContentHash = string.IsNullOrEmpty(responseContent) ? null : ComputeContentHash(responseContent)
                };

                searchCache[key] = entry;
                isDirty = true;

                // Auto-save if enough time has passed
                if ((DateTime.UtcNow - lastSaveTime).TotalMinutes > 5)
                {
                    SaveCache();
                }
            }
        }

        /// <summary>
        /// Checks if cached content matches new content (for detecting unchanged responses).
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="sortBy">The sort order.</param>
        /// <param name="newContent">The new response content.</param>
        /// <returns>True if the content is unchanged.</returns>
        public bool IsContentUnchanged(string query, string sortBy, string newContent)
        {
            if (!IsEnabled || string.IsNullOrEmpty(newContent))
            {
                return false;
            }

            string key = GenerateCacheKey(query, sortBy);
            string newHash = ComputeContentHash(newContent);

            lock (cacheLock)
            {
                if (!searchCache.TryGetValue(key, out ModDbSearchCache entry) || entry == null)
                {
                    return false;
                }

                if (string.IsNullOrEmpty(entry.ContentHash))
                {
                    return false;
                }

                return string.Equals(entry.ContentHash, newHash, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Updates the cached timestamp to extend validity without changing content.
        /// Used when a conditional request returns 304 Not Modified.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="sortBy">The sort order.</param>
        public void RefreshCacheTimestamp(string query, string sortBy)
        {
            if (!IsEnabled)
            {
                return;
            }

            string key = GenerateCacheKey(query, sortBy);

            lock (cacheLock)
            {
                if (searchCache.TryGetValue(key, out ModDbSearchCache entry) && entry != null)
                {
                    entry.CachedAt = DateTime.UtcNow;
                    isDirty = true;
                }
            }
        }

        /// <summary>
        /// Removes expired cache entries.
        /// </summary>
        private void PruneExpiredEntries()
        {
            DateTime now = DateTime.UtcNow;
            List<string> keysToRemove = new List<string>();

            foreach (var kvp in searchCache)
            {
                if (kvp.Value == null || kvp.Value.Mods == null)
                {
                    keysToRemove.Add(kvp.Key);
                    continue;
                }

                // Remove entries older than 24 hours regardless of config
                TimeSpan age = now - kvp.Value.CachedAt;
                if (age.TotalHours > 24)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (string key in keysToRemove)
            {
                searchCache.Remove(key);
                isDirty = true;
            }
        }

        /// <summary>
        /// Enforces the maximum cache size limit by removing oldest entries.
        /// </summary>
        private void EnforceCacheSizeLimit()
        {
            if (searchCache.Count <= config.MaxCachedSearches)
            {
                return;
            }

            // Sort by cached time and remove oldest
            List<KeyValuePair<string, ModDbSearchCache>> sorted = new List<KeyValuePair<string, ModDbSearchCache>>(searchCache);
            sorted.Sort((a, b) => a.Value.CachedAt.CompareTo(b.Value.CachedAt));

            int toRemove = searchCache.Count - config.MaxCachedSearches;
            for (int i = 0; i < toRemove && i < sorted.Count; i++)
            {
                searchCache.Remove(sorted[i].Key);
                isDirty = true;
            }
        }

        /// <summary>
        /// Clears all cached data.
        /// </summary>
        public void ClearCache()
        {
            lock (cacheLock)
            {
                searchCache.Clear();
                isDirty = true;
                SaveCache();
            }
        }

        /// <summary>
        /// Gets statistics about the cache.
        /// </summary>
        /// <returns>A summary of cache statistics.</returns>
        public string GetCacheStatistics()
        {
            lock (cacheLock)
            {
                int totalEntries = searchCache.Count;
                int validEntries = 0;
                int staleEntries = 0;
                DateTime now = DateTime.UtcNow;

                foreach (var entry in searchCache.Values)
                {
                    if (entry == null || entry.Mods == null)
                    {
                        continue;
                    }

                    TimeSpan age = now - entry.CachedAt;
                    if (age.TotalMinutes <= config.SearchCacheDurationMinutes)
                    {
                        validEntries++;
                    }
                    else
                    {
                        staleEntries++;
                    }
                }

                return $"Cached searches: {totalEntries}, Valid: {validEntries}, Stale: {staleEntries}";
            }
        }

        /// <summary>
        /// Disposes of resources and saves the cache.
        /// </summary>
        public void Dispose()
        {
            SaveCache();
            SaveConfig();
        }
    }
}
