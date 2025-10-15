using OpenQA.Selenium.Chrome;
using System;

namespace RegMail
{
    public static class ChromeOptionsManager
    {
        public static ChromeOptions CreateMinimalOptions(string userDataDir, ProxyManager.ProxySpec proxy = null)
        {
            var options = new ChromeOptions();

            // Sử dụng profile riêng (không dùng Guest/Incognito) - tái sử dụng giữa các lần chạy
            options.AddArgument($"--user-data-dir={userDataDir}");
            options.AddArgument("--profile-directory=Default");
            
            // Đảm bảo bật JavaScript và cookies bình thường
            options.AddUserProfilePreference("profile.default_content_setting_values.javascript", 1); // Bật JS
            options.AddUserProfilePreference("profile.default_content_setting_values.cookies", 1); // Bật cookies
            
            // Cấu hình window
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--window-position=0,0");

            // Chỉ thêm cờ Linux container nếu cần
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-dev-shm-usage");
            }
            
            // ✅ CẤU HÌNH PROXY NẾU CÓ
            if (proxy != null)
            {
                ConfigureProxyForOptions(options, proxy);
            }
            

            return options;
        }

        public static ChromeOptions CreateAdvancedOptions(string userDataDir, ProxyManager.ProxySpec proxy = null)
        {
            // Sử dụng cùng cấu hình cơ bản
            var options = CreateMinimalOptions(userDataDir, proxy);
            
            // ✅ CẤU HÌNH PROXY NẾU CÓ (đã được gọi trong CreateMinimalOptions)
            
            return options;
        }
        
        // ✅ THÊM METHOD CẤU HÌNH PROXY SỬ DỤNG ProxyManager
        private static void ConfigureProxyForOptions(ChromeOptions options, ProxyManager.ProxySpec proxy)
        {
            if (proxy == null) 
            {
                Console.WriteLine("🔍 DEBUG: Proxy null, bỏ qua cấu hình");
                return;
            }
            
            try
            {
                Console.WriteLine($"🔧 Đang cấu hình proxy cho Chrome: {proxy}");
                Console.WriteLine($"🔍 DEBUG: Proxy scheme: {proxy.Scheme}");
                Console.WriteLine($"🔍 DEBUG: Proxy host:port: {proxy.Host}:{proxy.Port}");
                Console.WriteLine($"🔍 DEBUG: Proxy có auth: {proxy.HasAuth}");
                
                // Sử dụng ProxyManager.ApplyToChrome để cấu hình proxy
                ProxyManager.ApplyToChrome(options, proxy);
                
                // Thêm debug logging cho proxy authentication
                if (proxy.HasAuth)
                {
                    Console.WriteLine($"🔐 Proxy authentication được cấu hình:");
                    Console.WriteLine($"   👤 Username: {proxy.Username}");
                    Console.WriteLine($"   🔑 Password: {new string('*', proxy.Password.Length)}");
                    Console.WriteLine($"   🌐 Proxy URL: {proxy.Scheme}://{proxy.Host}:{proxy.Port}");
                }
                
                Console.WriteLine($"✅ Đã cấu hình proxy thành công: {proxy}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi cấu hình proxy: {ex.Message}");
                Console.WriteLine($"📋 Stack trace: {ex.StackTrace}");
            }
        }
    }
}
