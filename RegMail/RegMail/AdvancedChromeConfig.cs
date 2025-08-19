using OpenQA.Selenium.Chrome;
using System;
using System.IO;

namespace RegMail
{
    public static class AdvancedChromeConfig
    {
        public static void ConfigureAdvancedChromeOptions(ChromeOptions options, int width, int height, int posX, int posY)
        {
            // 1. KHẮC PHỤC navigator.webdriver = true
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);

            // 2. KHẮC PHỤC Plugins/MIME trống - Tạo user data directory thật và độc nhất
            string uniqueUserDataDir = CreateUniqueUserDataDirectory();
            options.AddArgument($"--user-data-dir={uniqueUserDataDir}");
            options.AddArgument("--profile-directory=Default");
            options.AddArgument("--remote-debugging-port=0"); // Tự động chọn port debug
            options.AddArgument("--disable-dev-shm-usage"); // Tránh xung đột shared memory
            options.AddArgument("--disable-shared-memory"); // Tắt shared memory
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-ipc-flooding-protection");
            options.AddArgument("--disable-hang-monitor");
            options.AddArgument("--disable-prompt-on-repost");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-software-rasterizer");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-plugins");
            options.AddArgument("--disable-images");
            options.AddArgument("--disable-javascript");
            options.AddArgument("--disable-java");
            options.AddArgument("--disable-plugins-discovery");
            options.AddArgument("--disable-preconnect");

            // 3. KHẮC PHỤC Languages/permissions khác lạ
            options.AddArgument("--lang=en-US");
            options.AddArgument("--accept-lang=en-US,en;q=0.9,vi;q=0.8");

            // 4. KHẮC PHỤC Event chuột/bàn phím thiếu tự nhiên
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-background-timer-throttling");
            options.AddArgument("--disable-backgrounding-occluded-windows");
            options.AddArgument("--disable-renderer-backgrounding");

