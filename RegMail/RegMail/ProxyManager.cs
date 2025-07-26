using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using OpenQA.Selenium.Chrome;

namespace RegMail
{
    public class ProxyInfo
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool IsAuthenticated => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

        public override string ToString()
        {
            if (IsAuthenticated)
                return $"{Host}:{Port} (User: {Username})";
            return $"{Host}:{Port}";
        }
    }

    public class ProxyManager
    {
        private List<ProxyInfo> _proxyList;
        private int _currentIndex = 0;
        private readonly object _lockObject = new object();
        private readonly string _proxyFilePath;

        public ProxyManager(string proxyFilePath = "proxies.txt")
        {
            _proxyFilePath = proxyFilePath;
            _proxyList = new List<ProxyInfo>();
            LoadProxies();
        }

        public void LoadProxies()
        {
            try
            {
                if (!File.Exists(_proxyFilePath))
                {
                    Console.WriteLine($"⚠️ File proxy không tồn tại: {_proxyFilePath}");
                    Console.WriteLine("📝 Tạo file proxy mẫu...");
                    CreateSampleProxyFile();
                    return;
                }

                _proxyList.Clear();
                string[] lines = File.ReadAllLines(_proxyFilePath);

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    ProxyInfo proxy = ParseProxyLine(line);
                    if (proxy != null)
                    {
                        _proxyList.Add(proxy);
                    }
                }

                Console.WriteLine($"✅ Đã tải {_proxyList.Count} proxy từ file {_proxyFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi tải file proxy: {ex.Message}");
            }
        }

        private ProxyInfo ParseProxyLine(string line)
        {
            try
            {
                // Format: host:port hoặc host:port:username:password
                string[] parts = line.Trim().Split(':');

                if (parts.Length < 2)
                {
                    Console.WriteLine($"⚠️ Dòng proxy không hợp lệ: {line}");
                    return null;
                }

                if (!int.TryParse(parts[1], out int port))
                {
                    Console.WriteLine($"⚠️ Port không hợp lệ: {parts[1]}");
                    return null;
                }

                var proxy = new ProxyInfo
                {
                    Host = parts[0],
                    Port = port
                };

                // Nếu có username và password
                if (parts.Length >= 4)
                {
                    proxy.Username = parts[2];
                    proxy.Password = parts[3];
                }

                return proxy;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi parse proxy line '{line}': {ex.Message}");
                return null;
            }
        }

        private void CreateSampleProxyFile()
        {
            try
            {
                string sampleContent = @"# File cấu hình proxy cho RegMail
# Format: host:port hoặc host:port:username:password
# Mỗi dòng một proxy, dòng bắt đầu bằng # là comment

# Ví dụ proxy không cần xác thực:
# 192.168.1.100:8080

# Ví dụ proxy cần xác thực:
# 192.168.1.100:8080:username:password

# Thêm proxy của bạn vào đây:
";

                File.WriteAllText(_proxyFilePath, sampleContent);
                Console.WriteLine($"✅ Đã tạo file proxy mẫu: {_proxyFilePath}");
                Console.WriteLine("📝 Vui lòng thêm proxy vào file và chạy lại chương trình");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Không thể tạo file proxy mẫu: {ex.Message}");
            }
        }

        public ProxyInfo GetNextProxy()
        {
            lock (_lockObject)
            {
                if (_proxyList.Count == 0)
                {
                    Console.WriteLine("⚠️ Không có proxy nào khả dụng");
                    return null;
                }

                var proxy = _proxyList[_currentIndex];
                _currentIndex = (_currentIndex + 1) % _proxyList.Count;

                Console.WriteLine($"🔄 Sử dụng proxy: {proxy}");
                return proxy;
            }
        }

        public ProxyInfo GetRandomProxy()
        {
            lock (_lockObject)
            {
                if (_proxyList.Count == 0)
                {
                    Console.WriteLine("⚠️ Không có proxy nào khả dụng");
                    return null;
                }

                Random random = new Random();
                int index = random.Next(_proxyList.Count);
                var proxy = _proxyList[index];

                Console.WriteLine($"🎲 Sử dụng proxy ngẫu nhiên: {proxy}");
                return proxy;
            }
        }

        public void ConfigureChromeOptions(ChromeOptions options, ProxyInfo proxy)
        {
            if (proxy == null)
            {
                Console.WriteLine("⚠️ Không có proxy để cấu hình");
                return;
            }

            try
            {
                // Cấu hình proxy cho Chrome
                string proxyString = proxy.IsAuthenticated
                    ? $"{proxy.Host}:{proxy.Port}:{proxy.Username}:{proxy.Password}"
                    : $"{proxy.Host}:{proxy.Port}";

                options.AddArgument($"--proxy-server={proxyString}");

                // Thêm các argument để tránh phát hiện automation
                options.AddArgument("--disable-blink-features=AutomationControlled");
                options.AddExcludedArgument("enable-automation");
                options.AddArgument("--disable-web-security");
                options.AddArgument("--allow-running-insecure-content");
                options.AddArgument("--disable-features=VizDisplayCompositor");

                // Cấu hình user agent để tránh phát hiện
                options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                Console.WriteLine($"✅ Đã cấu hình proxy cho Chrome: {proxy}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi cấu hình proxy cho Chrome: {ex.Message}");
            }
        }

        public async Task<bool> TestProxy(ProxyInfo proxy)
        {
            if (proxy == null) return false;

            try
            {
                var handler = new HttpClientHandler();

                if (proxy.IsAuthenticated)
                {
                    var credentials = new NetworkCredential(proxy.Username, proxy.Password);
                    handler.Proxy = new WebProxy(proxy.Host, proxy.Port)
                    {
                        Credentials = credentials
                    };
                }
                else
                {
                    handler.Proxy = new WebProxy(proxy.Host, proxy.Port);
                }

                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(10);

                    // Test với Google để kiểm tra proxy
                    var response = await client.GetAsync("https://www.google.com");
                    bool isSuccess = response.IsSuccessStatusCode;

                    Console.WriteLine($"🔍 Test proxy {proxy}: {(isSuccess ? "✅ Thành công" : "❌ Thất bại")}");
                    return isSuccess;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi test proxy {proxy}: {ex.Message}");
                return false;
            }
        }

        public async Task<List<ProxyInfo>> TestAllProxies()
        {
            var workingProxies = new List<ProxyInfo>();

            Console.WriteLine($"🔍 Bắt đầu test {_proxyList.Count} proxy...");

            foreach (var proxy in _proxyList)
            {
                bool isWorking = await TestProxy(proxy);
                if (isWorking)
                {
                    workingProxies.Add(proxy);
                }
            }

            Console.WriteLine($"✅ Tìm thấy {workingProxies.Count}/{_proxyList.Count} proxy hoạt động");
            return workingProxies;
        }

        public void AddProxy(string host, int port, string username = null, string password = null)
        {
            var proxy = new ProxyInfo
            {
                Host = host,
                Port = port,
                Username = username,
                Password = password
            };

            lock (_lockObject)
            {
                _proxyList.Add(proxy);
            }

            Console.WriteLine($"✅ Đã thêm proxy: {proxy}");
        }

        public void RemoveProxy(ProxyInfo proxy)
        {
            lock (_lockObject)
            {
                _proxyList.Remove(proxy);
            }

            Console.WriteLine($"🗑️ Đã xóa proxy: {proxy}");
        }

        public int GetProxyCount()
        {
            return _proxyList.Count;
        }

        public List<ProxyInfo> GetAllProxies()
        {
            return new List<ProxyInfo>(_proxyList);
        }
    }
}