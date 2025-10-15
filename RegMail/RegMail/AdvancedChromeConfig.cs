using OpenQA.Selenium.Chrome;
using System;
using System.IO;
using System.Linq;
using System.Management;

namespace RegMail
{
    public static class AdvancedChromeConfig
    {
        // Profile động - có thể thay đổi qua SetCurrentProfile()
        private static string _currentProfileName = "qa1"; // Mặc định qa1
        private static readonly string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        
        // Danh sách profiles khả dụng
        private static readonly string[] AvailableProfiles = { "qa1", "qa2", "qa3", "qa4", "qa5" };
        
        // Counter để rotation tự động
        private static int _currentProfileIndex = 0;
        
        // Getter cho ProfilePath động
        private static string StableProfilePath => Path.Combine(baseDir, "RegMail", "chrome_profiles", _currentProfileName);
        
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
            options.AddArgument("--disable-features=TranslateUI");
            
            // 3. CÁC ARGUMENTS PERFORMANCE (KHÔNG CỰC ĐOAN)
            options.AddArgument("--disable-dev-shm-usage");
            
            // 4. CÁC ARGUMENTS SECURITY CƠ BẢN (KHÔNG CỰC ĐOAN)
            options.AddArgument("--no-sandbox");
            
            // 5. CÁC ARGUMENTS NETWORK VÀ CONNECTIVITY
            options.AddArgument("--remote-debugging-port=0");
            
            // 6. CÁC ARGUMENTS LANGUAGE VÀ LOCALE
            options.AddArgument("--lang=en-US");
            options.AddArgument("--accept-lang=en-US,en;q=0.9,vi;q=0.8");
            
            // 7. CÁC ARGUMENTS WINDOW VÀ POSITION
            options.AddArgument("--new-window");
            options.AddArgument("--window-size=" + width + "," + height);
            options.AddArgument("--window-position=" + posX + "," + posY);
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
                    string backupPath = Path.Combine(Environment.CurrentDirectory, $"chrome_profile_backup_{_currentProfileName}_{DateTime.Now:yyyyMMdd_HHmmss}");
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
                            Console.WriteLine($"🔪 Đã kill Chrome process cho profile {_currentProfileName}: {process.Id}");
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
                            Console.WriteLine($"🔪 Đã kill ChromeDriver process cho profile {_currentProfileName}: {process.Id}");
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
                           $"Tên profile: {_currentProfileName}\n" +
                           $"Ngày tạo: {dirInfo.CreationTime}\n" +
                           $"Kích thước: {GetDirectorySize(StableProfilePath):N0} bytes";
                }
                else
                {
                    return $"Profile cố định {_currentProfileName} chưa được tạo";
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

        // =====================================================
        // QUẢN LÝ NHIỀU PROFILES - HỖ TRỢ TẠO NHIỀU TÀI KHOẢN
        // =====================================================
        
        /// <summary>
        /// Lấy tên profile hiện tại
        /// </summary>
        public static string GetCurrentProfileName()
        {
            return _currentProfileName;
        }
        
        /// <summary>
        /// Đặt profile cụ thể (qa1, qa2, qa3, qa4, qa5)
        /// </summary>
        public static void SetCurrentProfile(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
            {
                Console.WriteLine("⚠️ Tên profile không hợp lệ!");
                return;
            }
            
            _currentProfileName = profileName;
            Console.WriteLine($"✅ Đã chuyển sang profile: {profileName}");
            Console.WriteLine($"📁 Đường dẫn: {StableProfilePath}");
        }
        
        /// <summary>
        /// Rotation tự động sang profile tiếp theo (qa1 → qa2 → qa3 → qa4 → qa5 → qa1)
        /// </summary>
        public static string RotateToNextProfile()
        {
            _currentProfileIndex = (_currentProfileIndex + 1) % AvailableProfiles.Length;
            _currentProfileName = AvailableProfiles[_currentProfileIndex];
            
            Console.WriteLine($"🔄 Rotation sang profile: {_currentProfileName} (Index: {_currentProfileIndex + 1}/{AvailableProfiles.Length})");
            return _currentProfileName;
        }
        
        /// <summary>
        /// Lấy danh sách tất cả profiles khả dụng
        /// </summary>
        public static string[] GetAllAvailableProfiles()
        {
            return AvailableProfiles;
        }
        
        /// <summary>
        /// Hiển thị thông tin tất cả profiles
        /// </summary>
        public static void ShowAllProfilesInfo()
        {
            Console.WriteLine("\n📋 DANH SÁCH TẤT CẢ PROFILES:");
            Console.WriteLine("═══════════════════════════════════════════");
            
            for (int i = 0; i < AvailableProfiles.Length; i++)
            {
                string profileName = AvailableProfiles[i];
                string profilePath = Path.Combine(baseDir, "RegMail", "chrome_profiles", profileName);
                bool exists = Directory.Exists(profilePath);
                string status = exists ? "✅ Đã tạo" : "⚪ Chưa tạo";
                string current = profileName == _currentProfileName ? " 👈 ĐANG DÙNG" : "";
                
                Console.WriteLine($"{i + 1}. {profileName} - {status}{current}");
                
                if (exists)
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(profilePath);
                        long sizeInBytes = GetDirectorySize(profilePath);
                        double sizeInMB = sizeInBytes / (1024.0 * 1024.0);
                        Console.WriteLine($"   📁 Đường dẫn: {profilePath}");
                        Console.WriteLine($"   📅 Ngày tạo: {dirInfo.CreationTime:yyyy-MM-dd HH:mm}");
                        Console.WriteLine($"   💾 Kích thước: {sizeInMB:F2} MB");
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }
                Console.WriteLine();
            }
            
            Console.WriteLine("═══════════════════════════════════════════");
        }
        
