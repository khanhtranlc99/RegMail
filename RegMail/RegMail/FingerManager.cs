using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenQA.Selenium.Chrome;

namespace RegMail
{
    public class FingerprintInfo
    {
        public string UserAgent { get; set; }
        public string Language { get; set; }
        public string Platform { get; set; }
        public string WebGLVendor { get; set; }
        public string WebGLRenderer { get; set; }
        public string CanvasFingerprint { get; set; }
        public string ScreenResolution { get; set; }
        public string ColorDepth { get; set; }
        public string Timezone { get; set; }
        public string AcceptLanguage { get; set; }
        public string AcceptEncoding { get; set; }
        public string ConnectionType { get; set; }
        public string DeviceMemory { get; set; }
        public string HardwareConcurrency { get; set; }
        public string TouchSupport { get; set; }
        public string DoNotTrack { get; set; }
        public string ProfileName { get; set; }
    }

    public class FingerprintManager
    {
        private static readonly Random _random = new Random();

        // Danh sách User Agent ngẫu nhiên - Mở rộng
        private static readonly string[] UserAgents = {
            // Windows Chrome
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36",
            
            // Windows Edge
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36 Edg/119.0.0.0",
            
            // Windows Firefox
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:120.0) Gecko/20100101 Firefox/120.0",
            
            // Mac Chrome
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36",
            
            // Mac Safari
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Safari/605.1.15",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15",
            
            // Linux Chrome
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36",
            
            // Linux Firefox
            "Mozilla/5.0 (X11; Linux x86_64; rv:121.0) Gecko/20100101 Firefox/121.0",
            "Mozilla/5.0 (X11; Linux x86_64; rv:120.0) Gecko/20100101 Firefox/120.0",
            
            // Mobile Android Chrome
            "Mozilla/5.0 (Linux; Android 13; SM-G991B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36",
            "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36",
            "Mozilla/5.0 (Linux; Android 12; SM-G998B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Mobile Safari/537.36",
            
            // Mobile iOS Safari
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Mobile/15E148 Safari/604.1",
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1"
        };

        // Danh sách ngôn ngữ mở rộng
        private static readonly string[] Languages = {
            // English variants
            "en-US,en;q=0.9",
            "en-GB,en;q=0.9",
            "en-CA,en;q=0.9",
            "en-AU,en;q=0.9",
            "en-NZ,en;q=0.9",
            "en-IN,en;q=0.9",
        };

        // Danh sách platform mở rộng
        private static readonly string[] Platforms = {
            "Win32",
            "MacIntel",
            "Linux x86_64",
            "Linux armv8l",
            "Linux aarch64"
        };

        // Danh sách WebGL Vendor mở rộng
        private static readonly string[] WebGLVendors = {
            "Google Inc. (Intel)",
            "Google Inc. (NVIDIA)",
            "Google Inc. (AMD)",
            "Intel Inc.",
            "NVIDIA Corporation",
            "AMD",
            "Apple Inc.",
            "ARM",
            "Qualcomm",
            "Imagination Technologies",
            "Broadcom",
            "Mesa/X.org"
        };

        // Danh sách WebGL Renderer mở rộng
        private static readonly string[] WebGLRenderers = {
            // Intel
            "ANGLE (Intel, Intel(R) UHD Graphics 620 Direct3D11 vs_5_0 ps_5_0, D3D11)",
            "ANGLE (Intel, Intel(R) UHD Graphics 630 Direct3D11 vs_5_0 ps_5_0, D3D11)",
            "ANGLE (Intel, Intel(R) Iris(R) Xe Graphics Direct3D11 vs_5_0 ps_5_0, D3D11)",
            "Intel(R) UHD Graphics 620",
            "Intel(R) UHD Graphics 630",
            "Intel(R) Iris(R) Xe Graphics",
            
            // NVIDIA
            "ANGLE (NVIDIA, NVIDIA GeForce GTX 1060 Direct3D11 vs_5_0 ps_5_0, D3D11)",
            "ANGLE (NVIDIA, NVIDIA GeForce RTX 3060 Direct3D11 vs_5_0 ps_5_0, D3D11)",
            "ANGLE (NVIDIA, NVIDIA GeForce RTX 4070 Direct3D11 vs_5_0 ps_5_0, D3D11)",
            "NVIDIA GeForce GTX 1060",
            "NVIDIA GeForce RTX 3060",
            "NVIDIA GeForce RTX 4070",
            
            // AMD
            "ANGLE (AMD, AMD Radeon RX 580 Direct3D11 vs_5_0 ps_5_0, D3D11)",
            "ANGLE (AMD, AMD Radeon RX 6600 Direct3D11 vs_5_0 ps_5_0, D3D11)",
            "ANGLE (AMD, AMD Radeon RX 7600 Direct3D11 vs_5_0 ps_5_0, D3D11)",
            "AMD Radeon RX 580",
            "AMD Radeon RX 6600",
            "AMD Radeon RX 7600",
            
            // Apple
            "Apple M1",
            "Apple M2",
            "Apple M2 Pro",
            "Apple M2 Max",
            
            // Mobile
            "Mali-G78 MC14",
            "Adreno 650",
            "PowerVR GT7600",
            "Mali-G610 MC6"
        };