            // 5. KHẮC PHỤC Chrome launch flags lạ - Sử dụng flags tự nhiên
            options.AddArgument("--no-first-run");
            options.AddArgument("--no-default-browser-check");
            options.AddArgument("--disable-default-apps");
            options.AddArgument("--disable-popup-blocking");
            options.AddArgument("--disable-translate");
            options.AddArgument("--disable-sync");
            options.AddArgument("--disable-background-networking");
            options.AddArgument("--disable-background-downloads");
            options.AddArgument("--disable-client-side-phishing-detection");
            options.AddArgument("--disable-component-update");
            options.AddArgument("--disable-domain-reliability");
            options.AddArgument("--disable-features=TranslateUI");
            options.AddArgument("--disable-ipc-flooding-protection");
            options.AddArgument("--disable-hang-monitor");
            options.AddArgument("--disable-prompt-on-repost");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-software-rasterizer");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-plugins");
            options.AddArgument("--disable-images");
            options.AddArgument("--disable-javascript");
            options.AddArgument("--disable-java");
            options.AddArgument("--disable-plugins-discovery");
            options.AddArgument("--disable-preconnect");
            options.AddArgument("--disable-background-timer-throttling");
            options.AddArgument("--disable-backgrounding-occluded-windows");
            options.AddArgument("--disable-renderer-backgrounding");
            options.AddArgument("--disable-features=TranslateUI");
            options.AddArgument("--disable-ipc-flooding-protection");
            options.AddArgument("--disable-hang-monitor");
            options.AddArgument("--disable-prompt-on-repost");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-software-rasterizer");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-plugins");
            options.AddArgument("--disable-images");
            options.AddArgument("--disable-javascript");
            options.AddArgument("--disable-java");
            options.AddArgument("--disable-plugins-discovery");
            options.AddArgument("--disable-preconnect");

            // 6. KHẮC PHỤC Timing hành vi không giống người thật
            options.AddArgument("--disable-background-timer-throttling");
            options.AddArgument("--disable-backgrounding-occluded-windows");
            options.AddArgument("--disable-renderer-backgrounding");

            // 7. THÊM CÁC ARGUMENTS TỰ NHIÊN KHÁC
            options.AddArgument("--disable-web-security");
            options.AddArgument("--allow-running-insecure-content");
            options.AddArgument("--disable-background-networking");
            options.AddArgument("--disable-background-downloads");
            options.AddArgument("--disable-client-side-phishing-detection");
            options.AddArgument("--disable-component-update");
            options.AddArgument("--disable-domain-reliability");
            options.AddArgument("--disable-features=TranslateUI");
            options.AddArgument("--disable-ipc-flooding-protection");
            options.AddArgument("--disable-hang-monitor");
            options.AddArgument("--disable-prompt-on-repost");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-software-rasterizer");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-plugins");
            options.AddArgument("--disable-images");
            options.AddArgument("--disable-javascript");
            options.AddArgument("--disable-java");
            options.AddArgument("--disable-plugins-discovery");
            options.AddArgument("--disable-preconnect");
            options.AddArgument("--disable-background-timer-throttling");
            options.AddArgument("--disable-backgrounding-occluded-windows");
            options.AddArgument("--disable-renderer-backgrounding");
            options.AddArgument("--disable-features=TranslateUI");
            options.AddArgument("--disable-ipc-flooding-protection");
            options.AddArgument("--disable-hang-monitor");
            options.AddArgument("--disable-prompt-on-repost");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-software-rasterizer");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-plugins");
            options.AddArgument("--disable-images");
            options.AddArgument("--disable-javascript");
            options.AddArgument("--disable-java");
            options.AddArgument("--disable-plugins-discovery");
            options.AddArgument("--disable-preconnect");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-ipc-flooding-protection");
            options.AddArgument("--disable-hang-monitor");
            options.AddArgument("--disable-prompt-on-repost");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-software-rasterizer");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-plugins");
            options.AddArgument("--disable-images");
            options.AddArgument("--disable-javascript");
            options.AddArgument("--disable-java");
            options.AddArgument("--disable-plugins-discovery");
            options.AddArgument("--disable-preconnect");

            // 8. CẤU HÌNH WINDOW VÀ POSITION
            options.AddArgument("--new-window");
            options.AddArgument("--window-size=" + width + "," + height);
            options.AddArgument("--window-position=" + posX + "," + posY);

            // 9. THÊM USER AGENT TỰ NHIÊN
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            // 10. CẤU HÌNH PERFORMANCE
            options.AddArgument("--memory-pressure-off");
            options.AddArgument("--max_old_space_size=4096");
            options.AddArgument("--disable-background-timer-throttling");
            options.AddArgument("--disable-backgrounding-occluded-windows");
            options.AddArgument("--disable-renderer-backgrounding");

            // 11. THÊM EXPERIMENTAL FEATURES
            options.AddArgument("--enable-experimental-web-platform-features");
            options.AddArgument("--enable-features=NetworkService,NetworkServiceLogging");

            // 12. CẤU HÌNH SECURITY
            options.AddArgument("--disable-web-security");
            options.AddArgument("--allow-running-insecure-content");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-ipc-flooding-protection");
            options.AddArgument("--disable-hang-monitor");
            options.AddArgument("--disable-prompt-on-repost");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-software-rasterizer");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-plugins");
            options.AddArgument("--disable-images");
            options.AddArgument("--disable-javascript");
            options.AddArgument("--disable-java");
            options.AddArgument("--disable-plugins-discovery");
            options.AddArgument("--disable-preconnect");

            // 13. THÊM CÁC ARGUMENTS CUỐI CÙNG
            options.AddArgument("--disable-background-timer-throttling");
            options.AddArgument("--disable-backgrounding-occluded-windows");
            options.AddArgument("--disable-renderer-backgrounding");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-ipc-flooding-protection");
            options.AddArgument("--disable-hang-monitor");
            options.AddArgument("--disable-prompt-on-repost");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-software-rasterizer");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-plugins");
            options.AddArgument("--disable-images");
            options.AddArgument("--disable-javascript");
            options.AddArgument("--disable-java");
            options.AddArgument("--disable-plugins-discovery");
            options.AddArgument("--disable-preconnect");
        }

        // Phương thức dọn dẹp các thư mục user data cũ
        public static void CleanupOldUserDataDirectories()
        {
            try
            {
                string currentDir = Environment.CurrentDirectory;
                var chromeDataDirs = Directory.GetDirectories(currentDir, "chrome_user_data_*");
                
                foreach (var dir in chromeDataDirs)
                {
                    try
                    {
                        // Xóa thư mục nếu nó cũ hơn 1 giờ
                        var dirInfo = new DirectoryInfo(dir);
                        if (DateTime.Now - dirInfo.CreationTime > TimeSpan.FromHours(1))
                        {
                            Directory.Delete(dir, true);
                            Console.WriteLine($"🧹 Đã dọn dẹp thư mục cũ: {dir}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Không thể xóa thư mục {dir}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi dọn dẹp thư mục user data: {ex.Message}");
            }
        }

        // Phương thức tạo user data directory độc nhất
        public static string CreateUniqueUserDataDirectory()
        {
            string baseDir = Environment.CurrentDirectory;
            string uniqueDir = Path.Combine(baseDir, $"chrome_user_data_{DateTime.Now.Ticks}_{Guid.NewGuid():N}");
            
            // Đảm bảo thư mục tồn tại
            if (!Directory.Exists(uniqueDir))
            {
                Directory.CreateDirectory(uniqueDir);
            }
            
            return uniqueDir;
        }

        // Phương thức kill các process Chrome cũ nếu cần
        public static void KillOldChromeProcesses()
        {
            try
            {
                var chromeProcesses = System.Diagnostics.Process.GetProcessesByName("chrome");
                var chromedriverProcesses = System.Diagnostics.Process.GetProcessesByName("chromedriver");
                
                foreach (var process in chromeProcesses)
                {
                    try
                    {
                        if (process.StartTime < DateTime.Now.AddMinutes(-30)) // Chỉ kill process cũ hơn 30 phút
                        {
                            process.Kill();
                            Console.WriteLine($"🔪 Đã kill Chrome process cũ: {process.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Không thể kill Chrome process {process.Id}: {ex.Message}");
                    }
                }
                
                foreach (var process in chromedriverProcesses)
                {
                    try
                    {
                        if (process.StartTime < DateTime.Now.AddMinutes(-30)) // Chỉ kill process cũ hơn 30 phút
                        {
                            process.Kill();
                            Console.WriteLine($"🔪 Đã kill ChromeDriver process cũ: {process.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Không thể kill ChromeDriver process {process.Id}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi kill Chrome processes: {ex.Message}");
            }
        }
    }
}