        /// <summary>
        /// Khuyến nghị số lượng tài khoản tối đa/profile/ngày
        /// </summary>
        public static int GetRecommendedAccountsPerProfilePerDay()
        {
            return 3; // Khuyến nghị: tối đa 3 tài khoản/profile/ngày
        }
        
        /// <summary>
        /// Tính toán số profile cần thiết cho số lượng Gmail mong muốn
        /// </summary>
        public static void ShowScalingRecommendation(int desiredGmailCount)
        {
            int accountsPerProfile = GetRecommendedAccountsPerProfilePerDay();
            int profilesNeeded = (int)Math.Ceiling((double)desiredGmailCount / accountsPerProfile);
            int minutesSpacing = 30; // Spacing giữa mỗi lần tạo
            int totalMinutes = desiredGmailCount * minutesSpacing;
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            
            Console.WriteLine($"\n📊 KHUYẾN NGHỊ ĐỂ TẠO {desiredGmailCount} GMAIL/NGÀY:");
            Console.WriteLine("═══════════════════════════════════════════");
            Console.WriteLine($"✅ Số profile cần: {profilesNeeded} profile(s)");
            Console.WriteLine($"✅ Mỗi profile: {accountsPerProfile} tài khoản");
            Console.WriteLine($"✅ Spacing khuyến nghị: {minutesSpacing} phút/tài khoản");
            Console.WriteLine($"✅ Thời gian ước tính: ~{hours}h{minutes:D2}m");
            Console.WriteLine($"✅ Cần {profilesNeeded} proxy khác nhau (mỗi profile 1 proxy)");
            Console.WriteLine("\n📋 CHIẾN LƯỢC:");
            
            for (int i = 0; i < profilesNeeded && i < AvailableProfiles.Length; i++)
            {
                int accountsForThisProfile = Math.Min(accountsPerProfile, desiredGmailCount - (i * accountsPerProfile));
                Console.WriteLine($"   Profile {AvailableProfiles[i]} + Proxy_{i + 1} → {accountsForThisProfile} tài khoản");
            }
            
            if (profilesNeeded > AvailableProfiles.Length)
            {
                Console.WriteLine($"\n⚠️ CẢNH BÁO: Cần {profilesNeeded} profiles nhưng chỉ có {AvailableProfiles.Length} profiles!");
                Console.WriteLine($"   Giảm xuống {AvailableProfiles.Length * accountsPerProfile} Gmail/ngày hoặc thêm profiles!");
            }
            
            Console.WriteLine("═══════════════════════════════════════════\n");
        }
        
        // Phương thức để thay đổi profile name (cho tester khác) - DEPRECATED
        [Obsolete("Sử dụng SetCurrentProfile() thay thế")]
        public static void SetProfileName(string newProfileName)
        {
            SetCurrentProfile(newProfileName);
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