        // Danh sách độ phân giải màn hình mở rộng
        private static readonly string[] ScreenResolutions = {
            // Desktop resolutions
            "1920x1080",
            "1366x768",
            "1440x900",
            "1536x864",
            "2560x1440",
            "3840x2160",
            "2560x1600",
            "1920x1200",
            "1680x1050",
            "1600x900",
            "1280x720",
            "1024x768",
            
            // Laptop resolutions
            "1366x768",
            "1920x1080",
            "2560x1440",
            "3200x1800",
            "3840x2160",
            
            // Mobile resolutions
            "360x640",
            "375x667",
            "414x896",
            "390x844",
            "428x926",
            "393x851"
        };

        // Danh sách timezone mở rộng
        private static readonly string[] Timezones = {
            // North America
            "America/New_York",
            "America/Los_Angeles",
            "America/Chicago",
            "America/Denver",
            "America/Phoenix",
            "America/Anchorage",
            "America/Honolulu",
            "America/Toronto",
            "America/Vancouver",
            "America/Montreal",
            
            // Europe
            "Europe/London",
            "Europe/Paris",
            "Europe/Berlin",
            "Europe/Rome",
            "Europe/Madrid",
            "Europe/Amsterdam",
            "Europe/Brussels",
            "Europe/Vienna",
            "Europe/Zurich",
            "Europe/Stockholm",
            "Europe/Oslo",
            "Europe/Copenhagen",
            "Europe/Helsinki",
            "Europe/Warsaw",
            "Europe/Prague",
            "Europe/Budapest",
            "Europe/Bucharest",
            "Europe/Sofia",
            "Europe/Athens",
            "Europe/Istanbul",
            "Europe/Moscow",
            
            // Asia
            "Asia/Tokyo",
            "Asia/Shanghai",
            "Asia/Seoul",
            "Asia/Singapore",
            "Asia/Bangkok",
            "Asia/Ho_Chi_Minh",
            "Asia/Jakarta",
            "Asia/Manila",
            "Asia/Kuala_Lumpur",
            "Asia/Hong_Kong",
            "Asia/Taipei",
            "Asia/Dubai",
            "Asia/Kolkata",
            "Asia/Dhaka",
            "Asia/Karachi",
            "Asia/Tehran",
            "Asia/Jerusalem",
            "Asia/Riyadh",
            
            // Oceania
            "Australia/Sydney",
            "Australia/Melbourne",
            "Australia/Brisbane",
            "Australia/Perth",
            "Australia/Adelaide",
            "Pacific/Auckland",
            "Pacific/Fiji",
            
            // South America
            "America/Sao_Paulo",
            "America/Buenos_Aires",
            "America/Santiago",
            "America/Lima",
            "America/Bogota",
            "America/Caracas",
            "America/Mexico_City"
        };

        // Các profile fingerprint được định nghĩa sẵn
        public static FingerprintInfo GetWindowsProfile()
        {
            return new FingerprintInfo
            {
                ProfileName = "Windows Desktop",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                Language = "en-US,en;q=0.9",
                Platform = "Win32",
                WebGLVendor = "Google Inc. (Intel)",
                WebGLRenderer = "ANGLE (Intel, Intel(R) UHD Graphics 620 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                CanvasFingerprint = GenerateRandomCanvasFingerprint(),
                ScreenResolution = "1920x1080",
                ColorDepth = "24",
                Timezone = "America/New_York",
                AcceptLanguage = "en-US,en;q=0.9",
                AcceptEncoding = "gzip, deflate, br",
                ConnectionType = "4g",
                DeviceMemory = "8",
                HardwareConcurrency = "8",
                TouchSupport = "false",
                DoNotTrack = "1"
            };
        }

