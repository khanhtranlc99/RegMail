using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace RegMail
{
    /// <summary>
    /// ProxyPicker - Quản lý proxy với AutoAuth extension để xử lý authentication
    /// </summary>
    public class ProxyPicker : IDisposable
    {
        private readonly string _proxyFilePath;
        private List<ProxyInfo> _proxies;
        private readonly Random _random;
        private int _currentIndex = 0; // Index cho GetNextProxy()
        private readonly Dictionary<ProxyInfo, ProxyForwarder> _activeForwarders;

        public ProxyPicker(string proxyFilePath = null)
        {
            _proxyFilePath = proxyFilePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "proxies.txt");
            _proxies = new List<ProxyInfo>();
            _random = new Random();
            _activeForwarders = new Dictionary<ProxyInfo, ProxyForwarder>();
            LoadProxies();
        }

        /// <summary>
        /// Đọc danh sách proxy từ file, chuẩn hóa thành URL đầy đủ
        /// </summary>
        public void LoadProxies()
        {
            _proxies.Clear();
            
            if (!File.Exists(_proxyFilePath))
            {
                Console.WriteLine($"⚠️ File proxy không tồn tại: {_proxyFilePath}");
                CreateSampleProxyFile();
                return;
            }

            foreach (var raw in File.ReadAllLines(_proxyFilePath))
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) 
                    continue;

                var proxy = ParseProxyLine(line);
                if (proxy != null)
                {
                    _proxies.Add(proxy);
                }
            }

            Console.WriteLine($"✅ Đã tải {_proxies.Count} proxy từ file {_proxyFilePath}");
        }

        /// <summary>
        /// Parse một dòng proxy thành ProxyInfo
        /// </summary>
        private ProxyInfo ParseProxyLine(string line)
        {
            try
            {
                line = line.Trim();
                
                // Kiểm tra có prefix protocol không (socks5://, socks4://, http://, https://)
                ProxyType proxyType = ProxyType.HTTP; // Mặc định
                string actualLine = line;
                
                if (line.StartsWith("socks5://", StringComparison.OrdinalIgnoreCase))
                {
                    proxyType = ProxyType.SOCKS5;
                    actualLine = line.Substring(9); // Bỏ "socks5://"
                }
                else if (line.StartsWith("socks4://", StringComparison.OrdinalIgnoreCase))
                {
                    proxyType = ProxyType.SOCKS4;
                    actualLine = line.Substring(9); // Bỏ "socks4://"
                }
                else if (line.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    proxyType = ProxyType.HTTP;
                    actualLine = line.Substring(7); // Bỏ "http://"
                }
                else if (line.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    proxyType = ProxyType.HTTPS;
                    actualLine = line.Substring(8); // Bỏ "https://"
                }
                else
                {
                    // Không có prefix → mặc định là HTTP
                    proxyType = ProxyType.HTTP;
                    actualLine = line;
                }

                // Format: host:port hoặc host:port:username:password
                string[] parts = actualLine.Split(':');

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
                    Port = port,
                    Type = proxyType
                };

                // Nếu có username và password
                if (parts.Length >= 4)
                {
                    proxy.Username = parts[2];
                    proxy.Password = parts[3];
                }

                Console.WriteLine($"✅ Đã parse {proxyType} proxy: {proxy.Host}:{proxy.Port}" +
                    (proxy.IsAuthenticated ? $" (User: {proxy.Username})" : ""));
                Console.WriteLine($"🔍 Debug: Type={proxyType}, Chrome URL={proxy.GetChromeProxyUrl()}");
                
                return proxy;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi parse proxy line '{line}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse username:password từ userInfo string
        /// </summary>
        private void ParseUserPass(string userInfo, out string user, out string pass)
        {
            user = ""; 
            pass = "";
            
            if (string.IsNullOrEmpty(userInfo)) return;
            
            var idx = userInfo.IndexOf(':');
            if (idx > 0)
            {
                user = Uri.UnescapeDataString(userInfo.Substring(0, idx));
                pass = Uri.UnescapeDataString(userInfo.Substring(idx + 1));
            }
            else
            {
                user = Uri.UnescapeDataString(userInfo);
            }
        }

        /// <summary>
        /// Chọn proxy ngẫu nhiên
        /// </summary>
        public ProxyInfo GetRandomProxy()
        {
            if (_proxies.Count == 0)
            {
                Console.WriteLine("⚠️ Không có proxy nào khả dụng");
                return null;
            }

            var chosen = _proxies[_random.Next(_proxies.Count)];
            Console.WriteLine($"🎲 Chọn proxy ngẫu nhiên: {chosen}");
            return chosen;
        }

        /// <summary>
        /// Lấy proxy theo thứ tự (sequential)
        /// </summary>
        public ProxyInfo GetNextProxy()
        {
            if (_proxies.Count == 0)
            {
                Console.WriteLine("⚠️ Không có proxy nào khả dụng");
                return null;
            }

            // Sử dụng sequential thay vì random để dễ debug
            var chosen = _proxies[_currentIndex % _proxies.Count];
            _currentIndex++;
            
            Console.WriteLine($"🔄 Sử dụng proxy #{_currentIndex}: {chosen}");
            return chosen;
        }

        /// <summary>
        /// Lấy proxy ngẫu nhiên
        /// </summary>
        public ProxyInfo GetRandomProxy()
        {
            if (_proxies.Count == 0)
            {
                Console.WriteLine("⚠️ Không có proxy nào khả dụng");
                return null;
            }

            var chosen = _proxies[_random.Next(_proxies.Count)];
            Console.WriteLine($"🎲 Chọn proxy ngẫu nhiên: {chosen}");
            return chosen;
        }

        /// <summary>
        /// Tạo ChromeDriver với proxy và AutoAuth extension
        /// </summary>
        public IWebDriver CreateDriverWithProxy(ProxyInfo proxy, string userDataDir = null)
        {
            var options = new ChromeOptions();

            // Cấu hình user data directory
            if (!string.IsNullOrEmpty(userDataDir))
            {
                options.AddArgument($"--user-data-dir={userDataDir}");
                options.AddArgument("--profile-directory=Default");
            }

            // Cấu hình window
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--start-maximized");

            // Tắt automation detection
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);

            // Quyết định headless mode TRƯỚC khi cấu hình proxy
            bool shouldUseHeadless = DetermineHeadlessMode(proxy);
            
            // Cấu hình proxy (có thể ảnh hưởng đến quyết định headless)
            if (proxy != null)
            {
                ConfigureProxy(options, proxy, shouldUseHeadless);
            }

            // Áp dụng headless mode nếu được quyết định
            if (shouldUseHeadless)
            {
                options.AddArgument("--headless=new");
            }

            return new ChromeDriver(options);
        }

        /// <summary>
        /// Quyết định có nên dùng headless mode hay không dựa trên proxy
        /// </summary>
        private bool DetermineHeadlessMode(ProxyInfo proxy)
        {
            // Nếu không có proxy hoặc proxy không cần auth → có thể dùng headless
            if (proxy == null || !proxy.IsAuthenticated)
            {
                return ConfigManager.Chrome_Headless_Mode;
            }

            // Nếu proxy cần auth và là HTTP/HTTPS → ưu tiên dùng AutoAuth extension (non-headless)
            if (proxy.Type == ProxyType.HTTP || proxy.Type == ProxyType.HTTPS)
            {
                // Với HTTP/HTTPS + auth, ưu tiên AutoAuth extension (non-headless) vì:
                // 1. AutoAuth extension hoạt động tốt hơn trong non-headless
                // 2. Tránh xung đột với headless mode
                // 3. Chỉ dùng headless nếu có thể setup forwarder thành công
                Console.WriteLine("💡 HTTP/HTTPS + auth → ưu tiên AutoAuth extension (non-headless)");
                return false;
            }

            // SOCKS5 + auth → luôn cần forwarder, có thể dùng headless
            if (proxy.Type == ProxyType.SOCKS5)
            {
                Console.WriteLine("💡 SOCKS5 + auth → sẽ dùng forwarder, có thể headless");
                return ConfigManager.Chrome_Headless_Mode;
            }

            return ConfigManager.Chrome_Headless_Mode;
        }

        /// <summary>
        /// Cấu hình proxy cho ChromeOptions (public method để ChromeOptionsManager sử dụng)
        /// </summary>
        public void ConfigureProxyForOptions(ChromeOptions options, ProxyInfo proxy)
        {
            if (proxy == null) return;
            
            // Quyết định headless mode dựa trên proxy
            bool shouldUseHeadless = DetermineHeadlessMode(proxy);
            ConfigureProxy(options, proxy, shouldUseHeadless);
        }

        /// <summary>
        /// Cấu hình proxy cho ChromeOptions với chiến lược thông minh
        /// </summary>
        private void ConfigureProxy(ChromeOptions options, ProxyInfo proxy, bool shouldUseHeadless)
        {
            try
            {
                if (proxy.IsAuthenticated)
                {
                    // Chiến lược cho proxy có authentication
                    ConfigureAuthenticatedProxy(options, proxy, shouldUseHeadless);
                }
                else
                {
                    // Proxy không cần auth - đơn giản
                    ConfigureSimpleProxy(options, proxy);
                }

                // Bypass localhost cho tất cả
                options.AddArgument("--proxy-bypass-list=localhost,127.0.0.1");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi cấu hình proxy: {ex.Message}");
            }
        }

        /// <summary>
        /// Cấu hình proxy có authentication với chiến lược thông minh
        /// </summary>
        private void ConfigureAuthenticatedProxy(ChromeOptions options, ProxyInfo proxy, bool shouldUseHeadless)
        {
            Console.WriteLine($"🔧 Cấu hình {proxy.Type} proxy có auth: {proxy.Username}@{proxy.Host}:{proxy.Port}");
            
            // Chiến lược 1: SOCKS5 + auth → không hỗ trợ, dùng forwarder
            if (proxy.Type == ProxyType.SOCKS5)
            {
                Console.WriteLine("⚠️ SOCKS5 + auth không được Chrome hỗ trợ. Dùng forwarder HTTP->SOCKS5.");
                if (TrySetupLocalForwarder(options, proxy))
                {
                    Console.WriteLine("✅ Đã setup local forwarder cho SOCKS5 auth");
                    return;
                }
                else
                {
                    throw new NotSupportedException("Không thể setup forwarder cho SOCKS5 auth. Vui lòng dùng HTTP proxy.");
                }
            }

            // Chiến lược 2: HTTP/HTTPS + auth
            if (proxy.Type == ProxyType.HTTP || proxy.Type == ProxyType.HTTPS)
            {
                // Thử nhúng user:pass@ trong URL trước
                if (TryEmbedCredentialsInUrl(options, proxy))
                {
                    Console.WriteLine("✅ Đã nhúng credentials vào URL proxy");
                    return;
                }

                // Nếu headless mode được yêu cầu → thử dùng forwarder
                if (shouldUseHeadless)
                {
                    if (TrySetupLocalForwarder(options, proxy))
                    {
                        Console.WriteLine("✅ Đã setup local forwarder cho headless mode");
                        return;
                    }
                    else
                    {
                        Console.WriteLine("⚠️ Không thể setup forwarder cho headless mode");
                        Console.WriteLine("💡 Fallback về AutoAuth extension với non-headless mode");
                        // Fallback về AutoAuth extension (non-headless)
                        ConfigureAutoAuthExtension(options, proxy);
                    }
                }
                else
                {
                    // Non-headless → dùng AutoAuth extension
                    Console.WriteLine("💡 Sử dụng AutoAuth extension với non-headless mode");
                    ConfigureAutoAuthExtension(options, proxy);
                }
            }
        }

        /// <summary>
        /// Cấu hình proxy đơn giản (không auth)
        /// </summary>
        private void ConfigureSimpleProxy(ChromeOptions options, ProxyInfo proxy)
        {
            string proxyServerArg = ProxyServerArg(proxy);
            options.AddArgument($"--proxy-server={proxyServerArg}");

            Console.WriteLine($"✅ Đã cấu hình {proxy.Type} proxy: {proxyServerArg}");
        }

        /// <summary>
        /// Thử nhúng credentials vào URL proxy (chỉ cho HTTP/HTTPS)
        /// </summary>
        private bool TryEmbedCredentialsInUrl(ChromeOptions options, ProxyInfo proxy)
        {
            try
            {
                // Chỉ thử với HTTP/HTTPS proxy
                if (proxy.Type != ProxyType.HTTP && proxy.Type != ProxyType.HTTPS)
                    return false;

                Console.WriteLine($"🔍 Thử nhúng credentials vào URL cho {proxy.Type} proxy");
                
                // Tạo URL với user:pass@host:port
                string proxyUrl = $"http://{proxy.Username}:{proxy.Password}@{proxy.Host}:{proxy.Port}";
                options.AddArgument($"--proxy-server={proxyUrl}");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Không thể nhúng credentials vào URL: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Setup local forwarder cho headless mode hoặc SOCKS5 auth
        /// </summary>
        private bool TrySetupLocalForwarder(ChromeOptions options, ProxyInfo proxy)
        {
            try
            {
                // Tạo và khởi động ProxyForwarder
                var forwarder = new ProxyForwarder(proxy);
                forwarder.StartAsync().Wait(5000); // Đợi tối đa 5 giây

                // Lưu forwarder instance để có thể dispose sau
                if (!_activeForwarders.ContainsKey(proxy))
                {
                    _activeForwarders[proxy] = forwarder;
                }

                // Cấu hình Chrome dùng local forwarder
                options.AddArgument($"--proxy-server={forwarder.LocalProxyUrl}");
                
                Console.WriteLine($"🔧 Đã setup ProxyForwarder tại {forwarder.LocalProxyUrl}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Không thể setup ProxyForwarder: {ex.Message}");
                return false;
            }
        }



        /// <summary>
        /// Cấu hình AutoAuth extension
        /// </summary>
        private void ConfigureAutoAuthExtension(ChromeOptions options, ProxyInfo proxy)
        {
            Console.WriteLine($"🔧 Tạo AutoAuth extension cho {proxy.Type} proxy {proxy.Username}@{proxy.Host}:{proxy.Port}");
            
            // Cấu hình proxy server không có credentials
            string proxyServerArg = ProxyServerArg(proxy);
            options.AddArgument($"--proxy-server={proxyServerArg}");
            
            // Tạo và thêm AutoAuth extension
            string extZip = BuildAutoAuthExtensionZip(proxy.Username, proxy.Password);
            if (!string.IsNullOrEmpty(extZip))
            {
                options.AddExtension(extZip);
                Console.WriteLine($"✅ Đã thêm AutoAuth extension");
            }
        }

        /// <summary>
        /// Lấy URL cho Chrome --proxy-server argument
        /// </summary>
        private string ProxyServerArg(ProxyInfo proxy)
        {
            // Sử dụng method GetChromeProxyUrl() để đảm bảo consistency
            return proxy.GetChromeProxyUrl();
        }

        /// <summary>
        /// Tạo AutoAuth extension để tự động điền auth proxy
        /// </summary>
        private string BuildAutoAuthExtensionZip(string username, string password)
        {
            try
            {
                // Tạo thư mục tạm
                string tempDir = Path.Combine(Path.GetTempPath(), "AutoAuth_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                // manifest.json (MV2 cho tương thích rộng)
                string manifest = @"{
  ""version"": ""1.0.0"",
  ""manifest_version"": 2,
  ""name"": ""AutoAuth Proxy"",
  ""permissions"": [""webRequest"", ""webRequestBlocking"", ""<all_urls>""],
  ""background"": { ""scripts"": [""background.js""] }
}";
                File.WriteAllText(Path.Combine(tempDir, "manifest.json"), manifest);

                // background.js – tự động trả về authCredentials
                string bg = $@"chrome.webRequest.onAuthRequired.addListener(
  function (details) {{
    return {{ authCredentials: {{ username: ""{EscapeJs(username)}"", password: ""{EscapeJs(password)}"" }} }};
  }},
  {{ urls: [""<all_urls>""] }},
  ['blocking']
);";
                File.WriteAllText(Path.Combine(tempDir, "background.js"), bg);

                // Nén thành zip
                string zipPath = Path.Combine(Path.GetTempPath(), "AutoAuth_" + Guid.NewGuid().ToString("N") + ".zip");
                ZipFile.CreateFromDirectory(tempDir, zipPath);

                // Dọn dẹp thư mục tạm
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch { /* Ignore cleanup errors */ }

                Console.WriteLine($"✅ Đã tạo AutoAuth extension: {zipPath}");
                return zipPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi tạo AutoAuth extension: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Escape string cho JavaScript
        /// </summary>
        private string EscapeJs(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        /// <summary>
        /// Test proxy bằng ProxyTester nhẹ và chính xác
        /// </summary>
        public async Task<bool> TestProxy(ProxyInfo proxy)
        {
            if (proxy == null) return false;

            try
            {
                Console.WriteLine($"🔍 Test proxy: {proxy}");
                
                // Sử dụng ProxyTester thay vì Selenium (nhẹ và nhanh hơn)
                bool isWorking = await ProxyTester.TestProxy(proxy, 10); // 10 giây timeout
                
                if (isWorking)
                {
                    Console.WriteLine($"✅ Proxy test thành công: {proxy}");
                }
                else
                {
                    Console.WriteLine($"❌ Proxy test thất bại: {proxy}");
                }
                
                return isWorking;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi test proxy {proxy}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Test tất cả proxy bằng ProxyTester
        /// </summary>
        public async Task<List<ProxyInfo>> TestAllProxies()
        {
            Console.WriteLine($"🔍 Bắt đầu test {_proxies.Count} proxy...");
            
            // Sử dụng ProxyTester để test tất cả proxy
            var workingProxies = await ProxyTester.TestAllProxies(_proxies, 10); // 10 giây timeout
            
            return workingProxies;
        }

        /// <summary>
        /// Tạo file proxy mẫu
        /// </summary>
        private void CreateSampleProxyFile()
        {
            try
            {
                                 string sampleContent = @"# File cấu hình proxy cho RegMail
 # Format có thể là:
 # 1. host:port (HTTP proxy không auth)
 # 2. host:port:username:password (HTTP proxy có auth)  
 # 3. http://host:port (HTTP proxy rõ ràng)
 # 4. https://host:port (HTTPS proxy - Chrome sẽ dùng http:// scheme)
 # 5. socks4://host:port (SOCKS4 proxy không auth)
 # 6. socks5://host:port (SOCKS5 proxy không auth)
 # 7. socks4://host:port:username:password (SOCKS4 proxy có auth)
 # 8. socks5://host:port:username:password (SOCKS5 proxy có auth - Chrome sẽ dùng extension)

 # ============================================
 # VÍ DỤ HTTP/HTTPS PROXY:
 # ============================================
 # 192.168.1.100:8080
 # http://proxy.example.com:3128:user:pass
 # https://https-proxy.example.com:3128:user:pass  # HTTPS proxy vẫn dùng http:// cho Chrome

 # ============================================
 # VÍ DỤ SOCKS4 PROXY:
 # ============================================
 # socks4://127.0.0.1:1080
 # socks4://proxy.example.com:1080:user:pass  # Auth sẽ được xử lý bởi extension

 # ============================================
 # VÍ DỤ SOCKS5 PROXY (KHUYẾN NGHỊ):
 # ============================================
 # socks5://127.0.0.1:1080
 # socks5://proxy.example.com:1080:user:pass  # Auth sẽ được xử lý bởi extension

# ============================================
# PROXY CỦA BẠN:
# ============================================

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

        /// <summary>
        /// Thêm proxy mới
        /// </summary>
        public void AddProxy(string host, int port, string username = null, string password = null, ProxyType type = ProxyType.HTTP)
        {
            var proxy = new ProxyInfo
            {
                Host = host,
                Port = port,
                Username = username,
                Password = password,
                Type = type
            };

            _proxies.Add(proxy);
            Console.WriteLine($"✅ Đã thêm {type} proxy: {proxy}");
        }

        /// <summary>
        /// Lấy số lượng proxy
        /// </summary>
        public int GetProxyCount()
        {
            return _proxies.Count;
        }

        /// <summary>
        /// Lấy tất cả proxy
        /// </summary>
        public List<ProxyInfo> GetAllProxies()
        {
            return new List<ProxyInfo>(_proxies);
        }

        /// <summary>
        /// Dispose resources và dừng tất cả forwarder
        /// </summary>
        public void Dispose()
        {
            try
            {
                foreach (var forwarder in _activeForwarders.Values)
                {
                    try
                    {
                        forwarder.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Lỗi khi dispose forwarder: {ex.Message}");
                    }
                }
                _activeForwarders.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi dispose ProxyPicker: {ex.Message}");
            }
        }
    }
}
