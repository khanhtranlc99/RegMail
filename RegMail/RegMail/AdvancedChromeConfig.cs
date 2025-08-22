using OpenQA.Selenium.Chrome;
using System;
using System.IO;
using System.Linq;
using System.Management;

namespace RegMail
{
    public static class AdvancedChromeConfig
    {
        // Profile cố định cho từng tester
        private static readonly string ProfileName = "qa1"; // Có thể thay đổi thành qa2, qa3, etc.
        private static readonly string StableProfilePath = Path.Combine(Environment.CurrentDirectory, "chrome_profiles", ProfileName);
        
        public static void ConfigureAdvancedChromeOptions(ChromeOptions options, int width, int height, int posX, int posY)
        {
            // 1. SỬ DỤNG PROFILE CỐ ĐỊNH CHO TỪNG TESTER
            EnsureStableProfileExists();
            options.AddArgument($"--user-data-dir={StableProfilePath}");
            options.AddArgument("--profile-directory=Default");
            
            // 2. CÁC ARGUMENTS CƠ BẢN CHO AUTOMATION (KHÔNG CỰC ĐOAN)
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
            
            // 3. CÁC ARGUMENTS PERFORMANCE (KHÔNG CỰC ĐOAN)
            options.AddArgument("--disable-background-timer-throttling");
            options.AddArgument("--disable-backgrounding-occluded-windows");
            options.AddArgument("--disable-renderer-backgrounding");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-ipc-flooding-protection");
            options.AddArgument("--disable-hang-monitor");
            options.AddArgument("--disable-prompt-on-repost");
            
            // 4. CÁC ARGUMENTS SECURITY CƠ BẢN (KHÔNG CỰC ĐOAN)
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-software-rasterizer");
            
            // 5. CÁC ARGUMENTS NETWORK VÀ CONNECTIVITY
            options.AddArgument("--remote-debugging-port=0");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-shared-memory");
            
            // 6. CÁC ARGUMENTS LANGUAGE VÀ LOCALE
            options.AddArgument("--lang=en-US");
            options.AddArgument("--accept-lang=en-US,en;q=0.9,vi;q=0.8");
            
            // 7. CÁC ARGUMENTS WINDOW VÀ POSITION
            options.AddArgument("--new-window");
            options.AddArgument("--window-size=" + width + "," + height);
            options.AddArgument("--window-position=" + posX + "," + posY);
            
            // 8. KHÔNG OVERRIDE USER-AGENT - ĐỂ CHROME TỰ ĐỘNG SỬ DỤNG UA MẶC ĐỊNH
            
            // 9. CÁC ARGUMENTS PERFORMANCE BỔ SUNG
            options.AddArgument("--memory-pressure-off");
            options.AddArgument("--max_old_space_size=4096");
            
            // 10. CÁC ARGUMENTS EXPERIMENTAL FEATURES
            options.AddArgument("--enable-experimental-web-platform-features");
            options.AddArgument("--enable-features=NetworkService,NetworkServiceLogging");
        }