        public static FingerprintInfo GetMacProfile()
        {
            return new FingerprintInfo
            {
                ProfileName = "Mac Desktop",
                UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                Language = "en-US,en;q=0.9",
                Platform = "MacIntel",
                WebGLVendor = "Apple Inc.",
                WebGLRenderer = "Apple M1",
                CanvasFingerprint = GenerateRandomCanvasFingerprint(),
                ScreenResolution = "2560x1600",
                ColorDepth = "24",
                Timezone = "America/Los_Angeles",
                AcceptLanguage = "en-US,en;q=0.9",
                AcceptEncoding = "gzip, deflate, br",
                ConnectionType = "4g",
                DeviceMemory = "16",
                HardwareConcurrency = "10",
                TouchSupport = "false",
                DoNotTrack = "1"
            };
        }

        public static FingerprintInfo GetLinuxProfile()
        {
            return new FingerprintInfo
            {
                ProfileName = "Linux Desktop",
                UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                Language = "en-US,en;q=0.9",
                Platform = "Linux x86_64",
                WebGLVendor = "Mesa/X.org",
                WebGLRenderer = "Mesa Intel(R) UHD Graphics 620 (CFL GT2)",
                CanvasFingerprint = GenerateRandomCanvasFingerprint(),
                ScreenResolution = "1920x1080",
                ColorDepth = "24",
                Timezone = "Europe/London",
                AcceptLanguage = "en-US,en;q=0.9",
                AcceptEncoding = "gzip, deflate, br",
                ConnectionType = "4g",
                DeviceMemory = "8",
                HardwareConcurrency = "8",
                TouchSupport = "false",
                DoNotTrack = "1"
            };
        }

        public static FingerprintInfo GetAndroidProfile()
        {
            return new FingerprintInfo
            {
                ProfileName = "Android Mobile",
                UserAgent = "Mozilla/5.0 (Linux; Android 13; SM-G991B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36",
                Language = "en-US,en;q=0.9",
                Platform = "Linux armv8l",
                WebGLVendor = "ARM",
                WebGLRenderer = "Mali-G78 MC14",
                CanvasFingerprint = GenerateRandomCanvasFingerprint(),
                ScreenResolution = "360x640",
                ColorDepth = "24",
                Timezone = "America/New_York",
                AcceptLanguage = "en-US,en;q=0.9",
                AcceptEncoding = "gzip, deflate, br",
                ConnectionType = "4g",
                DeviceMemory = "4",
                HardwareConcurrency = "8",
                TouchSupport = "true",
                DoNotTrack = "1"
            };
        }

        public static FingerprintInfo GetIOSProfile()
        {
            return new FingerprintInfo
            {
                ProfileName = "iOS Mobile",
                UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Mobile/15E148 Safari/604.1",
                Language = "en-US,en;q=0.9",
                Platform = "iPhone",
                WebGLVendor = "Apple Inc.",
                WebGLRenderer = "Apple GPU",
                CanvasFingerprint = GenerateRandomCanvasFingerprint(),
                ScreenResolution = "375x667",
                ColorDepth = "24",
                Timezone = "America/Los_Angeles",
                AcceptLanguage = "en-US,en;q=0.9",
                AcceptEncoding = "gzip, deflate, br",
                ConnectionType = "4g",
                DeviceMemory = "4",
                HardwareConcurrency = "6",
                TouchSupport = "true",
                DoNotTrack = "1"
            };
        }

        public static FingerprintInfo GetEuropeanProfile()
        {
            return new FingerprintInfo
            {
                ProfileName = "European Desktop",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                Language = "de-DE,de;q=0.9,en;q=0.8",
                Platform = "Win32",
                WebGLVendor = "Google Inc. (NVIDIA)",
                WebGLRenderer = "ANGLE (NVIDIA, NVIDIA GeForce GTX 1060 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                CanvasFingerprint = GenerateRandomCanvasFingerprint(),
                ScreenResolution = "1920x1080",
                ColorDepth = "24",
                Timezone = "Europe/Berlin",
                AcceptLanguage = "de-DE,de;q=0.9,en;q=0.8",
                AcceptEncoding = "gzip, deflate, br",
                ConnectionType = "4g",
                DeviceMemory = "16",
                HardwareConcurrency = "12",
                TouchSupport = "false",
                DoNotTrack = "1"
            };
        }

