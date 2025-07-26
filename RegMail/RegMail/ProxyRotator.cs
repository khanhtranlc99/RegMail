using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RegMail
{
    public class ProxyRotator
    {
        private readonly ProxyManager _proxyManager;
        private readonly Dictionary<ProxyInfo, ProxyStats> _proxyStats;
        private readonly object _lockObject = new object();
        private readonly Random _random = new Random();

        public ProxyRotator(ProxyManager proxyManager)
        {
            _proxyManager = proxyManager;
            _proxyStats = new Dictionary<ProxyInfo, ProxyStats>();
        }

        public ProxyInfo GetNextProxy(RotationStrategy strategy = RotationStrategy.RoundRobin)
        {
            lock (_lockObject)
            {
                var availableProxies = _proxyManager.GetAllProxies();
                if (availableProxies.Count == 0)
                    return null;

                // Khởi tạo stats cho proxy mới
                foreach (var proxy in availableProxies)
                {
                    if (!_proxyStats.ContainsKey(proxy))
                    {
                        _proxyStats[proxy] = new ProxyStats();
                    }
                }

                // Loại bỏ proxy không còn trong danh sách
                var keysToRemove = _proxyStats.Keys.Where(k => !availableProxies.Contains(k)).ToList();
                foreach (var key in keysToRemove)
                {
                    _proxyStats.Remove(key);
                }

                ProxyInfo selectedProxy = null;

                switch (strategy)
                {
                    case RotationStrategy.RoundRobin:
                        selectedProxy = GetRoundRobinProxy(availableProxies);
                        break;
                    case RotationStrategy.Random:
                        selectedProxy = GetRandomProxy(availableProxies);
                        break;
                    case RotationStrategy.LeastUsed:
                        selectedProxy = GetLeastUsedProxy(availableProxies);
                        break;
                    case RotationStrategy.BestPerformance:
                        selectedProxy = GetBestPerformanceProxy(availableProxies);
                        break;
                    case RotationStrategy.WeightedRandom:
                        selectedProxy = GetWeightedRandomProxy(availableProxies);
                        break;
                }

                if (selectedProxy != null)
                {
                    _proxyStats[selectedProxy].UsageCount++;
                    _proxyStats[selectedProxy].LastUsed = DateTime.Now;
                    Console.WriteLine($"🔄 Sử dụng proxy ({strategy}): {selectedProxy}");
                }

                return selectedProxy;
            }
        }

        private ProxyInfo GetRoundRobinProxy(List<ProxyInfo> proxies)
        {
            // Tìm proxy có thời gian sử dụng lâu nhất
            var oldestUsed = _proxyStats.Values
                .Where(stats => stats.LastUsed.HasValue)
                .OrderBy(stats => stats.LastUsed)
                .FirstOrDefault();

            if (oldestUsed != null)
            {
                var proxy = _proxyStats.FirstOrDefault(kvp => kvp.Value == oldestUsed).Key;
                if (proxies.Contains(proxy))
                    return proxy;
            }

            // Nếu không có proxy nào được sử dụng, chọn proxy đầu tiên
            return proxies.FirstOrDefault();
        }

        private ProxyInfo GetRandomProxy(List<ProxyInfo> proxies)
        {
            return proxies[_random.Next(proxies.Count)];
        }

        private ProxyInfo GetLeastUsedProxy(List<ProxyInfo> proxies)
        {
            var leastUsed = _proxyStats
                .Where(kvp => proxies.Contains(kvp.Key))
                .OrderBy(kvp => kvp.Value.UsageCount)
                .FirstOrDefault();

            return leastUsed.Key ?? proxies.FirstOrDefault();
        }

        private ProxyInfo GetBestPerformanceProxy(List<ProxyInfo> proxies)
        {
            var bestPerformance = _proxyStats
                .Where(kvp => proxies.Contains(kvp.Key) && kvp.Value.SuccessRate > 0)
                .OrderByDescending(kvp => kvp.Value.SuccessRate)
                .ThenBy(kvp => kvp.Value.AverageResponseTime)
                .FirstOrDefault();

            return bestPerformance.Key ?? proxies.FirstOrDefault();
        }

        private ProxyInfo GetWeightedRandomProxy(List<ProxyInfo> proxies)
        {
            var availableStats = _proxyStats
                .Where(kvp => proxies.Contains(kvp.Key))
                .ToList();

            if (!availableStats.Any())
                return proxies.FirstOrDefault();

            // Tính tổng weight dựa trên success rate và response time
            double totalWeight = 0;
            var weightedProxies = new List<(ProxyInfo proxy, double weight)>();

            foreach (var kvp in availableStats)
            {
                double weight = CalculateWeight(kvp.Value);
                weightedProxies.Add((kvp.Key, weight));
                totalWeight += weight;
            }

            if (totalWeight <= 0)
                return proxies.FirstOrDefault();

            // Chọn proxy dựa trên weight
            double randomValue = _random.NextDouble() * totalWeight;
            double currentWeight = 0;

            foreach (var (proxy, weight) in weightedProxies)
            {
                currentWeight += weight;
                if (randomValue <= currentWeight)
                    return proxy;
            }

            return proxies.FirstOrDefault();
        }

        private double CalculateWeight(ProxyStats stats)
        {
            // Weight dựa trên success rate (70%) và response time (30%)
            double successWeight = stats.SuccessRate * 0.7;
            double responseWeight = Math.Max(0, 1 - (stats.AverageResponseTime / 10000)) * 0.3; // 10s = 0 weight
            return successWeight + responseWeight;
        }

        public void UpdateProxyStats(ProxyInfo proxy, bool success, long responseTimeMs = 0)
        {
            lock (_lockObject)
            {
                if (!_proxyStats.ContainsKey(proxy))
                    _proxyStats[proxy] = new ProxyStats();

                var stats = _proxyStats[proxy];
                stats.TotalRequests++;

                if (success)
                {
                    stats.SuccessfulRequests++;
                }

                // Cập nhật response time trung bình
                if (responseTimeMs > 0)
                {
                    stats.TotalResponseTime += responseTimeMs;
                    stats.AverageResponseTime = stats.TotalResponseTime / stats.TotalRequests;
                }

                stats.SuccessRate = (double)stats.SuccessfulRequests / stats.TotalRequests;
            }
        }

        public void MarkProxyAsFailed(ProxyInfo proxy)
        {
            UpdateProxyStats(proxy, false);
        }

        public void MarkProxyAsSuccess(ProxyInfo proxy, long responseTimeMs = 0)
        {
            UpdateProxyStats(proxy, true, responseTimeMs);
        }

        public List<ProxyInfo> GetWorkingProxies()
        {
            lock (_lockObject)
            {
                return _proxyStats
                    .Where(kvp => kvp.Value.SuccessRate > 0.5) // Success rate > 50%
                    .OrderByDescending(kvp => kvp.Value.SuccessRate)
                    .Select(kvp => kvp.Key)
                    .ToList();
            }
        }

        public void PrintProxyStats()
        {
            lock (_lockObject)
            {
                Console.WriteLine("\n=== THỐNG KÊ PROXY ===");
                foreach (var kvp in _proxyStats.OrderByDescending(x => x.Value.SuccessRate))
                {
                    var stats = kvp.Value;
                    Console.WriteLine($"Proxy: {kvp.Key}");
                    Console.WriteLine($"  - Sử dụng: {stats.UsageCount} lần");
                    Console.WriteLine($"  - Thành công: {stats.SuccessfulRequests}/{stats.TotalRequests} ({stats.SuccessRate:P1})");
                    Console.WriteLine($"  - Thời gian phản hồi TB: {stats.AverageResponseTime}ms");
                    Console.WriteLine($"  - Lần cuối sử dụng: {stats.LastUsed?.ToString("HH:mm:ss") ?? "Chưa sử dụng"}");
                    Console.WriteLine();
                }
            }
        }

        public void ResetStats()
        {
            lock (_lockObject)
            {
                _proxyStats.Clear();
            }
        }
    }

    public class ProxyStats
    {
        public int UsageCount { get; set; } = 0;
        public int TotalRequests { get; set; } = 0;
        public int SuccessfulRequests { get; set; } = 0;
        public double SuccessRate { get; set; } = 0.0;
        public long TotalResponseTime { get; set; } = 0;
        public long AverageResponseTime { get; set; } = 0;
        public DateTime? LastUsed { get; set; } = null;
    }

    public enum RotationStrategy
    {
        RoundRobin,      // Luân phiên theo thứ tự
        Random,          // Ngẫu nhiên
        LeastUsed,       // Ít sử dụng nhất
        BestPerformance, // Hiệu suất tốt nhất
        WeightedRandom   // Ngẫu nhiên có trọng số
    }
}