        // Phương thức đảm bảo profile cố định tồn tại
        private static void EnsureStableProfileExists()
        {
            try
            {
                if (!Directory.Exists(StableProfilePath))
                {
                    Directory.CreateDirectory(StableProfilePath);
                    Console.WriteLine($"📁 Đã tạo profile cố định: {StableProfilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi tạo profile cố định: {ex.Message}");
            }
        }

        // Phương thức dọn dẹp profile cố định (chỉ khi cần thiết)
        public static void CleanupStableProfile()
        {
            try
            {
                if (Directory.Exists(StableProfilePath))
                {
                    // Chỉ xóa các file cache và temporary, giữ lại cookies và settings
                    var cacheDir = Path.Combine(StableProfilePath, "Default", "Cache");
                    var codeCacheDir = Path.Combine(StableProfilePath, "Default", "Code Cache");
                    var serviceWorkerDir = Path.Combine(StableProfilePath, "Default", "Service Worker");
                    
                    if (Directory.Exists(cacheDir))
                    {
                        Directory.Delete(cacheDir, true);
                        Console.WriteLine("🧹 Đã dọn dẹp cache");
                    }
                    
                    if (Directory.Exists(codeCacheDir))
                    {
                        Directory.Delete(codeCacheDir, true);
                        Console.WriteLine("🧹 Đã dọn dẹp code cache");
                    }
                    
                    if (Directory.Exists(serviceWorkerDir))
                    {
                        Directory.Delete(serviceWorkerDir, true);
                        Console.WriteLine("🧹 Đã dọn dẹp service worker cache");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi dọn dẹp profile: {ex.Message}");
            }
        }

        // Phương thức reset hoàn toàn profile cố định (chỉ khi cần thiết)
        public static void ResetStableProfile()
        {
            try
            {
                if (Directory.Exists(StableProfilePath))
                {
                    Directory.Delete(StableProfilePath, true);
                    Console.WriteLine("🔄 Đã reset hoàn toàn profile cố định");
                }
                EnsureStableProfileExists();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi reset profile: {ex.Message}");
            }
        }

        // Phương thức backup profile cố định
        public static void BackupStableProfile()
        {
            try
            {
                if (Directory.Exists(StableProfilePath))
                {
                    string backupPath = Path.Combine(Environment.CurrentDirectory, $"chrome_profile_backup_{ProfileName}_{DateTime.Now:yyyyMMdd_HHmmss}");
                    Directory.CreateDirectory(backupPath);
                    
                    // Copy tất cả files từ profile gốc sang backup
                    CopyDirectory(StableProfilePath, backupPath);
                    Console.WriteLine($"💾 Đã backup profile: {backupPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi backup profile: {ex.Message}");
            }
        }

        // Phương thức helper để copy directory
        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }

        // Phương thức kill chỉ các tiến trình Chrome liên quan đến profile cụ thể
        public static void KillChromeProcessesForProfile()
        {
            try
            {
                var chromeProcesses = System.Diagnostics.Process.GetProcessesByName("chrome");
                var chromedriverProcesses = System.Diagnostics.Process.GetProcessesByName("chromedriver");
                
                foreach (var process in chromeProcesses)
                {
                    try
                    {
                        // Kiểm tra command line có chứa user-data-dir của profile cụ thể không
                        string commandLine = GetProcessCommandLine(process.Id);
                        if (!string.IsNullOrEmpty(commandLine) && commandLine.Contains($"--user-data-dir={StableProfilePath}"))
                        {
                            process.Kill();
                            Console.WriteLine($"🔪 Đã kill Chrome process cho profile {ProfileName}: {process.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Không thể kiểm tra/kill Chrome process {process.Id}: {ex.Message}");
                    }
                }
                
                foreach (var process in chromedriverProcesses)
                {
                    try
                    {
                        // Kiểm tra command line có chứa user-data-dir của profile cụ thể không
                        string commandLine = GetProcessCommandLine(process.Id);
                        if (!string.IsNullOrEmpty(commandLine) && commandLine.Contains($"--user-data-dir={StableProfilePath}"))
                        {
                            process.Kill();
                            Console.WriteLine($"🔪 Đã kill ChromeDriver process cho profile {ProfileName}: {process.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Không thể kiểm tra/kill ChromeDriver process {process.Id}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi kill Chrome processes cho profile: {ex.Message}");
            }
        }

        // Phương thức helper để lấy command line của process
        private static string GetProcessCommandLine(int processId)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return obj["CommandLine"]?.ToString() ?? "";
                    }
                }
            }
            catch
            {
                // Nếu không thể lấy command line, trả về empty string
            }
            return "";
        }

        // Phương thức lấy thông tin profile cố định
        public static string GetStableProfileInfo()
        {
            try
            {
                if (Directory.Exists(StableProfilePath))
                {
                    var dirInfo = new DirectoryInfo(StableProfilePath);
                    return $"Profile cố định: {StableProfilePath}\n" +
                           $"Tên profile: {ProfileName}\n" +
                           $"Ngày tạo: {dirInfo.CreationTime}\n" +
                           $"Kích thước: {GetDirectorySize(StableProfilePath):N0} bytes";
                }
                else
                {
                    return $"Profile cố định {ProfileName} chưa được tạo";
                }
            }
            catch (Exception ex)
            {
                return $"Lỗi khi lấy thông tin profile: {ex.Message}";
            }
        }

        // Phương thức helper để tính kích thước directory
        private static long GetDirectorySize(string path)
        {
            try
            {
                var dir = new DirectoryInfo(path);
                long size = 0;
                
                foreach (var file in dir.GetFiles("*", SearchOption.AllDirectories))
                {
                    size += file.Length;
                }
                
                return size;
            }
            catch
            {
                return 0;
            }
        }

        // Phương thức để thay đổi profile name (cho tester khác)
        public static void SetProfileName(string newProfileName)
        {
            // Lưu ý: Cần restart ứng dụng để áp dụng profile mới
            Console.WriteLine($"⚠️ Cần restart ứng dụng để chuyển sang profile: {newProfileName}");
        }

        // Phương thức tương thích ngược - trả về profile cố định thay vì tạo profile mới
        public static string CreateUniqueUserDataDirectory()
        {
            // Đảm bảo profile cố định tồn tại
            EnsureStableProfileExists();
            
            // Trả về đường dẫn profile cố định thay vì tạo profile mới
            Console.WriteLine($"📁 Sử dụng profile cố định: {StableProfilePath}");
            return StableProfilePath;
        }
    }
}