        public static FingerprintInfo GetAsianProfile()
        {
            return new FingerprintInfo
            {
                ProfileName = "Asian Desktop",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                Language = "ja-JP,ja;q=0.9,en;q=0.8",
                Platform = "Win32",
                WebGLVendor = "Google Inc. (AMD)",
                WebGLRenderer = "ANGLE (AMD, AMD Radeon RX 580 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                CanvasFingerprint = GenerateRandomCanvasFingerprint(),
                ScreenResolution = "2560x1440",
                ColorDepth = "24",
                Timezone = "Asia/Tokyo",
                AcceptLanguage = "ja-JP,ja;q=0.9,en;q=0.8",
                AcceptEncoding = "gzip, deflate, br",
                ConnectionType = "4g",
                DeviceMemory = "32",
                HardwareConcurrency = "16",
                TouchSupport = "false",
                DoNotTrack = "1"
            };
        }

        public static FingerprintInfo GetGamingProfile()
        {
            return new FingerprintInfo
            {
                ProfileName = "Gaming Desktop",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                Language = "en-US,en;q=0.9",
                Platform = "Win32",
                WebGLVendor = "NVIDIA Corporation",
                WebGLRenderer = "ANGLE (NVIDIA, NVIDIA GeForce RTX 4070 Direct3D11 vs_5_0 ps_5_0, D3D11)",
                CanvasFingerprint = GenerateRandomCanvasFingerprint(),
                ScreenResolution = "2560x1440",
                ColorDepth = "24",
                Timezone = "America/New_York",
                AcceptLanguage = "en-US,en;q=0.9",
                AcceptEncoding = "gzip, deflate, br",
                ConnectionType = "4g",
                DeviceMemory = "32",
                HardwareConcurrency = "16",
                TouchSupport = "false",
                DoNotTrack = "1"
            };
        }

        public static FingerprintInfo GetBusinessProfile()
        {
            return new FingerprintInfo
            {
                ProfileName = "Business Desktop",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                Language = "en-US,en;q=0.9",
                Platform = "Win32",
                WebGLVendor = "Intel Inc.",
                WebGLRenderer = "ANGLE (Intel, Intel(R) Iris(R) Xe Graphics Direct3D11 vs_5_0 ps_5_0, D3D11)",
                CanvasFingerprint = GenerateRandomCanvasFingerprint(),
                ScreenResolution = "1920x1080",
                ColorDepth = "24",
                Timezone = "America/Chicago",
                AcceptLanguage = "en-US,en;q=0.9",
                AcceptEncoding = "gzip, deflate, br",
                ConnectionType = "4g",
                DeviceMemory = "16",
                HardwareConcurrency = "12",
                TouchSupport = "false",
                DoNotTrack = "1"
            };
        }

        public static FingerprintInfo GetStudentProfile()
        {
            return new FingerprintInfo
            {
                ProfileName = "Student Laptop",
                UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                Language = "en-US,en;q=0.9",
                Platform = "MacIntel",
                WebGLVendor = "Apple Inc.",
                WebGLRenderer = "Apple M2",
                CanvasFingerprint = GenerateRandomCanvasFingerprint(),
                ScreenResolution = "1440x900",
                ColorDepth = "24",
                Timezone = "America/Los_Angeles",
                AcceptLanguage = "en-US,en;q=0.9",
                AcceptEncoding = "gzip, deflate, br",
                ConnectionType = "4g",
                DeviceMemory = "8",
                HardwareConcurrency = "8",
                TouchSupport = "false",
                DoNotTrack = "1"
            };
        }

        // Danh sách tất cả các profile có sẵn
        public static List<FingerprintInfo> GetAllProfiles()
        {
            return new List<FingerprintInfo>
            {
                GetWindowsProfile(),
                GetMacProfile(),
                GetLinuxProfile(),
                GetAndroidProfile(),
                GetIOSProfile(),
                GetEuropeanProfile(),
                GetAsianProfile(),
                GetGamingProfile(),
                GetBusinessProfile(),
                GetStudentProfile()
            };
        }

        // Lấy profile ngẫu nhiên từ danh sách có sẵn
        public static FingerprintInfo GetRandomProfile()
        {
            var profiles = GetAllProfiles();
            return profiles[_random.Next(profiles.Count)];
        }

        // Lấy profile theo tên
        public static FingerprintInfo GetProfileByName(string profileName)
        {
            var profiles = GetAllProfiles();
            return profiles.FirstOrDefault(p => p.ProfileName.Equals(profileName, StringComparison.OrdinalIgnoreCase));
        }

