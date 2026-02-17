using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;

namespace ISPing
{
    public class DnsCache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly TimeSpan _defaultTtl;

        public DnsCache(TimeSpan? defaultTtl = null)
        {
            _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(5);
        }

        public async Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress)
        {
            if (string.IsNullOrWhiteSpace(hostNameOrAddress))
                throw new ArgumentException("Hostname não pode ser vazio", nameof(hostNameOrAddress));

            if (IPAddress.TryParse(hostNameOrAddress, out IPAddress? parsedIp))
            {
                return new[] { parsedIp };
            }

            string cacheKey = hostNameOrAddress.ToLowerInvariant();

            if (_cache.TryGetValue(cacheKey, out CacheEntry? entry))
            {
                if (!entry.IsExpired)
                {
                    return entry.Addresses;
                }
                else
                {
                    _cache.TryRemove(cacheKey, out _);
                }
            }

            IPAddress[] addresses = await Dns.GetHostAddressesAsync(hostNameOrAddress);

            _cache[cacheKey] = new CacheEntry(addresses, DateTime.UtcNow + _defaultTtl);

            return addresses;
        }

        public void Clear()
        {
            _cache.Clear();
        }

        public void Invalidate(string hostNameOrAddress)
        {
            if (!string.IsNullOrWhiteSpace(hostNameOrAddress))
            {
                _cache.TryRemove(hostNameOrAddress.ToLowerInvariant(), out _);
            }
        }

        public void CleanupExpired()
        {
            var expiredKeys = new System.Collections.Generic.List<string>();
            
            foreach (var kvp in _cache)
            {
                if (kvp.Value.IsExpired)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }
        }

        private class CacheEntry
        {
            public IPAddress[] Addresses { get; }
            public DateTime ExpiresAt { get; }
            public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

            public CacheEntry(IPAddress[] addresses, DateTime expiresAt)
            {
                Addresses = addresses ?? throw new ArgumentNullException(nameof(addresses));
                ExpiresAt = expiresAt;
            }
        }
    }
}
