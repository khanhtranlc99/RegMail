using OpenQA.Selenium.Chrome;
using System;

namespace RegMail
{
    public static class ChromeOptionsManager
    {
        public static ChromeOptions CreateMinimalOptions(string userDataDir)
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

            // Cấu hình User-Agent phù hợp với nền tảng
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            }
            else if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                options.AddArgument("--user-agent=Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            }
            else if (Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                options.AddArgument("--user-agent=Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            }

            // Cấu hình headless mode nếu cần
            if (ConfigManager.Chrome_Headless_Mode)
            {
                options.AddArgument("--headless");
            }
            else
            {
                options.AddArgument("--start-maximized");
            }

            return options;
        }

        public static ChromeOptions CreateAdvancedOptions(string userDataDir)
        {
            // Sử dụng cùng cấu hình cơ bản
            return CreateMinimalOptions(userDataDir);
        }
    }
}