        public static FingerprintInfo GenerateRandomFingerprint()
        {
            return new FingerprintInfo
            {
                ProfileName = "Random Generated",
                UserAgent = UserAgents[_random.Next(UserAgents.Length)],
                Language = Languages[_random.Next(Languages.Length)],
                Platform = Platforms[_random.Next(Platforms.Length)],
                WebGLVendor = WebGLVendors[_random.Next(WebGLVendors.Length)],
                WebGLRenderer = WebGLRenderers[_random.Next(WebGLRenderers.Length)],
                CanvasFingerprint = GenerateRandomCanvasFingerprint(),
                ScreenResolution = ScreenResolutions[_random.Next(ScreenResolutions.Length)],
                ColorDepth = "24",
                Timezone = Timezones[_random.Next(Timezones.Length)],
                AcceptLanguage = Languages[_random.Next(Languages.Length)],
                AcceptEncoding = "gzip, deflate, br",
                ConnectionType = "4g",
                DeviceMemory = _random.Next(4, 33).ToString(),
                HardwareConcurrency = _random.Next(4, 17).ToString(),
                TouchSupport = _random.Next(2) == 0 ? "true" : "false",
                DoNotTrack = "1"
            };
        }

        private static string GenerateRandomCanvasFingerprint()
        {
            // Tạo một chuỗi ngẫu nhiên để mô phỏng canvas fingerprint
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, 32).Select(s => s[_random.Next(s.Length)]).ToArray());
        }

        public static void ConfigureChromeOptions(ChromeOptions options, FingerprintInfo fingerprint)
        {
            if (fingerprint == null)
            {
                fingerprint = GenerateRandomFingerprint();
            }

            // Xóa tất cả fingerprint cũ
            ClearOldFingerprint(options);

            // Cấu hình fingerprint mới
            ConfigureNewFingerprint(options, fingerprint);
        }

        private static void ClearOldFingerprint(ChromeOptions options)
        {
            // Xóa các argument cũ liên quan đến fingerprint
            var argumentsToRemove = new List<string>
            {
                "--user-agent",
                "--lang",
                "--accept-language",
                "--accept-encoding",
                "--disable-web-security",
                "--disable-features",
                "--disable-blink-features"
            };

            foreach (var arg in argumentsToRemove)
            {
                options.AddArgument(arg + "=");
            }
        }

        private static void ConfigureNewFingerprint(ChromeOptions options, FingerprintInfo fingerprint)
        {
            // Cấu hình User Agent
            options.AddArgument($"--user-agent={fingerprint.UserAgent}");

            // Cấu hình ngôn ngữ
            options.AddArgument($"--lang={fingerprint.Language.Split(',')[0]}");

            // Cấu hình Accept Language
            options.AddArgument($"--accept-language={fingerprint.AcceptLanguage}");

            // Cấu hình Accept Encoding
            options.AddArgument($"--accept-encoding={fingerprint.AcceptEncoding}");

            // Cấu hình timezone
            options.AddArgument($"--timezone={fingerprint.Timezone}");

            // Cấu hình screen resolution
            var resolution = fingerprint.ScreenResolution.Split('x');
            if (resolution.Length == 2)
            {
                options.AddArgument($"--window-size={resolution[0]},{resolution[1]}");
            }

            // Cấu hình để tránh phát hiện automation
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--allow-running-insecure-content");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-plugins");
            options.AddArgument("--disable-images");
            options.AddArgument("--disable-javascript");
            options.AddArgument("--disable-default-apps");
            options.AddArgument("--disable-sync");
            options.AddArgument("--disable-background-networking");
            options.AddArgument("--disable-background-timer-throttling");
            options.AddArgument("--disable-client-side-phishing-detection");
            options.AddArgument("--disable-component-extensions-with-background-pages");
            options.AddArgument("--disable-hang-monitor");
            options.AddArgument("--disable-ipc-flooding-protection");
            options.AddArgument("--disable-renderer-backgrounding");
            options.AddArgument("--disable-backgrounding-occluded-windows");
            options.AddArgument("--disable-features=TranslateUI");
            options.AddArgument("--disable-ignore-certificate-errors");
            options.AddArgument("--disable-extensions-file-access-check");
            options.AddArgument("--disable-extensions-http-throttling");
            options.AddArgument("--disable-features=site-per-process");
            options.AddArgument("--disable-site-isolation-trials");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-features=TranslateUI");
            options.AddArgument("--disable-features=BlinkGenPropertyTrees");
            options.AddArgument("--disable-features=ImprovedCookieControls");
            options.AddArgument("--disable-features=SameSiteByDefaultCookies");
            options.AddArgument("--disable-features=CookiesWithoutSameSiteMustBeSecure");
            options.AddArgument("--disable-features=AutoupgradeMixedContent");
            options.AddArgument("--disable-features=AutoupgradeImageAds");
            options.AddArgument("--disable-features=AutoupgradeMixedContent");
            options.AddArgument("--disable-features=AutoupgradeImageAds");
            options.AddArgument("--disable-features=AutoupgradeMixedContent");
            options.AddArgument("--disable-features=AutoupgradeImageAds");

            // Thêm các preference để thay đổi fingerprint
            options.AddUserProfilePreference("profile.default_content_setting_values.notifications", 2);
            options.AddUserProfilePreference("profile.default_content_settings.popups", 0);
            options.AddUserProfilePreference("profile.managed_default_content_settings.images", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.media_stream", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.geolocation", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.mixed_script", 1);
            options.AddUserProfilePreference("profile.default_content_setting_values.media_stream_mic", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.media_stream_camera", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.protocol_handlers", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.ppapi_broker", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.automatic_downloads", 1);
            options.AddUserProfilePreference("profile.default_content_setting_values.midi_sysex", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.push_messaging", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.ssl_cert_decisions", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.metro_switch_to_desktop", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.protected_media_identifier", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.app_banner", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.site_engagement", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.durable_storage", 2);

            // Thêm các preference để thay đổi fingerprint
            options.AddUserProfilePreference("intl.accept_languages", fingerprint.Language);
            options.AddUserProfilePreference("profile.default_content_setting_values.notifications", 2);
            options.AddUserProfilePreference("profile.managed_default_content_settings.images", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.media_stream", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.geolocation", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.mixed_script", 1);
            options.AddUserProfilePreference("profile.default_content_setting_values.media_stream_mic", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.media_stream_camera", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.protocol_handlers", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.ppapi_broker", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.automatic_downloads", 1);
            options.AddUserProfilePreference("profile.default_content_setting_values.midi_sysex", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.push_messaging", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.ssl_cert_decisions", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.metro_switch_to_desktop", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.protected_media_identifier", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.app_banner", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.site_engagement", 2);
            options.AddUserProfilePreference("profile.default_content_setting_values.durable_storage", 2);

            Console.WriteLine($"✅ Đã tạo fingerprint mới: {fingerprint.ProfileName} - {fingerprint.UserAgent}");
        }

        public static void ClearChromeData()
        {
            try
            {
                string chromeDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data");

                if (Directory.Exists(chromeDataPath))
                {
                    // Xóa các thư mục cache và data
                    string[] foldersToDelete = { "Default", "Profile 1", "Profile 2", "Profile 3" };

                    foreach (string folder in foldersToDelete)
                    {
                        string fullPath = Path.Combine(chromeDataPath, folder);
                        if (Directory.Exists(fullPath))
                        {
                            try
                            {
                                Directory.Delete(fullPath, true);
                                Console.WriteLine($"🗑️ Đã xóa thư mục Chrome: {folder}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"⚠️ Không thể xóa thư mục {folder}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi xóa dữ liệu Chrome: {ex.Message}");
            }
        }

        // Hiển thị danh sách tất cả profile có sẵn
        public static void ShowAvailableProfiles()
        {
            Console.WriteLine("📋 Danh sách các profile fingerprint có sẵn:");
            Console.WriteLine("==========================================");

            var profiles = GetAllProfiles();
            for (int i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                Console.WriteLine($"{i + 1}. {profile.ProfileName}");
                Console.WriteLine($"   📱 Platform: {profile.Platform}");
                Console.WriteLine($"   🌍 Language: {profile.Language}");
                Console.WriteLine($"   🖥️ Resolution: {profile.ScreenResolution}");
                Console.WriteLine($"   🕐 Timezone: {profile.Timezone}");
                Console.WriteLine($"   💾 Memory: {profile.DeviceMemory}GB");
                Console.WriteLine($"   🔧 CPU Cores: {profile.HardwareConcurrency}");
                Console.WriteLine($"   🎮 GPU: {profile.WebGLRenderer}");
                Console.WriteLine();
            }

            Console.WriteLine("🎲 Random Generated - Tạo ngẫu nhiên hoàn toàn");
            Console.WriteLine("==========================================");
        }
    }
}