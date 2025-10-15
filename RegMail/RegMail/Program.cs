using ClosedXML.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using OtpNet;
using RegMail;
using SeleniumExtras.WaitHelpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public class OtpResponse
{
    public string message { get; set; }
    public OtpData data { get; set; }
}

public class OtpData
{
    public string transId { get; set; }
    public string phoneNumber { get; set; }
    public string country { get; set; }
    public string session_start { get; set; }
    public string session_end { get; set; }
    public double cost { get; set; }
}

class Program
{
    private static ProxyManager _proxyManager;
    private static ProxyManager.ProxySpec _currentProxy; // Proxy hiện tại được sử dụng
    private static string phoneNumber2FA;
    private static string currentAuthenticatorKey; // Lưu key cho Gmail hiện tại
    private static string currentGmail; // Lưu Gmail hiện tại
    private static string currentPassword; // Lưu password hiện tại
    private static Random _random = new Random(); // Random cho các hành động mô phỏng
    private static bool enableGmailSync = false; // Cờ để bật/tắt đồng bộ Gmail
    private static string path = "C:\\Users\\lqanh\\OneDrive\\ドキュメント\\Reg\\RegMail\\RegMail\\proxies.txt"; // Đường dẫn file proxy
    
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        // Khởi tạo Proxy Manager và tải proxy từ file
        Console.WriteLine("🔧 Khởi tạo Proxy Manager...");
        _proxyManager = new ProxyManager();
        
        // ✅ TẢI PROXY TỪ FILE SỬ DỤNG ProxyManager
        Console.WriteLine("🔧 Tải proxy từ file...");
        try
        {
            var proxies = ProxyManager.LoadProxiesFromFile(path);
            Console.WriteLine($"✅ Đã tải {proxies.Count} proxy từ file");
            
            if (proxies.Count > 0)
            {
                // Chọn proxy ngẫu nhiên
                _currentProxy = ProxyManager.PickRandom(proxies);
                Console.WriteLine($"✅ Đã chọn proxy: {_currentProxy}");
                
                // Test proxy trước khi sử dụng
                Console.WriteLine("🔍 Đang test proxy...");
                try
                {
                    bool proxyWorking = await ProxyManager.TestHttpProxyAsync(_currentProxy, 10);
                    if (!proxyWorking)
                    {
                        Console.WriteLine("⚠️ Proxy không hoạt động, thử proxy khác...");
                        // Thử proxy khác
                        for (int i = 0; i < Math.Min(5, proxies.Count); i++)
                        {
                            _currentProxy = ProxyManager.PickRandom(proxies);
                            proxyWorking = await ProxyManager.TestHttpProxyAsync(_currentProxy, 5);
                            if (proxyWorking)
                            {
                                Console.WriteLine($"✅ Đã tìm thấy proxy hoạt động: {_currentProxy}");
                                break;
                            }
                        }
                        
                        if (!proxyWorking)
                        {
                            Console.WriteLine("❌ Không tìm thấy proxy nào hoạt động, sẽ chạy không proxy");
                            _currentProxy = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Lỗi khi test proxy: {ex.Message}");
                    Console.WriteLine("⚠️ Sẽ tiếp tục với proxy đã chọn");
                }
            }
            else
            {
                Console.WriteLine("⚠️ Không có proxy nào khả dụng, sẽ chạy không proxy");
                _currentProxy = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi khi tải proxy: {ex.Message}");
            Console.WriteLine("⚠️ Sẽ chạy không proxy");
            _currentProxy = null;
        }

        // Hiển thị menu chế độ hoạt động
        Console.WriteLine("\n🎯 Chọn chế độ hoạt động:");
        Console.WriteLine("1. Tạo tài khoản Gmail mới (manual)");
        Console.WriteLine("2. Đăng nhập với email đã có (sử dụng persistent fingerprint)");
        Console.WriteLine("3. 🎯 TẠO NHIỀU GMAIL với Profile Rotation (TỰ ĐỘNG)");
        Console.WriteLine("4. Test tính nhất quán của persistent fingerprint");
        Console.WriteLine("5. 📊 Xem khuyến nghị scale (tạo nhiều Gmail/ngày)");
        Console.WriteLine("6. 📁 Quản lý Chrome Profiles");
        Console.Write("Lựa chọn của bạn (1-6): ");
        
        string choice = Console.ReadLine();
        
        // Xử lý lựa chọn 5: Xem khuyến nghị scale
        if (choice == "5")
        {
            Console.Write("\n📊 Bạn muốn tạo bao nhiêu Gmail/ngày? ");
            if (int.TryParse(Console.ReadLine(), out int desiredCount) && desiredCount > 0)
            {
                AdvancedChromeConfig.ShowScalingRecommendation(desiredCount);
                AdvancedChromeConfig.ShowAllProfilesInfo();
            }
            else
            {
                Console.WriteLine("❌ Số lượng không hợp lệ!");
            }
            return;
        }
        
        // Xử lý lựa chọn 3: Tạo nhiều Gmail với rotation
        if (choice == "3")
        {
            await CreateMultipleGmailsWithRotation();
            return;
        }
        
        // Chế độ 1: Tạo tài khoản mới
        Console.WriteLine("✅ Chế độ: Tạo tài khoản Gmail mới");

        // Hỏi người dùng có muốn đồng bộ Gmail không
        Console.Write("Bạn có muốn đăng nhập và đồng bộ Gmail sau khi tạo? (y/n): ");
        enableGmailSync = Console.ReadLine()?.ToLower().StartsWith("y") == true;
        
        if (enableGmailSync)
        {
            Console.WriteLine("✅ Sẽ tự động đăng nhập và đồng bộ Gmail sau khi tạo thành công");
        }
        else
        {
            Console.WriteLine("⚠️ Chỉ tạo tài khoản Gmail mà không đăng nhập đồng bộ");
        }

        Console.Write("\nNhập số lượng tab Chrome cần mở (Khuyến nghị: 1-3 tab với hotspot 4G): ");
        if (!int.TryParse(Console.ReadLine(), out int tabCount) || tabCount <= 0)
        {
            Console.WriteLine("Số lượng tab không hợp lệ!");
            return;
        }
        
        if (tabCount > 3)
        {
            Console.WriteLine("⚠️ CẢNH BÁO: Với hotspot 4G, khuyến nghị chỉ 1-3 tab để tránh verification!");
            Console.Write("Bạn có muốn tiếp tục? (y/n): ");
            if (Console.ReadLine()?.ToLower() != "y")
            {
                Console.WriteLine("Đã hủy. Hãy thử lại với ít tab hơn.");
                return;
            }
        }

        int spacing = 10;
        int width = 500;
        int height = 700;
        string signupUrl = ConfigManager.Google_Signup_URL;

        for (int i = 0; i < tabCount; i++)
        {
            int posX = i * (width + spacing);
            int posY = 100;

            // Reset các biến global cho Gmail mới
            phoneNumber2FA = "";
            currentGmail = "";
            currentPassword = "";
            currentAuthenticatorKey = "";
            
            // Biến theo dõi trang hiện tại (1 = trang đầu tiên)
            int currentPage = 1;
            
            
            ChromeOptions options = new ChromeOptions();
            
            // CẤU HÌNH CHROME ANTI-DETECTION NÂNG CAO
            AdvancedChromeConfig.ConfigureAdvancedChromeOptions(options, width, height, posX, posY);

            // Tạo account identifier deterministic cho persistent fingerprint
            // Sử dụng tab index và timestamp để tạo account unique identifier
            string accountIdentifier = $"gmail_tab_{i}_{DateTime.Now:yyyyMMdd_HHmm}";
            
            // Tạo userDataDir duy nhất cho mỗi tab
            string userDataDir = AdvancedChromeConfig.CreateUniqueUserDataDirectory();
            Console.WriteLine($"📁 UserDataDir cho tab {i + 1}: {userDataDir}");
            
            // ✅ DEBUG: Kiểm tra proxy trước khi tạo Chrome options
            if (_currentProxy != null)
            {
                Console.WriteLine($"🔍 DEBUG: Proxy cho tab {i + 1}: {_currentProxy}");
                Console.WriteLine($"🔍 DEBUG: Proxy scheme: {_currentProxy.Scheme}");
                Console.WriteLine($"🔍 DEBUG: Proxy host:port: {_currentProxy.Host}:{_currentProxy.Port}");
                Console.WriteLine($"🔍 DEBUG: Proxy có auth: {_currentProxy.HasAuth}");
            }
            else
            {
                Console.WriteLine($"🔍 DEBUG: Tab {i + 1} sẽ chạy không proxy");
            }
        
            options = ChromeOptionsManager.CreateMinimalOptions(userDataDir, _currentProxy);
            
            // Áp dụng fingerprint
            FingerprintManager.ConfigureChromeOptions(options);
            
            // Khởi tạo ChromeDriver với xử lý lỗi
            IWebDriver driver = null;
            try
            {
                driver = new ChromeDriver(options);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("session not created"))
            {
                Console.WriteLine("⚠️ Lỗi session Chrome, thử lại với cấu hình tối thiểu...");
                // Thử lại với cấu hình tối thiểu
                options = ChromeOptionsManager.CreateMinimalOptions(userDataDir, _currentProxy);
                driver = new ChromeDriver(options);
        }
        
        // Xử lý proxy authentication popup nếu xuất hiện
        HandleProxyAuthPopup(driver);
        
        driver.Navigate().GoToUrl(signupUrl);

        // Thay thế Thread.Sleep cố định bằng SmartDelay
        SmartDelay("navigate", 2000, 5000);


            //Inject JavaScript để thay đổi fingerprint và tránh phát hiện automation
            InjectAntiDetectionScripts(driver);
            
            // Cải thiện network fingerprint
            ImproveNetworkFingerprint(driver);
            
            // Kiểm tra fingerprint consistency
            EnsureFingerprintConsistency(driver);
            
            // Cải thiện device fingerprint
            ImproveDeviceFingerprint(driver);
            
            // Build profile history để tăng trust score
            BuildProfileHistory(driver);
            
            // Thêm browsing behavior để build profile
            AddBrowsingBehavior(driver);
            
            // Thêm xử lý lỗi tổng thể cho quá trình tạo Gmail
            try
            {

            // Thêm trust building behavior trước khi fill form
            AddTrustBuildingBehavior(driver);
            
            string firstName = FillFirstName(driver);
            AddTrustBuildingBehavior(driver);
            
            string lastName = FillLastName(driver);
            AddTrustBuildingBehavior(driver);
            ClickNextButton(driver, currentPage++);
            FillDayAndYearNew(driver);
            FillMonthNew(driver);
            FillGenderNew(driver);
            ClickNextButton(driver, currentPage++);
            ClickNextButton(driver, currentPage++);
            // Thêm delay thông minh và human-like behavior
            SmartDelay("think", 2000, 5000);
            HumanLikeActions(driver); // Thêm human behavior
            
            // Kiểm tra xem có cần click "Create your own Gmail address" hay không
            TryClickCreateOwnGmail(driver);
            SmartDelay("think", 2000, 5000);
            HumanLikeActions(driver); // Thêm human behavior

            string email = FillUsername(driver, firstName, lastName);
            string password = FillPassword(driver);
            
            // Lưu Gmail và password vào biến global
            currentGmail = email;
            currentPassword = password;
            
            
            ClickNextButton(driver, currentPage++);

            await HandleRequestSever(driver, email, password);
            
            // Kiểm tra xem tab có bị đóng không (do không nhận được OTP)
            try
            {
                // Thử truy cập một thuộc tính của driver để kiểm tra xem nó còn hoạt động không
                string currentUrl = driver.Url;
            }
            catch (Exception)
            {
                // Tab đã bị đóng, chuyển sang tab tiếp theo
                Console.WriteLine("🔄 Tab đã bị đóng do không nhận được OTP, chuyển sang tab tiếp theo...");
                continue;
            }
            
            Console.WriteLine($"✅ Tài khoản Gmail: {email}, Password: {password}");

            ClickSkipRecoveryEmailButton(driver);
            ClickReviewNextButton(driver);
                HumanLikeActions(driver);
                ClickPrivacyAgreeButton(driver);
            ClickConfirmPersonalizationButton(driver);
                HumanLikeActions(driver);
                GoToGoogle2FA(driver);
            ClickAddPhoneNumberButton(driver, phoneNumber2FA);
            Fill2FAPhoneAndNext(driver, phoneNumber2FA);
            ClickConfirmPhoneSaveButton(driver);
            ClickDoneButtonAfterPhoneVerify(driver);
            GoToAuthenticatorAppAndSetup(driver);
            ClickCantScanItLink(driver);
            string authKeyWithSpaces = ExtractAuthenticatorKey(driver);
            UpdateAuthenticatorKeyInExcel(currentGmail, authKeyWithSpaces);
            ClickNextButtonAfterAuthenticatorKey(driver, authKeyWithSpaces);
            Thread.Sleep(3000);
            Remove2FAPhoneNumber(driver);
            
            // Đăng nhập và đồng bộ hóa Gmail sau khi tạo thành công (nếu được bật)
            if (enableGmailSync)
            {
                Thread.Sleep(2000);
                LoginAndSyncGmail(driver, currentGmail, currentPassword);
            }
            else
            {
                Console.WriteLine("⚠️ Bỏ qua đăng nhập đồng bộ Gmail theo yêu cầu của người dùng");
            }
            
            // Đóng driver an toàn
            SafeCloseDriver(driver);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi trong quá trình tạo Gmail: {ex.Message}");
                Console.WriteLine($"📋 Stack trace: {ex.StackTrace}");
                
                // Đóng driver an toàn ngay cả khi có lỗi
                SafeCloseDriver(driver);
                
                // Chờ một chút trước khi thử lại
                Thread.Sleep(5000);
                continue;
            }
        }
    }

    // Phương thức đóng driver an toàn và dọn dẹp tài nguyên
    static void SafeCloseDriver(IWebDriver driver)
    {
        try
        {
            if (driver != null)
            {
                driver.Quit();
                driver.Dispose();
                Console.WriteLine("🔒 Đã đóng Chrome driver an toàn");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi đóng driver: {ex.Message}");
        }
    }

    // Hàm đăng nhập và đồng bộ hóa Gmail sau khi tạo thành công
    static void LoginAndSyncGmail(IWebDriver driver, string email, string password)
    {
        try
        {
            Console.WriteLine($"🔐 Bắt đầu đăng nhập và đồng bộ hóa Gmail: {email}");
            
            // Chuyển đến trang Gmail
            driver.Navigate().GoToUrl(ConfigManager.Google_Mail_URL);
            Thread.Sleep(3000);
            
            // Kiểm tra xem đã đăng nhập hay chưa
            try
            {
                // Tìm compose button để xác nhận đã đăng nhập
                var composeButton = driver.FindElement(By.XPath("//div[contains(text(), 'Compose')]"));
                Console.WriteLine("✅ Gmail đã được đăng nhập và đồng bộ thành công!");
                
                // Kích hoạt các dịch vụ Google khác để đồng bộ hoàn toàn
                ActivateGoogleServices(driver);
                return;
            }
            catch (NoSuchElementException)
            {
                // Chưa đăng nhập, tiếp tục quá trình đăng nhập
                Console.WriteLine("🔄 Đang thực hiện đăng nhập...");
            }
            
            // Nếu cần đăng nhập, điền email
            try
            {
                IWebElement emailInput = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                    .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@type='email']")));
                emailInput.Clear();
                HumanType(emailInput, email);
                ClickNextButton(driver);
                Thread.Sleep(2000);
            }
            catch (Exception)
            {
                Console.WriteLine("⚠️ Không tìm thấy ô nhập email hoặc đã đăng nhập rồi");
            }
            
            // Điền password
            try
            {
                IWebElement passwordInput = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                    .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@type='password']")));
                passwordInput.Clear();
                HumanType(passwordInput, password);
                ClickNextButton(driver);
                Thread.Sleep(3000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi nhập password: {ex.Message}");
            }
            
            // Xử lý 2FA nếu cần
            Handle2FALogin(driver);
            
            // Kích hoạt đồng bộ với các dịch vụ Google
            ActivateGoogleServices(driver);
            
            Console.WriteLine("✅ Gmail đã được đăng nhập và đồng bộ hoàn toàn!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi khi đăng nhập Gmail: {ex.Message}");
        }
    }
    
    // Hàm xử lý 2FA khi đăng nhập
    static void Handle2FALogin(IWebDriver driver)
    {
        try
        {
            // Kiểm tra xem có yêu cầu 2FA không
            var authenticatorInput = driver.FindElements(By.XPath("//input[@type='tel' or @aria-label='Enter code']"));
            if (authenticatorInput.Count > 0)
            {
                Console.WriteLine("🔐 Phát hiện yêu cầu 2FA, đang tạo mã OTP...");
                
                if (!string.IsNullOrEmpty(currentAuthenticatorKey))
                {
                    string authKeyWithoutSpaces = currentAuthenticatorKey.Replace(" ", "");
                    string otpCode = GenerateOtpCode(authKeyWithoutSpaces);
                    
                    if (!string.IsNullOrEmpty(otpCode))
                    {
                        authenticatorInput[0].Clear();
                        HumanType(authenticatorInput[0], otpCode);
                        ClickNextButton(driver);
                        Thread.Sleep(3000);
                        Console.WriteLine("✅ Đã nhập mã 2FA thành công");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi xử lý 2FA: {ex.Message}");
        }
    }
    
    // Hàm kích hoạt và đồng bộ các dịch vụ Google
    static void ActivateGoogleServices(IWebDriver driver)
    {
        try
        {
            Console.WriteLine("🔄 Đang kích hoạt đồng bộ hóa với các dịch vụ Google...");
            
            // Kích hoạt Google Drive
            driver.Navigate().GoToUrl(ConfigManager.Google_Drive_URL);
            Thread.Sleep(2000);
            Console.WriteLine("✅ Đã kích hoạt Google Drive");
            
            // Kích hoạt Google Photos
            driver.Navigate().GoToUrl(ConfigManager.Google_Photos_URL);
            Thread.Sleep(2000);
            Console.WriteLine("✅ Đã kích hoạt Google Photos");
            
            // Kích hoạt YouTube
            driver.Navigate().GoToUrl(ConfigManager.Google_YouTube_URL);
            Thread.Sleep(2000);
            Console.WriteLine("✅ Đã kích hoạt YouTube");
            
            // Quay lại Gmail để đảm bảo hoạt động bình thường
            driver.Navigate().GoToUrl(ConfigManager.Google_Mail_URL);
            Thread.Sleep(2000);
            
            // Bật sync trong Chrome (nếu có thể)
            EnableChromeSync(driver);
            
            Console.WriteLine("🎉 Đã hoàn thành việc đồng bộ hóa tất cả dịch vụ Google!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi kích hoạt dịch vụ Google: {ex.Message}");
        }
    }
    
    // Hàm bật Chrome Sync (nếu có thể) - Chỉ thực hiện khi được cấu hình
    static void EnableChromeSync(IWebDriver driver)
    {
        if (!ConfigManager.Chrome_Enable_Sync)
        {
            Console.WriteLine("ℹ️ Chrome Sync đã được tắt trong cấu hình");
            return;
        }
        
        try
        {
            Console.WriteLine("🔄 Đang cố gắng bật Chrome Sync...");
            
            // Truy cập trang settings Chrome và đợi trang load
            driver.Navigate().GoToUrl("chrome://settings/syncSetup");
            
            // Đợi trang Chrome settings load hoàn tất thay vì sleep cứng nhắc
            new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));
            
            // Tìm và click nút "Turn on sync" hoặc "Yes, I'm in"
            try
            {
                var syncButtons = driver.FindElements(By.XPath("//button[contains(text(), 'Turn on') or contains(text(), 'Yes') or contains(text(), 'Enable')]"));
                if (syncButtons.Count > 0)
                {
                    // Sử dụng click tự nhiên thay vì JavaScript click
                    var clickableButton = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                        .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(syncButtons[0]));
                    clickableButton.Click();
                    
                    // Đợi phản hồi sau khi click thay vì sleep cứng
                    RandomDelay(50, 150); // Random delay ngắn để tự nhiên hơn
                    Console.WriteLine("✅ Đã bật Chrome Sync thành công!");
                }
            }
            catch (Exception)
            {
                Console.WriteLine("⚠️ Không thể tự động bật Chrome Sync, có thể đã được bật sẵn");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Không thể truy cập Chrome Sync: {ex.Message}");
        }
    }

    // Hàm nhập từng ký tự một với delay ngẫu nhiên đơn giản (không có backspace hay double type)
    static void HumanType(IWebElement element, string text)
    {
        Random randomDelay = new Random();
        
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            element.SendKeys(c.ToString());
            Thread.Sleep(randomDelay.Next(80, 180));
        }
    }

    // Hàm đợi trang tải hoàn tất thông minh
    static bool WaitForPageLoad(IWebDriver driver, int timeoutSeconds = 15)
    {
        try
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            
            // Đợi document.readyState = "complete"
            wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));
            
            // Đợi không còn loading indicators
            wait.Until(d => d.FindElements(By.XPath("//div[contains(@class, 'loading') or contains(@class, 'spinner') or @aria-label='Loading']")).Count == 0);
            
            return true;
        }
        catch (WebDriverTimeoutException)
        {
            Console.WriteLine("⚠️ Page load timeout - tiếp tục thực hiện");
            return false;
        }
    }

    static string FillFirstName(IWebDriver driver)
    {
        string[] firstNames = { "Acacia", "Adela", "Blanche", "Bridget", "Donna", "Mayya", "Luccy" };
        Random random = new Random();
        string randomFirstName = firstNames[random.Next(firstNames.Length)];

        // Sử dụng click tự nhiên với ElementToBeClickable thay vì JS scroll + click
        var firstNameField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//input[@aria-label='First name']")));
        
        RandomDelay(200, 400);
        firstNameField.Click(); // Click tự nhiên - tạo ra đầy đủ mouse events
        RandomDelay(100, 200);
        
        // Sử dụng HumanTypeAdvanced với các tùy chọn mô phỏng hành vi thật
        HumanTypeAdvanced(firstNameField, randomFirstName, enableBackspace: true, enablePause: true, enableDoubleType: true);

        return randomFirstName;
    }

    static string FillLastName(IWebDriver driver)
    {
        string[] lastNames = { "Emery", "Fergal", "Augustus", "Cadell", "Garrick", "Antony", "Grak" };
        Random random = new Random();
        string randomLastName = lastNames[random.Next(lastNames.Length)];

        IWebElement lastNameField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='Last name (optional)']")));
        // Nhập từng ký tự một với mô phỏng hành vi thật
        lastNameField.Clear();
        HumanTypeAdvanced(lastNameField, randomLastName, enableBackspace: true, enablePause: true, enableDoubleType: true);

        return randomLastName;
    }

    static string FillUsername(IWebDriver driver, string firstName, string lastName)
    {
        int x = 1;
        bool success = false;
        string username = "";
        System.Random rand = new System.Random();
        int idx = rand.Next(1, 150);
        while (!success && x < 100)
        {
            username = firstName.ToLower() + idx.ToString() + lastName.ToLower() + idx;
            
            try
            {
                username = firstName.ToLower() + idx.ToString() + lastName.ToLower() + x;
                Console.WriteLine($"🔍 Đang thử username: {username}");
                
                // Thử tìm trường "Create a Gmail address" trước
                IWebElement usernameField = null;
                try
                {
                    usernameField = new WebDriverWait(driver, TimeSpan.FromSeconds(5))
                        .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='Create a Gmail address']")));
                    Console.WriteLine("✅ Tìm thấy trường 'Create a Gmail address'");
                }
                catch (WebDriverTimeoutException)
                {
                    // Nếu không tìm thấy, thử tìm trường "Username"
                    try
                    {
                        usernameField = new WebDriverWait(driver, TimeSpan.FromSeconds(5))
                            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='Username']")));
                        Console.WriteLine("✅ Tìm thấy trường 'Username'");
                    }
                    catch (WebDriverTimeoutException)
                    {
                        // Thử tìm bằng các selector khác
                        try
                        {
                            usernameField = new WebDriverWait(driver, TimeSpan.FromSeconds(5))
                                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[contains(@aria-label, 'username') or contains(@aria-label, 'Username') or contains(@aria-label, 'Gmail')]")));
                            Console.WriteLine("✅ Tìm thấy trường username bằng selector tổng quát");
                        }
                        catch (WebDriverTimeoutException)
                        {
                            // Thử tìm bằng name hoặc id
                            try
                            {
                                usernameField = driver.FindElement(By.Name("username")) ?? driver.FindElement(By.Id("username"));
                                Console.WriteLine("✅ Tìm thấy trường username bằng name/id");
                            }
                            catch (NoSuchElementException)
                            {
                                // Thử tìm input đầu tiên trong form
                                try
                                {
                                    usernameField = driver.FindElement(By.XPath("//form//input[@type='text' or @type='email']"));
                                    Console.WriteLine("✅ Tìm thấy trường input đầu tiên trong form");
                                }
                                catch (NoSuchElementException)
                                {
                                    throw new Exception("Không tìm thấy trường nhập username nào");
                                }
                            }
                        }
                    }
                }

                // Xóa nội dung cũ và nhập username mới
                usernameField.Clear();
                Thread.Sleep(500);
                
                HumanTypeAdvanced(usernameField, username, enableBackspace: true, enablePause: true, enableDoubleType: true);
                
                // Kiểm tra kết quả cuối cùng
                try
                {
                    string finalValue = usernameField.GetAttribute("value");
                    if (finalValue != username)
                    {
                        usernameField.Clear();
                        Thread.Sleep(300);
                        usernameField.SendKeys(username);
                        Thread.Sleep(200);
                        string correctedValue = usernameField.GetAttribute("value");
                        Console.WriteLine($"🔧 Sau khi sửa - Mong muốn: '{username}', Thực tế: '{correctedValue}'");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Không thể kiểm tra giá trị cuối cùng: {ex.Message}");
                }
                
                Console.WriteLine($"✅ Đã nhập username: {username}");

                ClickNextButton(driver, 2);
                Thread.Sleep(2000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi xử lý username: {ex.Message}");
                throw;
            }

            ClickNextButton(driver, 2);
            Thread.Sleep(2000);

            try
            {
                driver.FindElement(By.XPath("//div[contains(text(), 'That username is taken')]"));
                x++;
            }
            catch (NoSuchElementException)
            {
                success = true;
            }
        }
        return username + "@gmail.com";
    }

    static string FillPassword(IWebDriver driver)
    {
        string password = GenerateRandomPassword(12);

        IWebElement passwordField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='Password']")));
        passwordField.Clear();
        Thread.Sleep(500);
        
        HumanTypeAdvanced(passwordField, password, enableBackspace: true, enablePause: true, enableDoubleType: true);
        
        // Kiểm tra kết quả cuối cùng cho password
        try
        {
            string finalValue = passwordField.GetAttribute("value");
            if (finalValue != password)
            {
                passwordField.Clear();
                Thread.Sleep(300);
                passwordField.SendKeys(password);
                Thread.Sleep(200);
                string correctedValue = passwordField.GetAttribute("value");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Không thể kiểm tra giá trị password cuối cùng: {ex.Message}");
        }

        IWebElement confirmPasswordField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='Confirm']")));
        confirmPasswordField.Clear();
        Thread.Sleep(500);
        
        HumanTypeAdvanced(confirmPasswordField, password, enableBackspace: true, enablePause: true, enableDoubleType: true);
        
        // Kiểm tra kết quả cuối cùng cho confirm password
        try
        {
            string finalValue = confirmPasswordField.GetAttribute("value");
            Console.WriteLine($"🔐 Kết quả cuối cùng confirm password - Mong muốn: '{password}', Thực tế: '{finalValue}'");
            if (finalValue != password)
            {
                Console.WriteLine($"⚠️ CÓ SỰ KHÁC BIỆT CONFIRM PASSWORD! Đang sửa lại...");
                confirmPasswordField.Clear();
                Thread.Sleep(300);
                confirmPasswordField.SendKeys(password);
                Thread.Sleep(200);
                string correctedValue = confirmPasswordField.GetAttribute("value");
                Console.WriteLine($"🔧 Sau khi sửa confirm password - Mong muốn: '{password}', Thực tế: '{correctedValue}'");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Không thể kiểm tra giá trị confirm password cuối cùng: {ex.Message}");
        }

        return password;
    }

    static string GenerateRandomPassword(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        Random random = new Random();
        char[] password = new char[length];

        for (int i = 0; i < length; i++)
        {
            password[i] = chars[random.Next(chars.Length)];
        }

        return new string(password);
    }

    static void ClickNextButton(IWebDriver driver, int currentPage = 1)
    {
        try
        {
            // Lưu URL hiện tại để kiểm tra xem có chuyển trang không
            string currentUrl = driver.Url;
            
            
            // Tìm nút Next
            // Sử dụng click tự nhiên - WebDriverWait đã đảm bảo element clickable và visible
            var nextButton = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//span[contains(text(), 'Next')]")));
            
            RandomDelay(100, 200); // Random delay ngắn thay vì sleep cứng
            nextButton.Click(); // Click tự nhiên - tạo ra đầy đủ chuỗi sự kiện chuột


            // Đợi URL thay đổi thông minh thay vì sleep 5 giây cứng nhắc
            try
            {
                new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                    .Until(d => !driver.Url.Equals(currentUrl));
            }
            catch (WebDriverTimeoutException)
            {
                // Timeout là bình thường, có thể trang không chuyển
            }
            try
                {
                    
                    string finalUrl = driver.Url;
                    if (finalUrl != currentUrl)
                    {
                        Console.WriteLine($"✅ Click lại thành công! URL mới: {finalUrl}");
                    }
                    else
                    {
                        // Chỉ reload trang khi ở trang đầu tiên
                        if (currentPage == 1)
                        {
                            try
                            {
                                // Reload trang
                                driver.Navigate().Refresh();
                                Thread.Sleep(3000); // Chờ trang load xong
                                
                                // Tìm và click nút Next sau khi reload
                                var nextButtonAfterReload = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                                    .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//span[contains(text(), 'Next')]")));
                                
                                RandomDelay(100, 200); // Random delay ngắn
                                nextButtonAfterReload.Click(); // Click tự nhiên thay vì JS click
                                
                                // Đợi URL thay đổi thông minh thay vì sleep 5 giây cứng nhắc
                                try
                                {
                                    new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                                        .Until(d => !driver.Url.Equals(currentUrl));
                                }
                                catch (WebDriverTimeoutException)
                                {
                                    // Timeout là bình thường, có thể trang không chuyển
                                }
                                
                            }
                            catch (Exception reloadEx)
                            {
                                Console.WriteLine($"❌ Không thể reload và click lại: {reloadEx.Message}");
                            }
                        }
                    }
                }
                catch (Exception retryEx)
                {
                    Console.WriteLine($"❌ Không thể click lại: {retryEx.Message}");
                
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi click Next: {ex.Message}");
        }
    }
    
    static void ClickReviewNextButton(IWebDriver driver)
    {
        try
        {
            // Sử dụng click tự nhiên với ElementToBeClickable - đã đảm bảo element sẵn sàng click
            var nextButton = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Next']]")));
            
            RandomDelay(50, 150); // Random delay ngắn thay vì sleep cứng
            nextButton.Click(); // Click tự nhiên - tạo ra đầy đủ mouse events
            RandomDelay(100, 200); // Random delay ngắn sau click
            Console.WriteLine("✅ Đã ấn nút Next ở màn hình Review account info");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không ấn được nút Next ở màn hình Review account info: {ex.Message}");
        }
    }

    static void FillDayAndYearNew(IWebDriver driver)
    {
        
            Random random = new Random();
            int day = random.Next(1, 29);
            int year = random.Next(1985, 2010);

            // Multiple selectors cho Day field - Google thường thay đổi structure
            IWebElement dayField = null;

                    dayField = new WebDriverWait(driver, TimeSpan.FromSeconds(3))
                        .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//input[@aria-label='Day']")));
                    

                dayField.Clear();
                HumanType(dayField, day.ToString());
           

            // Multiple selectors cho Year field
            IWebElement yearField = null;
            
                
                    yearField = new WebDriverWait(driver, TimeSpan.FromSeconds(3))
                        .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//input[@aria-label='Year']")));
            
                yearField.Clear();
                HumanType(yearField, year.ToString());
            

            Console.WriteLine($"🎂 Hoàn thành nhập sinh nhật: {day}/{year}");
        
        
    }


    static void FillMonthNew(IWebDriver driver)
{
    try
    {
        // Click vào dropdown tháng
        IWebElement monthDropdown = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//span[contains(text(), 'Month')]")));

        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
        js.ExecuteScript("arguments[0].click();", monthDropdown);

        // Đợi dropdown tháng mở ra thay vì sleep cứng
        new WebDriverWait(driver, TimeSpan.FromSeconds(5))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//li[@role='option']")));

        string[] months = {
                "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };
        Random random = new Random();
        string month = months[random.Next(months.Length)];

        // Tìm element tháng
        var selectedMonth = driver.FindElements(By.XPath("//li[@role='option']"))
            .FirstOrDefault(opt => opt.Text.Trim() == month);

        if (selectedMonth != null)
        {
                // Force scroll element into view trong dropdown container
                js.ExecuteScript(@"
                var element = arguments[0];
                var container = element.closest('.dropdown-menu, .select-dropdown, [role=""listbox""]');
                if (container) {
                    element.scrollIntoView({block: 'center', inline: 'nearest'});
                }
            ", selectedMonth);
            
            RandomDelay(50, 100); // Random delay ngắn thay vì sleep cứng

                // Click tự nhiên vào option được chọn
                js.ExecuteScript("arguments[0].click();", selectedMonth);
                Console.WriteLine("✅ Đã chọn tháng: " + month);
            }
        else
        {
            Console.WriteLine("❌ Không tìm thấy tháng: " + month);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ Lỗi thao tác dropdown tháng: " + ex.Message);
    }
}




    static void FillGenderNew(IWebDriver driver)
    {
        try
        {
            // Click vào dropdown giới tính (span)
            IWebElement genderDropdown = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//span[contains(text(), 'Gender')]")));

            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].click();", genderDropdown);

            // Đợi dropdown giới tính mở ra thay vì sleep cứng
            new WebDriverWait(driver, TimeSpan.FromSeconds(5))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//li[@role='option']")));

            string[] genders = { "Male", "Female"};
            Random random = new Random();
            string gender = genders[random.Next(genders.Length)];

            // Tìm element option giới tính đúng với text random
            var selectedGender = driver.FindElements(By.XPath("//li[@role='option']"))
                .FirstOrDefault(opt => opt.Text.Trim() == gender);

            if (selectedGender != null)
            {
                js.ExecuteScript("arguments[0].click();", selectedGender);
                Console.WriteLine("✅ Đã chọn giới tính: " + gender);
            }
            else
            {
                Console.WriteLine("❌ Không tìm thấy giới tính: " + gender);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Lỗi thao tác dropdown giới tính: " + ex.Message);
        }
    }

    static async Task<string> HandleRequestSever(IWebDriver driver, string userNameParam, string passwordParam)
    {
        var client = new HttpClient();
        string url = "https://dailyotp.com/api/rent-number?appBrand=Google / Gmail / Youtube&countryCode=US&serverName=Server 6&api_key=4cdba4a83cb5e06bf4f81bb491f7a434vUo9b9CciGZ1VPPjbDcj";

        HttpResponseMessage response = await client.GetAsync(url);
        string body = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("✅ API OK: " + body);

            string jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OtpResponse>(jsonResponse);

            phoneNumber2FA = result.data.phoneNumber;
            Console.WriteLine($"Số thuê: {result.data.phoneNumber}");
            Console.WriteLine($"transId: {result.data.transId}");
            if(phoneNumber2FA == null || phoneNumber2FA == "")
            {
                Console.WriteLine("❌ Không có số điện thoại nào được thuê.");
            }
            try
            {
                IWebElement phoneInput = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                    .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@type='tel']")));

                phoneInput.Clear();
                phoneInput.SendKeys(result.data.phoneNumber);
                Thread.Sleep(1000);

                ClickNextButton(driver);
                await HandleGetCode(driver, result.data.transId, userNameParam, passwordParam);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Lỗi khi nhập số điện thoại hoặc nhấn Next: " + ex.Message);
            }
            return result.data.phoneNumber;
        }
        else
        {
            Console.WriteLine("❌ Lỗi gọi API: " + response.StatusCode);
            Console.WriteLine("📦 Nội dung: " + body);
            return "";
        }
    }

    static async Task HandleGetCode(IWebDriver driver, string transId, string userNameParam, string passwordParam)
    {
        string url = $"https://dailyotp.com/api/get-messages?transId={transId}&api_key=4cdba4a83cb5e06bf4f81bb491f7a434vUo9b9CciGZ1VPPjbDcj";
        var client = new HttpClient();

        int retry = 0;
        const int maxRetry = 15;

        while (retry < maxRetry)
        {
            HttpResponseMessage response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();

                var match = Regex.Match(json, @"\b\d{6}\b");
                if (match.Success)
                {
                    string otpCode = match.Value;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✅ MÃ OTP: " + otpCode);
                    Console.ResetColor();

                    try
                    {
                        IWebElement otpField = new WebDriverWait(driver, TimeSpan.FromSeconds(20))
                            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@type='tel']")));

                        otpField.Clear();
                        otpField.SendKeys(otpCode);
                        Thread.Sleep(1000);

                        ClickNextButton(driver);
                        // Ghi vào Excel với Authenticator Key
                        HandleWriteExcel(currentGmail, currentPassword, currentAuthenticatorKey);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("❌ Lỗi khi nhập mã OTP hoặc nhấn Next: " + ex.Message);
                    }

                    break;
                }
                else
                {
                    Console.WriteLine($"⏳ Chờ OTP... ({retry + 1})");
                }
            }
            else
            {
                Console.WriteLine("❌ Lỗi khi gọi get-messages.");
                break;
            }

            retry++;
            await Task.Delay(3000);
        }

        if (retry >= maxRetry)
        {
            Console.WriteLine("⚠️ Quá thời gian chờ mã OTP. Đóng tab và chuyển sang tab mới...");
            try
            {
                driver.Quit();
                driver.Dispose();
                Console.WriteLine("✅ Đã đóng tab thành công");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi đóng tab: {ex.Message}");
            }
            // Thoát khỏi hàm để không tiếp tục xử lý
            return;
        }
    }

    static void HandleWriteExcel(string userNameParam, string passwordParam, string authenticatorKey = null)
    {
        // Đường dẫn tới file Excel có sẵn
        string filePath = @"C:\Users\lqanh\OneDrive\ドキュメント\Reg\TestWriteInExel\ExcelDataGmailData.xlsx";

        // Kiểm tra file có tồn tại không
        if (!File.Exists(filePath))
        {
            Console.WriteLine("❌ File Excel không tồn tại tại đường dẫn: " + filePath);
            return;
        }

        try
        {
            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet(1); // hoặc đặt tên sheet nếu muốn

                // Tìm dòng đầu tiên trống tại cột A (userName)
                int currentRow = 2; // Bỏ qua dòng tiêu đề
                while (!string.IsNullOrWhiteSpace(worksheet.Cell(currentRow, 1).GetString()))
                {
                    currentRow++;
                }

                // Ghi dữ liệu vào dòng trống
                worksheet.Cell(currentRow, 1).Value = userNameParam; // Cột A: Gmail
                worksheet.Cell(currentRow, 2).Value = passwordParam; // Cột B: Password
                
                // Ghi Authenticator Key vào cột C (cột 3)
                string keyToWrite = authenticatorKey ?? currentAuthenticatorKey;
                if (!string.IsNullOrEmpty(keyToWrite))
                {
                    worksheet.Cell(currentRow, 3).Value = keyToWrite;
                    Console.WriteLine($"🔑 Đã ghi Authenticator Key vào cột C: {keyToWrite}");
                }
                else
                {
                    Console.WriteLine("⚠️ Không có Authenticator Key để ghi vào Excel");
                }

                // Lưu file
                workbook.Save();
                Console.WriteLine("✅ Đã ghi dữ liệu vào Excel!");
                Console.WriteLine($"📊 Gmail: {userNameParam} | Password: {passwordParam} | Key: {keyToWrite ?? "N/A"}");
              
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine("❌ Không thể ghi vào file Excel. Có thể đang mở file. Chi tiết: " + ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi khi ghi Excel: {ex.Message}");
        }
    }

    // Hàm cập nhật Authenticator Key cho Gmail đã tồn tại
    static void UpdateAuthenticatorKeyInExcel(string userNameParam, string authenticatorKey)
    {
        string filePath = @"C:\Users\lqanh\OneDrive\ドキュメント\Reg\TestWriteInExel\ExcelDataGmailData.xlsx";

        if (!File.Exists(filePath))
        {
            Console.WriteLine("❌ File Excel không tồn tại tại đường dẫn: " + filePath);
            return;
        }

        try
        {
            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet(1);

                // Tìm dòng chứa Gmail cần cập nhật
                int rowToUpdate = -1;
                for (int row = 2; row <= 1000; row++) // Giới hạn tìm trong 1000 dòng đầu
                {
                    string existingGmail = worksheet.Cell(row, 1).GetString();
                    if (existingGmail == userNameParam)
                    {
                        rowToUpdate = row;
                        break;
                    }
                    else if (string.IsNullOrWhiteSpace(existingGmail))
                    {
                        break; // Dừng khi gặp dòng trống
                    }
                }

                if (rowToUpdate > 0)
                {
                    // Cập nhật Authenticator Key vào cột C
                    worksheet.Cell(rowToUpdate, 3).Value = authenticatorKey;
                    workbook.Save();
                    Console.WriteLine($"✅ Đã cập nhật Authenticator Key cho {userNameParam}: {authenticatorKey}");
                }
                else
                {
                    Console.WriteLine($"⚠️ Không tìm thấy Gmail {userNameParam} trong Excel để cập nhật");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi khi cập nhật Excel: {ex.Message}");
        }
    }

    static void TryClickCreateOwnGmail(IWebDriver driver)
    {
        try
        {
            Console.WriteLine("🔍 Đang tìm kiếm option 'Create your own Gmail address'...");
            
            // Kiểm tra xem có element "Create your own Gmail address" không
            var createOwnElements = driver.FindElements(By.XPath("//*[contains(text(), 'Create your own Gmail address')]"));
            
            if (createOwnElements.Count > 0)
            {
                // Sử dụng click tự nhiên cho option "Create your own Gmail address"
                var createOwnOption = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                    .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(createOwnElements[0]));
                createOwnOption.Click(); // Click tự nhiên thay vì JavaScript click
                Console.WriteLine("✅ Đã chọn 'Create your own Gmail address'");
            }
            else
            {
                // Nếu không tìm thấy, kiểm tra các trường hợp khác
                Console.WriteLine("ℹ️ Không tìm thấy option 'Create your own Gmail address', kiểm tra các trường hợp khác...");
                
                // Kiểm tra xem có popup "How you'll sign in" không
                var popupElements = driver.FindElements(By.XPath("//*[contains(text(), 'How you'll sign in') or contains(text(), 'Create a Gmail address for signing in')]"));
                
                if (popupElements.Count > 0)
                {
                    Console.WriteLine("✅ Phát hiện popup 'How you'll sign in', đây là trường hợp bình thường");
                    
                    // Kiểm tra xem có trường Username trong popup không
                    var usernameFields = driver.FindElements(By.XPath("//input[@aria-label='Username' or @aria-label='Create a Gmail address' or contains(@aria-label, 'username')]"));
                    
                    if (usernameFields.Count > 0)
                    {
                        Console.WriteLine("✅ Trường Username đã có sẵn trong popup, tiếp tục bình thường");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ Không tìm thấy trường Username trong popup, có thể cần thao tác khác");
                    }
                }
                else
                {
                    // Nếu không có popup, kiểm tra xem có trường Username nào đã hiển thị chưa
                    var existingUsernameFields = driver.FindElements(By.XPath("//input[@aria-label='Username' or @aria-label='Create a Gmail address']"));
                    
                    if (existingUsernameFields.Count > 0)
                    {
                        Console.WriteLine("✅ Trường Username đã có sẵn, không cần click thêm");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ Không tìm thấy trường Username nào, có thể cần thao tác khác");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Lỗi khi xử lý 'Create your own Gmail address': " + ex.Message);
        }
    }

    static void RandomDelay(int min = 300, int max = 900)
    {
        Random rnd = new Random();
        Thread.Sleep(rnd.Next(min, max));
    }

    

    static void ClickSkipRecoveryEmailButton(IWebDriver driver)
    {
        try
        {
            // Sử dụng click tự nhiên cho nút Skip - ElementToBeClickable đã đảm bảo element sẵn sàng
            var skipButton = new WebDriverWait(driver, TimeSpan.FromSeconds(5))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Skip']]")));
            
            Thread.Sleep(200);
            skipButton.Click(); // Click tự nhiên thay vì JavaScript click
            Thread.Sleep(1000);
            Console.WriteLine("✅ Đã ấn nút Skip ở popup recovery email");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không ấn được nút Skip ở popup recovery email (có thể không xuất hiện): {ex.Message}");
        }
    }

    static void ClickPrivacyAgreeButton(IWebDriver driver)
    {
        try
        {
            // Scroll tự nhiên thay vì JavaScript scroll để tìm nút I agree
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
            Thread.Sleep(1000);
            
            // Sử dụng click tự nhiên cho nút I agree
            var agreeButton = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='I agree']]")));
            
            Thread.Sleep(200);
            agreeButton.Click(); // Click tự nhiên thay vì JavaScript click
            Thread.Sleep(1000);
            Console.WriteLine("✅ Đã ấn nút I agree ở màn hình Privacy and Terms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không ấn được nút I agree: {ex.Message}");
        } 
    }

    static void ClickConfirmPersonalizationButton(IWebDriver driver)
    {
        try
        {
            // Sử dụng click tự nhiên cho nút Confirm
            var confirmButton = new WebDriverWait(driver, TimeSpan.FromSeconds(5))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Confirm']]")));
            
            Thread.Sleep(200);
            confirmButton.Click(); // Click tự nhiên thay vì JavaScript click
            Thread.Sleep(1000);
            Console.WriteLine("✅ Đã ấn nút Confirm trên popup cá nhân hóa");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không ấn được nút Confirm trên popup cá nhân hóa: {ex.Message}");
        }
    }

    static void GoToGoogle2FA(IWebDriver driver)
    {
        try
        {
            string url2FA = ConfigManager.Google_2FA_URL;
            driver.Navigate().GoToUrl(url2FA);
            Thread.Sleep(3000);
            Console.WriteLine("✅ Đã truy cập vào trang bảo mật 2FA của Google");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không truy cập được trang 2FA: {ex.Message}");
        }
    }

    static void ClickAddPhoneNumberButton(IWebDriver driver, string phoneNumber)
    {
        try
        {
            
            // Đợi trang load hoàn toàn
            Thread.Sleep(3000);
            
            IWebElement addPhoneBtn = null;
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            
            // Danh sách các XPath và CSS selector để tìm nút
            var selectors = new[]
            {
                "//button[contains(text(), 'Add phone number')]",
                "//button[.//span[contains(text(), 'Add phone number')]]",
                "//button[.//div[contains(text(), 'Add phone number')]]",
                "//button[@aria-label='Add phone number']",
                "//button[@data-action='add-phone']",
                "//div[contains(@class, 'add-phone')]//button",
                "//button[contains(@class, 'add-phone')]",
                "//a[contains(text(), 'Add phone number')]",
                "//span[contains(text(), 'Add phone number')]/parent::button",
                "//div[contains(text(), 'Add phone number')]/parent::button"
            };
            foreach (var selector in selectors)
            {
                try
                {
                    addPhoneBtn = new WebDriverWait(driver, TimeSpan.FromSeconds(3))
                        .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath(selector)));
                    
                    if (addPhoneBtn != null)
                    {
                        break;
                    }
                }
                catch
                {
                    continue;
                }
            }
            bool clickSuccess = false;
            
            // Cách 1: Click tự nhiên trước (ưu tiên cao hơn)
            try
            {
                addPhoneBtn.Click(); // Click tự nhiên - tạo ra đầy đủ mouse events
                clickSuccess = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Click tự nhiên thất bại, thử JavaScript click: {ex.Message}");
                
                // Fallback: JavaScript click chỉ khi thật sự cần thiết
                try
                {
                    var jsExecutor = (IJavaScriptExecutor)driver;
                    jsExecutor.ExecuteScript("arguments[0].click();", addPhoneBtn);
                    clickSuccess = true;
                }
                catch (Exception jsEx)
                {
                    Console.WriteLine($"⚠️ JavaScript click cũng thất bại: {jsEx.Message}");
                }
            }
            
            // Cách 2: Click bằng Actions
            if (!clickSuccess)
            {
                try
                {
                    var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                    actions.MoveToElement(addPhoneBtn).Click().Perform();
                    clickSuccess = true;
                    Console.WriteLine("✅ Click thành công bằng Actions");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Click Actions thất bại: {ex.Message}");
                }
            }
            
            // Cách 3: Click thường
            if (!clickSuccess)
            {
                try
                {
                    addPhoneBtn.Click();
                    clickSuccess = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Click thường thất bại: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi trong ClickAddPhoneNumberButton: {ex.Message}");
            Console.WriteLine($"📄 Stack trace: {ex.StackTrace}");
        }
    }
    static void Fill2FAPhoneAndNext(IWebDriver driver, string phoneNumber)
    {
        try
        {
            // Tìm ô nhập số điện thoại
            IWebElement phoneInput = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@type='tel' and @aria-label]")));
            phoneInput.Clear();
            phoneInput.SendKeys(phoneNumber);
            RandomDelay(100, 200); // Random delay sau khi nhập số điện thoại
            
            // Sử dụng click tự nhiên cho nút Next
            var nextButton = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Next']]")));
            
            RandomDelay(50, 150); // Random delay trước click
            nextButton.Click(); // Click tự nhiên thay vì JavaScript click
            RandomDelay(100, 200); // Random delay sau click
            Console.WriteLine($"✅ Đã điền số điện thoại 2FA và ấn Next: {phoneNumber}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không điền được số điện thoại 2FA hoặc không ấn được Next: {ex.Message}");
        }
    }

    static void ClickConfirmPhoneSaveButton(IWebDriver driver)
    {
        try
        {
            // Sử dụng click tự nhiên cho nút Save
            var saveButton = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Save']]")));
            
            RandomDelay(50, 150); // Random delay trước click
            saveButton.Click(); // Click tự nhiên thay vì JavaScript click
            
            // Đợi trang load sau khi Save thay vì sleep 5s cứng nhắc
            WaitForPageLoad(driver, 10);
            Console.WriteLine("✅ Đã ấn nút Save xác nhận số điện thoại 2FA");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không ấn được nút Save xác nhận số điện thoại 2FA: {ex.Message}");
        }
    }

    static void ClickDoneButtonAfterPhoneVerify(IWebDriver driver)
    {
        try
        {
            // Sử dụng click tự nhiên cho nút Done
            var doneButton = new WebDriverWait(driver, TimeSpan.FromSeconds(30))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Done']]")));
            
            RandomDelay(50, 150); // Random delay trước click
            doneButton.Click(); // Click tự nhiên thay vì JavaScript click
            RandomDelay(100, 200); // Random delay sau click
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không ấn được nút Done sau xác nhận số điện thoại 2FA: {ex.Message}");
        }
    }

    static void GoToAuthenticatorAppAndSetup(IWebDriver driver)
    {
        try
        {
            // Truy cập vào trang Authenticator app và đợi trang load
            string urlAuthApp = ConfigManager.Google_Authenticator_URL;
            driver.Navigate().GoToUrl(urlAuthApp);
            WaitForPageLoad(driver, 10); // Đợi trang load thay vì sleep 3s cứng nhắc
            
            // Tìm và click nút Set up authenticator
            IWebElement setupBtn = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[contains(text(),'Set up authenticator')]]")));
            RandomDelay(50, 150); // Random delay trước click
            setupBtn.Click(); // Click tự nhiên thay vì JavaScript click
            RandomDelay(100, 200); // Random delay sau click
            Console.WriteLine("✅ Đã truy cập và ấn nút Set up authenticator");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không truy cập hoặc không ấn được nút Set up authenticator: {ex.Message}");
        }
    }

    static void ClickCantScanItLink(IWebDriver driver)
    {
        try
        {
            Thread.Sleep(2000);
            
            IWebElement cantScanLink = null;
            try
            {
                var allButtons = driver.FindElements(By.TagName("button"));

                foreach (var button in allButtons)
                {
                    try
                    {
                        string buttonText = button.Text.Trim();

                        // Chỉ chọn button có text chính xác "Can't scan it?" hoặc jsname="Pr7Yme"
                        if ((buttonText.Contains("Can't scan it?") || buttonText.Contains("scan")) &&
                            !buttonText.Contains("Set up") && !buttonText.Contains("authenticator"))
                        {
                            cantScanLink = button;
                            break;
                        }
                    }
                    catch { continue; }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Không tìm thấy button nào: {ex.Message}");
            }


            if (cantScanLink != null)
            {
                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

                // Thử click trực tiếp trước
                try
                {
                    cantScanLink.Click();
                    Thread.Sleep(1000);
                    return;
                }
                catch (Exception ex1)
                {
                    Console.WriteLine($"⚠️ Click trực tiếp thất bại: {ex1.Message}");
                }

                // Thử JavaScript click
                try
                {
                    js.ExecuteScript("arguments[0].click();", cantScanLink);
                    Thread.Sleep(1000);
                    return;
                }
                catch (Exception ex1)
                {
                    Console.WriteLine($"⚠️ JavaScript click thất bại: {ex1.Message}");
                }

                // Thử Actions click
                try
                {
                    var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                    actions.MoveToElement(cantScanLink).Click().Perform();
                    Thread.Sleep(1000);
                    return;
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"⚠️ Actions click thất bại: {ex2.Message}");
                }

                // Thử hover trước rồi click
                try
                {
                    var actions = new OpenQA.Selenium.Interactions.Actions(driver);

                    // Hover vào element trước
                    actions.MoveToElement(cantScanLink).Perform();
                    Thread.Sleep(500);

                    // Sau đó click
                    actions.Click().Perform();
                    Thread.Sleep(1000);
                }
                catch (Exception ex3)
                {
                    Console.WriteLine($"❌ Tất cả phương pháp click đều thất bại: {ex3.Message}");
                }
            }
            else
            {
                Console.WriteLine("❌ Không tìm thấy button 'Can't scan it?'");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không click được link Can't scan it?: {ex.Message}");
        }
    }

    static string ExtractAuthenticatorKey(IWebDriver driver)
    {
        try
        {
            Thread.Sleep(2000); // Tăng thời gian đợi popup xuất hiện
            
            // Đợi popup xuất hiện và tìm element chứa key
            IWebElement popup = null;
            string popupText = "";
            
            // Tìm kiếm trực tiếp thẻ strong chứa key
            if (popup == null || !IsValidAuthenticatorKey(popupText))
            {
                try
                {
                    var strongElements = driver.FindElements(By.TagName("strong"));
                    foreach (var strong in strongElements)
                    {
                        try
                        {
                            string strongText = strong.Text.Trim();
                            
                            if (IsValidAuthenticatorKey(strongText))
                            {
                                popupText = strongText;
                                break;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Lỗi khi tìm thẻ strong: {ex.Message}");
                }
            }
            var patterns = new[]
            {
                @"([a-z0-9]{4}\s+){7}[a-z0-9]{4}", // Pattern chính xác cho key 32 ký tự
                @"([a-z0-9]{4,}\s+){3,}[a-z0-9]{4,}", // Pattern linh hoạt hơn
                @"[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}", // Pattern cụ thể
                @"\b[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\b", // Pattern với word boundary
                // Thêm pattern mới dựa trên key thực tế từ hình ảnh
                @"\b[a-z0-9]{4}\s+[0-9][a-z0-9]{3}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[0-9][a-z0-9]{3}\s+[a-z0-9]{4}\b"
            };
            
            foreach (var pattern in patterns)
            {
                try
                {
                    var match = System.Text.RegularExpressions.Regex.Match(popupText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        string keyWithSpaces = match.Value.Trim();
                        string keyWithoutSpaces = keyWithSpaces.Replace(" ", "");
                        Console.WriteLine($"📝 Key gốc với khoảng trắng: {keyWithSpaces}");
                        
                        // Kiểm tra độ dài key (thường là 32 ký tự)
                        if (keyWithoutSpaces.Length == 32)
                        {
                            return keyWithSpaces; // Trả về key với khoảng trắng
                        }
                        else
                        {
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Lỗi với pattern {pattern}: {ex.Message}");
                }
            }
            Console.WriteLine("❌ Không tìm thấy key Authenticator trong popup!");
            Console.WriteLine($"📄 Nội dung popup đầy đủ: {popupText}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi khi lấy key Authenticator: {ex.Message}");
            Console.WriteLine($"📄 Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    static bool IsValidAuthenticatorKey(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
            
        // Loại bỏ khoảng trắng và chuyển về chữ thường
        string cleanText = text.Replace(" ", "").ToLower();
        
        // Kiểm tra độ dài (thường là 32 ký tự cho Base32)
        if (cleanText.Length != 32)
            return false;
            
        // Kiểm tra chỉ chứa ký tự Base32 hợp lệ
        string validChars = "abcdefghijklmnopqrstuvwxyz234567";
        return cleanText.All(c => validChars.Contains(c));
    }
    
    static string GenerateOtpCode(string key)
    {
        try
        {
            var bytes = OtpNet.Base32Encoding.ToBytes(key.ToUpper());
            var totp = new OtpNet.Totp(bytes);
            string otp = totp.ComputeTotp();
            Console.WriteLine($"✅ Mã OTP từ key: {otp}");
            return otp;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi tạo mã OTP từ key: {ex.Message}");
            return null;
        }
    }

    static void FillAuthenticatorCodeAndVerify(IWebDriver driver, string otpCode)
    {
        try
        {
            // Tìm ô nhập code
            IWebElement codeInput = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@type='text' and @aria-label] | //input[@type='text' and @autocomplete] | //input[@type='text']")));
            codeInput.Clear();
            codeInput.SendKeys(otpCode);
            RandomDelay(100, 200); // Random delay sau khi nhập OTP
            
            // Tìm và click nút Verify
            // Sử dụng click tự nhiên cho nút Verify
            var verifyBtn = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//span[contains(text(), 'Verify')]")));
            
            RandomDelay(50, 150); // Random delay trước click
            verifyBtn.Click(); // Click tự nhiên thay vì JavaScript click
            RandomDelay(100, 200); // Random delay sau click
            Console.WriteLine($"✅ Đã điền mã OTP và ấn Verify: {otpCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không điền được mã OTP hoặc không ấn được Verify: {ex.Message}");
        }
    }

    static void Remove2FAPhoneNumber(IWebDriver driver)
    {
        // Quay lại trang 2FA phone
                    string url2FA = ConfigManager.Google_PhoneNumbers_URL;
        driver.Navigate().GoToUrl(url2FA);
        Thread.Sleep(3000);
        Console.WriteLine("🔍 Đang tìm kiếm các button có thể xóa...");
        var allButtons = driver.FindElements(By.XPath("//button"));
        foreach (var btn in allButtons)
        {
            try
            {
                string ariaLabel = btn.GetAttribute("aria-label");
                string jsname = btn.GetAttribute("jsname");
                string className = btn.GetAttribute("class");

                if (ariaLabel != null && (ariaLabel.Contains("Delete") || ariaLabel.Contains("Remove")))
                {
                    Console.WriteLine($"📋 Tìm thấy button: aria-label='{ariaLabel}', jsname='{jsname}', class='{className}'");
                }
            }
            catch { }
        }

        // Dựa trên HTML structure đã cung cấp, tìm button có aria-label chứa "Delete phone number"
        // HTML: <button jsname="Pr7Yme" aria-label="Delete phone number: (815) 523-6515" aria-haspopup="true">
        IWebElement trashBtn = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(
                By.XPath("//button[contains(@aria-label, 'Delete phone number')]")
            ));

        // Scroll đến element để đảm bảo nó hiển thị
        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
        js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", trashBtn);
        Thread.Sleep(500);

        // Ưu tiên click tự nhiên trước
        try
        {
            trashBtn.Click(); // Click tự nhiên - ưu tiên cao hơn
            Console.WriteLine("✅ Đã click vào biểu tượng thùng rác bằng click tự nhiên");
        }
        catch (Exception ex)
        {
            // Fallback: JavaScript click chỉ khi thật sự cần thiết  
            Console.WriteLine($"⚠️ Click tự nhiên thất bại: {ex.Message}, thử JavaScript click");
            try
            {
                var jsExecutor = (IJavaScriptExecutor)driver;
                jsExecutor.ExecuteScript("arguments[0].click();", trashBtn);
                Console.WriteLine("✅ Đã click vào biểu tượng thùng rác bằng JavaScript");
            }
            catch (Exception jsEx)
            {
                Console.WriteLine($"❌ JavaScript click cũng thất bại: {jsEx.Message}");
                throw;
            }
        }

        Thread.Sleep(1000);

        // Kiểm tra xem có dialog xác nhận xuất hiện không
        try
        {
            // Sử dụng click tự nhiên cho nút OK xác nhận xóa
            var verifyBtn = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//span[contains(text(), 'OK')]")));
            
            Thread.Sleep(200);
            verifyBtn.Click(); // Click tự nhiên thay vì JavaScript click
            Console.WriteLine("✅ Đã xác nhận xóa số điện thoại");
            Thread.Sleep(1000);
        }
        catch
        {
            Console.WriteLine("ℹ️ Không tìm thấy dialog xác nhận, có thể đã xóa trực tiếp");
        }

        Console.WriteLine("✅ Đã xóa số điện thoại 2FA thành công");

    }

    // Hàm mô phỏng thao tác người dùng thật: di chuột, rê chuột, cuộn trang, click linh tinh, delay ngẫu nhiên
    static void HumanLikeActions(IWebDriver driver)
    {
        try 
        {
            int actionCount = _random.Next(2, 5); // Giảm số hành động để tự nhiên hơn
            int width = driver.Manage().Window.Size.Width;
            int height = driver.Manage().Window.Size.Height;
            
            Console.WriteLine($"🎭 Mô phỏng {actionCount} hành động người dùng thật...");

            // ✅ THAO TÁC MÔ PHỎNG NGƯỜI DÙNG THẬT TỐI ƯU HÓA
            for (int i = 0; i < actionCount; i++)
            {
                int actionType = _random.Next(0, 7); // Thêm scroll ngắn liên tiếp
                switch (actionType)
                {
                    case 0: // Cuộn trang nhẹ nhàng (thao tác phổ biến nhất)
                        int scrollY = _random.Next(50, 200);
                        ((IJavaScriptExecutor)driver).ExecuteScript($"window.scrollBy({{top: {scrollY}, left: 0, behavior: 'smooth'}});");
                        Thread.Sleep(_random.Next(800, 2000));
                        break;
                        
                    case 1: // Di chuyển chuột tự nhiên với chuyển động mượt mà
                        try
                        {
                            // Đảm bảo tọa độ nằm trong phạm vi an toàn
                            int safeMargin = 50;
                            int startX = _random.Next(safeMargin, width - safeMargin);
                            int startY = _random.Next(safeMargin, height - safeMargin);
                            int endX = _random.Next(safeMargin, width - safeMargin);
                            int endY = _random.Next(safeMargin, height - safeMargin);
                            SmoothMouseMove(driver, startX, startY, endX, endY);
                            Thread.Sleep(_random.Next(500, 1500));
                        }
                        catch (Exception mouseEx)
                        {
                            Console.WriteLine($"⚠️ Lỗi di chuyển chuột: {mouseEx.Message}");
                        }
                        break;
                        
                    case 2: // Dừng lại đọc (giả vờ đọc nội dung)
                        Thread.Sleep(_random.Next(1500, 4000));
                        break;
                        
                    case 3: // Hover trên các element để mô phỏng việc đọc
                        try
                        {
                            // Tìm các element button, input, link thay vì chỉ span
                            var buttonElements = driver.FindElements(By.TagName("button"));
                            var inputElements = driver.FindElements(By.TagName("input"));
                            var linkElements = driver.FindElements(By.TagName("a"));
                            
                            // Gộp tất cả các element lại
                            var allElements = new List<IWebElement>();
                            allElements.AddRange(buttonElements);
                            allElements.AddRange(inputElements);
                            allElements.AddRange(linkElements);
                            
                            if (allElements.Count > 0)
                            {
                                var randomElement = allElements[_random.Next(0, Math.Min(allElements.Count, 10))];
                                var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                                actions.MoveToElement(randomElement).Perform();
                                Console.WriteLine($"🎯 Hover vào element: {randomElement.TagName}");
                                Thread.Sleep(_random.Next(800, 2000));
                            }
                        }
                        catch { }
                        break;
                        
                    case 4: // Cuộn nhẹ về phía trên
                        int scrollUp = _random.Next(-100, -20);
                        ((IJavaScriptExecutor)driver).ExecuteScript($"window.scrollBy({{top: {scrollUp}, left: 0, behavior: 'smooth'}});");
                        Thread.Sleep(_random.Next(600, 1200));
                        break;
                        
                    case 5: // Di chuyển chuột tự nhiên với chuyển động mượt mà (không click)
                        try
                        {
                            // Đảm bảo tọa độ nằm trong phạm vi an toàn
                            int safeMargin = 50;
                            int startX = _random.Next(safeMargin, width - safeMargin);
                            int startY = _random.Next(safeMargin, height - safeMargin);
                            int endX = _random.Next(safeMargin, width - safeMargin);
                            int endY = _random.Next(safeMargin, height - safeMargin);
                            SmoothMouseMove(driver, startX, startY, endX, endY);
                            Thread.Sleep(_random.Next(300, 800));
                        }
                        catch (Exception mouseEx)
                        {
                            Console.WriteLine($"⚠️ Lỗi di chuyển chuột: {mouseEx.Message}");
                        }
                        break;
                        
                    case 6: // Scroll ngắn liên tiếp (mô phỏng cuộn bánh xe chuột)
                        try
                        {
                            int scrollCount = _random.Next(2, 5); // Số lần scroll ngắn
                            Console.WriteLine($"🖱️ Thực hiện {scrollCount} lần scroll ngắn liên tiếp...");
                            
                            for (int scroll = 0; scroll < scrollCount; scroll++)
                            {
                                // Scroll ngắn với khoảng cách nhỏ (10-30px)
                                int shortScroll = _random.Next(10, 31);
                                if (_random.Next(0, 2) == 0) // 50% khả năng scroll xuống
                                {
                                    ((IJavaScriptExecutor)driver).ExecuteScript($"window.scrollBy({{top: {shortScroll}, left: 0, behavior: 'smooth'}});");
                                }
                                else // 50% khả năng scroll lên
                                {
                                    ((IJavaScriptExecutor)driver).ExecuteScript($"window.scrollBy({{top: -{shortScroll}, left: 0, behavior: 'smooth'}});");
                                }
                                
                                // Delay ngắn giữa các lần scroll (100-300ms)
                                Thread.Sleep(_random.Next(100, 301));
                            }
                            
                            // Delay sau khi hoàn thành chuỗi scroll
                            Thread.Sleep(_random.Next(500, 1200));
                        }
                        catch (Exception scrollEx)
                        {
                            Console.WriteLine($"⚠️ Lỗi khi scroll ngắn liên tiếp: {scrollEx.Message}");
                        }
                        break;
                }
                // Delay ngẫu nhiên giữa các hành động
                Thread.Sleep(_random.Next(200, 800));
            }
            // Dừng lại lâu hơn ở cuối
            Thread.Sleep(_random.Next(1500, 3500));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi trong HumanLikeActions: {ex.Message}");
        }
    }

    static void ClickNextButtonAfterAuthenticatorKey(IWebDriver driver, string authKeyWithSpaces)
    {
        // Sử dụng click tự nhiên cho nút Next
        var nextButton = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//span[contains(text(), 'Next')]")));
        
        RandomDelay(50, 150); // Random delay trước click
        nextButton.Click(); // Click tự nhiên thay vì JavaScript click
        RandomDelay(100, 200); // Random delay sau click
        // Ấn nút Next sau khi lấy key Authenticator
        Console.WriteLine("✅ Đã ấn nút Next sau khi lấy key Authenticator");

        // Sau khi ấn Next, điền mã OTP và ấn Verify
        if (!string.IsNullOrEmpty(authKeyWithSpaces))
        {
            // Sử dụng lại key không có khoảng trắng để tạo OTP
            string authKeyWithoutSpaces = authKeyWithSpaces.Replace(" ", "");
            string otpCode = GenerateOtpCode(authKeyWithoutSpaces);
            FillAuthenticatorCodeAndVerify(driver, otpCode);
        }
    }

    // Hàm di chuyển chuột mượt mà theo từng bước
    static void SmoothMouseMove(IWebDriver driver, int startX, int startY, int endX, int endY)
    {
        try
        {
            var actions = new OpenQA.Selenium.Interactions.Actions(driver);
            
            // Đảm bảo tọa độ không vượt quá kích thước cửa sổ
            int windowWidth = driver.Manage().Window.Size.Width;
            int windowHeight = driver.Manage().Window.Size.Height;
            
            // Giới hạn tọa độ trong phạm vi cửa sổ
            startX = Math.Max(0, Math.Min(startX, windowWidth - 1));
            startY = Math.Max(0, Math.Min(startY, windowHeight - 1));
            endX = Math.Max(0, Math.Min(endX, windowWidth - 1));
            endY = Math.Max(0, Math.Min(endY, windowHeight - 1));
            
            // Di chuyển chuột đến vị trí bắt đầu trước
            actions.MoveToLocation(startX, startY).Perform();
            Thread.Sleep(_random.Next(50, 100));
            
            // Di chuyển chuột theo từng bước mượt mà
            int steps = 15; // Giảm số bước để tránh lỗi
            for (int step = 1; step <= steps; step++)
            {
                int stepX = startX + (endX - startX) * step / steps;
                int stepY = startY + (endY - startY) * step / steps;
                
                // Sử dụng MoveToLocation thay vì MoveByOffset
                actions.MoveToLocation(stepX, stepY).Perform();
                Thread.Sleep(_random.Next(15, 40));
            }
            
            Console.WriteLine($"🖱️ Đã di chuyển chuột mượt mà từ ({startX}, {startY}) đến ({endX}, {endY})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi di chuyển chuột mượt mà: {ex.Message}");
            // Fallback: thử di chuyển trực tiếp đến vị trí cuối
            try
            {
                var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                actions.MoveToLocation(endX, endY).Perform();
                Console.WriteLine($"🖱️ Đã di chuyển chuột trực tiếp đến ({endX}, {endY})");
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"❌ Không thể di chuyển chuột: {fallbackEx.Message}");
            }
        }
    }

    
    // Hàm nhập text nâng cao với nhiều hành vi mô phỏng người dùng thật - PHIÊN BẢN MỚI
    static void HumanTypeAdvanced(IWebElement element, string text, bool enableBackspace = true, bool enablePause = true, bool enableDoubleType = true)
    {
        // Sử dụng _random global để tránh pattern
        Random randomDelay = _random;
        Random randomBehavior = _random;
        
        Console.WriteLine($"🔍 HumanTypeAdvanced - Bắt đầu nhập: '{text}' (độ dài: {text.Length})");
        
        // Đảm bảo element sẵn sàng
        try
        {
            element.Clear();
            Thread.Sleep(200);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Không thể clear element: {ex.Message}");
        }
        
        // Theo dõi vị trí hiện tại trong text
        int currentPosition = 0;
        
        while (currentPosition < text.Length)
        {
            char currentChar = text[currentPosition];
            
            // Có 2% khả năng nhập sai ký tự và sửa lại (double type)
            if (enableDoubleType && randomBehavior.Next(1, 51) == 1 && currentPosition < text.Length - 1)
            {
                // Nhập sai ký tự
                char wrongChar = GetRandomWrongChar(currentChar);
                element.SendKeys(wrongChar.ToString());
                Thread.Sleep(randomDelay.Next(100, 200));
                
                // Backspace để xóa ký tự sai
                element.SendKeys(OpenQA.Selenium.Keys.Backspace);
                Thread.Sleep(randomDelay.Next(80, 150));
                
                Console.WriteLine($"🔄 Double type: '{wrongChar}' -> backspace -> '{currentChar}'");
            }
            
            // Nhập ký tự đúng với tốc độ thay đổi theo ngữ cảnh
            element.SendKeys(currentChar.ToString());
            currentPosition++;
            
            // Tốc độ gõ thay đổi theo ngữ cảnh (giống người thật)
            int baseSpeed = randomDelay.Next(60, 220); // Dải rộng hơn
            
            // Chậm hơn khi gặp khoảng trắng, dấu chấm, dấu phẩy
            if (currentChar == ' ' || currentChar == '.' || currentChar == ',')
            {
                baseSpeed += randomDelay.Next(100, 300); // Pause tự nhiên
            }
            
            // Nhanh hơn khi gõ liên tục (không có khoảng trắng)
            if (currentPosition > 1 && text[currentPosition - 2] != ' ')
            {
                baseSpeed = Math.Max(40, baseSpeed - randomDelay.Next(20, 60));
            }
            
            // 5% chance có "spike" dài (mô phỏng bị phân tâm)
            if (randomBehavior.Next(1, 21) == 1)
            {
                baseSpeed += randomDelay.Next(800, 2000);
                Console.WriteLine($"🧠 Spike delay: {baseSpeed}ms (phân tâm)");
            }
            
            Thread.Sleep(baseSpeed);
            
            // Có 3% khả năng dừng lại một chút (mô phỏng suy nghĩ)
            if (enablePause && randomBehavior.Next(1, 34) == 1)
            {
                Thread.Sleep(randomDelay.Next(300, 800));
                Console.WriteLine($"⏸️ Pause ngẫu nhiên");
            }
            
            // Có 3% khả năng sẽ backspace ngẫu nhiên (mô phỏng lỗi gõ phím)
            if (enableBackspace && randomBehavior.Next(1, 34) == 1 && currentPosition > 1)
            {
                // Backspace 1 ký tự
                element.SendKeys(OpenQA.Selenium.Keys.Backspace);
                currentPosition--;
                Thread.Sleep(randomDelay.Next(50, 120));
                
                Console.WriteLine($"⌫ Random backspace 1 ký tự, quay lại vị trí {currentPosition}");
            }
            
            // Kiểm tra định kỳ giá trị thực tế
            if (currentPosition % 5 == 0 || currentPosition == text.Length)
            {
                try
                {
                    string currentValue = element.GetAttribute("value");
                    string expectedValue = text.Substring(0, Math.Min(currentPosition, text.Length));
                    
                    // Nếu có sự khác biệt lớn, sửa lại
                    if (currentValue.Length < expectedValue.Length - 2 || currentValue.Length > expectedValue.Length + 2)
                    {
                        element.Clear();
                        Thread.Sleep(200);
                        element.SendKeys(expectedValue);
                        Thread.Sleep(200);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Không thể kiểm tra giá trị tại vị trí {currentPosition}: {ex.Message}");
                }
            }
        }
        
        // Thêm mouse behavior cuối cùng (giống người thật)
        try
        {
            // 30% chance move mouse ra khỏi field trước khi blur
            if (randomBehavior.Next(1, 4) == 1)
            {
                // Lấy driver bằng reflection để tránh phụ thuộc vào Internal.IWrapsDriver
                IWebDriver driver = null;
                try
                {
                    var prop = element.GetType().GetProperty("WrappedDriver", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    driver = prop?.GetValue(element) as IWebDriver;
                }
                catch { }
                if (driver == null)
                {
                    Console.WriteLine("ℹ️ Không lấy được WrappedDriver từ element, bỏ qua mouse move");
                }
                else
                {
                    var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                    
                    // Move mouse ra khỏi field
                    int offsetX = randomDelay.Next(-100, 101);
                    int offsetY = randomDelay.Next(-50, 51);
                    actions.MoveByOffset(offsetX, offsetY).Perform();
                    
                    Thread.Sleep(randomDelay.Next(200, 600));
                    Console.WriteLine($"🖱️ Mouse moved away from field");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Không thể move mouse: {ex.Message}");
        }
        
        // Final pause trước khi blur (giống người thật)
        Thread.Sleep(randomDelay.Next(500, 1200));
        
        // Kiểm tra và sửa lỗi cuối cùng
        try
        {
            string finalValue = element.GetAttribute("value");
            
            if (finalValue != text)
            {
                element.Clear();
                Thread.Sleep(200);
                element.SendKeys(text);
                Thread.Sleep(200);
                
                string correctedValue = element.GetAttribute("value");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Không thể kiểm tra giá trị cuối cùng: {ex.Message}");
        }
    }
    
    // Hàm tạo ký tự sai ngẫu nhiên
    static char GetRandomWrongChar(char correctChar)
    {
        Random random = new Random();
        
        // Nếu là chữ cái
        if (char.IsLetter(correctChar))
        {
            // Tạo chữ cái ngẫu nhiên cùng loại (hoa/thường)
            if (char.IsUpper(correctChar))
            {
                return (char)random.Next('A', 'Z' + 1);
            }
            else
            {
                return (char)random.Next('a', 'z' + 1);
            }
        }
        // Nếu là số
        else if (char.IsDigit(correctChar))
        {
            return (char)random.Next('0', '9' + 1);
        }
        // Nếu là ký tự đặc biệt
        else
        {
            char[] specialChars = { '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '_', '=', '+', '[', ']', '{', '}', '|', '\\', ';', ':', '"', '\'', ',', '.', '<', '>', '/', '?' };
            return specialChars[random.Next(specialChars.Length)];
        }
    }
    // Method sử dụng CDP để stealth
    static void ApplyCDPStealth(IWebDriver driver)
    {
        try
        {
            ChromeDriver chromeDriver = driver as ChromeDriver;
            if (chromeDriver == null)
            {
                Console.WriteLine("⚠️ Driver không phải ChromeDriver, bỏ qua CDP stealth");
                return;
            }
            
            // Sử dụng CDP để execute commands
            var executeCdpCommand = chromeDriver.GetType().GetMethod("ExecuteCdpCommand");
            if (executeCdpCommand == null)
            {
                Console.WriteLine("⚠️ ExecuteCdpCommand không khả dụng");
                return;
            }
            
            // Command 1: Set user agent override (quan trọng cho fingerprint)
            try
            {
                var userAgentParams = new Dictionary<string, object>
                {
                    { "userAgent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" },
                    { "platform", "Win32" },
                    { "userAgentMetadata", new Dictionary<string, object>
                        {
                            { "brands", new object[] 
                                {
                                    new Dictionary<string, object> { { "brand", "Not_A Brand" }, { "version", "8" } },
                                    new Dictionary<string, object> { { "brand", "Chromium" }, { "version", "120" } },
                                    new Dictionary<string, object> { { "brand", "Google Chrome" }, { "version", "120" } }
                                }
                            },
                            { "fullVersion", "120.0.6099.109" },
                            { "platform", "Windows" },
                            { "platformVersion", "10.0.0" },
                            { "architecture", "x86" },
                            { "model", "" },
                            { "mobile", false }
                        }
                    }
                };
                executeCdpCommand.Invoke(chromeDriver, new object[] { "Network.setUserAgentOverride", userAgentParams });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi set user agent via CDP: {ex.Message}");
            }
            
            // Command 2: Disable web security notifications
            try
            {
                executeCdpCommand.Invoke(chromeDriver, new object[] { "Page.setWebLifecycleState", new Dictionary<string, object> { { "state", "active" } } });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi set web lifecycle: {ex.Message}");
            }
            
            Console.WriteLine("✅ Đã áp dụng CDP stealth thành công");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi áp dụng CDP stealth: {ex.Message}");
        }
    }
    
    // Method inject JavaScript stealth scripts để tránh bị phát hiện automation
    static void InjectAntiDetectionScripts(IWebDriver driver)
    {
        try
        {
            // Áp dụng CDP stealth trước
            ApplyCDPStealth(driver);
            
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            
            // Script 1: Override navigator.webdriver
            string script1 = @"
                Object.defineProperty(navigator, 'webdriver', {
                    get: () => undefined
                });
            ";
            
            // Script 2: Override chrome runtime
            string script2 = @"
                window.navigator.chrome = {
                    runtime: {}
                };
            ";
            
            // Script 3: Override permissions
            string script3 = @"
                const originalQuery = window.navigator.permissions.query;
                window.navigator.permissions.query = (parameters) => (
                    parameters.name === 'notifications' ?
                        Promise.resolve({ state: Notification.permission }) :
                        originalQuery(parameters)
                );
            ";
            
            // Script 4: Override plugins length
            string script4 = @"
                Object.defineProperty(navigator, 'plugins', {
                    get: () => [1, 2, 3, 4, 5]
                });
            ";
            
            // Script 5: Override languages
            string script5 = @"
                Object.defineProperty(navigator, 'languages', {
                    get: () => ['en-US', 'en']
                });
            ";
            
            // Script 6: Override connection rtt (giảm khả năng phát hiện datacenter)
            string script6 = @"
                if (navigator.connection) {
                    Object.defineProperty(navigator.connection, 'rtt', {
                        get: () => " + _random.Next(50, 150) + @"
                    });
                }
            ";
            
            // Script 7: Override battery API
            string script7 = @"
                if (navigator.getBattery) {
                    navigator.getBattery = () => Promise.resolve({
                        charging: true,
                        chargingTime: 0,
                        dischargingTime: Infinity,
                        level: 1.0
                    });
                }
            ";
            
            // Script 8: Override automation-controlled feature
            string script8 = @"
                delete navigator.__proto__.webdriver;
            ";
            
            // Script 9: Override Chrome App and Load Times
            string script9 = @"
                window.chrome = {
                    app: {
                        isInstalled: false,
                        InstallState: {
                            DISABLED: 'disabled',
                            INSTALLED: 'installed',
                            NOT_INSTALLED: 'not_installed'
                        },
                        RunningState: {
                            CANNOT_RUN: 'cannot_run',
                            READY_TO_RUN: 'ready_to_run',
                            RUNNING: 'running'
                        }
                    },
                    runtime: {
                        OnInstalledReason: {
                            CHROME_UPDATE: 'chrome_update',
                            INSTALL: 'install',
                            SHARED_MODULE_UPDATE: 'shared_module_update',
                            UPDATE: 'update'
                        },
                        OnRestartRequiredReason: {
                            APP_UPDATE: 'app_update',
                            OS_UPDATE: 'os_update',
                            PERIODIC: 'periodic'
                        },
                        PlatformArch: {
                            ARM: 'arm',
                            ARM64: 'arm64',
                            MIPS: 'mips',
                            MIPS64: 'mips64',
                            X86_32: 'x86-32',
                            X86_64: 'x86-64'
                        },
                        PlatformNaclArch: {
                            ARM: 'arm',
                            MIPS: 'mips',
                            MIPS64: 'mips64',
                            X86_32: 'x86-32',
                            X86_64: 'x86-64'
                        },
                        PlatformOs: {
                            ANDROID: 'android',
                            CROS: 'cros',
                            LINUX: 'linux',
                            MAC: 'mac',
                            OPENBSD: 'openbsd',
                            WIN: 'win'
                        },
                        RequestUpdateCheckStatus: {
                            NO_UPDATE: 'no_update',
                            THROTTLED: 'throttled',
                            UPDATE_AVAILABLE: 'update_available'
                        }
                    }
                };
            ";
            
            // Script 10: Override toString() để ẩn native code
            string script10 = @"
                const overrideToString = (obj, name) => {
                    const handler = {
                        apply: function(target, ctx, args) {
                            return target.apply(ctx, args);
                        }
                    };
                    obj[name] = new Proxy(obj[name], handler);
                    obj[name].toString = function() { return 'function ' + name + '() { [native code] }'; };
                };
                
                if (navigator.permissions && navigator.permissions.query) {
                    overrideToString(navigator.permissions, 'query');
                }
            ";
            
            // Execute tất cả scripts
            js.ExecuteScript(script1);
            js.ExecuteScript(script2);
            js.ExecuteScript(script3);
            js.ExecuteScript(script4);
            js.ExecuteScript(script5);
            js.ExecuteScript(script6);
            js.ExecuteScript(script7);
            js.ExecuteScript(script8);
            js.ExecuteScript(script9);
            js.ExecuteScript(script10);
            
            Console.WriteLine("✅ Đã inject stealth scripts thành công");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi inject stealth scripts: {ex.Message}");
        }
    }
    
    static void HandleProxyAuthPopup(IWebDriver driver)
    {
        try
        {
            // Đợi một chút để popup có thể xuất hiện
            Thread.Sleep(2000);
            
            // Kiểm tra xem có popup authentication không
            var authDialogs = driver.FindElements(By.XPath("//div[contains(text(), 'Sign in') or contains(text(), 'Authentication')]"));
            if (authDialogs.Count > 0)
            {
                Console.WriteLine("🔐 Phát hiện proxy authentication popup, đang xử lý...");
                
                // Tìm username và password fields
                var usernameFields = driver.FindElements(By.XPath("//input[@type='text' or @name='username' or @id='username']"));
                var passwordFields = driver.FindElements(By.XPath("//input[@type='password' or @name='password' or @id='password']"));
                
                if (usernameFields.Count > 0 && passwordFields.Count > 0 && _currentProxy != null && _currentProxy.HasAuth)
                {
                    // Điền username
                    usernameFields[0].Clear();
                    HumanType(usernameFields[0], _currentProxy.Username);
                    Thread.Sleep(500);
                    
                    // Điền password
                    passwordFields[0].Clear();
                    HumanType(passwordFields[0], _currentProxy.Password);
                    Thread.Sleep(500);
                    
                    // Tìm và click nút Sign in
                    var signInButtons = driver.FindElements(By.XPath("//button[contains(text(), 'Sign in') or contains(text(), 'Login') or contains(text(), 'OK')]"));
                    if (signInButtons.Count > 0)
                    {
                        signInButtons[0].Click();
                        Console.WriteLine("✅ Đã xử lý proxy authentication popup");
                        Thread.Sleep(2000);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi xử lý proxy auth popup: {ex.Message}");
        }
    }
    
    // =====================================================
    // CẢI THIỆN HUMAN-LIKE BEHAVIOR ĐỂ TRÁNH VERIFY
    // =====================================================
    
    /// <summary>
    /// Kiểm tra và cải thiện network fingerprint
    /// </summary>
    static void ImproveNetworkFingerprint(IWebDriver driver)
    {
        try
        {
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            
            // Script 1: Override connection RTT để giống residential
            string networkScript = @"
                if (navigator.connection) {
                    Object.defineProperty(navigator.connection, 'rtt', {
                        get: () => " + _random.Next(50, 200) + @"
                    });
                    Object.defineProperty(navigator.connection, 'downlink', {
                        get: () => " + (_random.Next(1, 10) + 0.1).ToString("F1") + @"
                    });
                    Object.defineProperty(navigator.connection, 'effectiveType', {
                        get: () => '4g'
                    });
                }
            ";
            js.ExecuteScript(networkScript);
            
            // Script 2: Override battery để giống laptop
            string batteryScript = @"
                if (navigator.getBattery) {
                    navigator.getBattery = () => Promise.resolve({
                        charging: " + (_random.Next(0, 2) == 1 ? "true" : "false") + @",
                        chargingTime: " + _random.Next(0, 3600) + @",
                        dischargingTime: " + _random.Next(3600, 14400) + @",
                        level: " + (_random.Next(20, 100) / 100.0).ToString("F2") + @"
                    });
                }
            ";
            js.ExecuteScript(batteryScript);
            
            Console.WriteLine("✅ Đã cải thiện network fingerprint");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi cải thiện network fingerprint: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Thêm random browsing behavior để build profile history
    /// </summary>
    static void AddBrowsingBehavior(IWebDriver driver)
    {
        try
        {
            // 30% chance để thêm browsing behavior
            if (_random.Next(1, 4) == 1)
            {
                Console.WriteLine("🌐 Thêm browsing behavior để build profile...");
                
                // Random actions
                int action = _random.Next(1, 5);
                switch (action)
                {
                    case 1: // Scroll để "đọc" trang
                        int scrollAmount = _random.Next(200, 800);
                        ((IJavaScriptExecutor)driver).ExecuteScript($"window.scrollBy({{top: {scrollAmount}, left: 0, behavior: 'smooth'}});");
                        SmartDelay("read", 2000, 4000);
                        break;
                        
                    case 2: // Hover trên links
                        var links = driver.FindElements(By.TagName("a"));
                        if (links.Count > 0)
                        {
                            var randomLink = links[_random.Next(0, Math.Min(links.Count, 5))];
                            var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                            actions.MoveToElement(randomLink).Perform();
                            SmartDelay("read", 1000, 3000);
                        }
                        break;
                        
                    case 3: // "Đọc" nội dung (pause lâu)
                        SmartDelay("read", 5000, 10000);
                        break;
                        
                    case 4: // Random mouse movement
                        var actions2 = new OpenQA.Selenium.Interactions.Actions(driver);
                        int x = _random.Next(100, 1000);
                        int y = _random.Next(100, 700);
                        actions2.MoveByOffset(x, y).Perform();
                        SmartDelay("think", 1000, 3000);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi thêm browsing behavior: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Kiểm tra và cải thiện fingerprint consistency
    /// </summary>
    static void EnsureFingerprintConsistency(IWebDriver driver)
    {
        try
        {
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            
            // Script: Đảm bảo fingerprint nhất quán
            string consistencyScript = @"
                // Đảm bảo timezone khớp với IP location
                const timezone = Intl.DateTimeFormat().resolvedOptions().timeZone;
                console.log('Timezone:', timezone);
                
                // Đảm bảo language khớp với region
                const language = navigator.language;
                console.log('Language:', language);
                
                // Đảm bảo screen resolution hợp lý
                const screen = {
                    width: screen.width,
                    height: screen.height,
                    availWidth: screen.availWidth,
                    availHeight: screen.availHeight
                };
                console.log('Screen:', screen);
                
                // Đảm bảo WebGL vendor hợp lệ
                const canvas = document.createElement('canvas');
                const gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');
                if (gl) {
                    const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
                    if (debugInfo) {
                        const vendor = gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL);
                        const renderer = gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL);
                        console.log('WebGL Vendor:', vendor);
                        console.log('WebGL Renderer:', renderer);
                    }
                }
            ";
            js.ExecuteScript(consistencyScript);
            
            Console.WriteLine("✅ Đã kiểm tra fingerprint consistency");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi kiểm tra consistency: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Delay thông minh dựa trên loại action
    /// </summary>
    static void SmartDelay(string action, int minMs = 1000, int maxMs = 3000)
    {
        // Delay dựa trên loại action
        int baseDelay = 0;
        switch (action.ToLower())
        {
            case "click":
                baseDelay = _random.Next(500, 1500); // Click nhanh hơn
                break;
            case "type":
                baseDelay = _random.Next(800, 1500); // Typing cần thời gian
                break;
            case "navigate":
                baseDelay = _random.Next(2000, 4000); // Navigation cần thời gian load
                break;
            case "think":
                baseDelay = _random.Next(3000, 6000); // "Suy nghĩ" lâu hơn
                break;
            case "read":
                baseDelay = _random.Next(2000, 5000); // Đọc nội dung
                break;
            default:
                baseDelay = _random.Next(minMs, maxMs);
                break;
        }
        
        // Thêm random variation
        int finalDelay = baseDelay + _random.Next(-500, 1000);
        finalDelay = Math.Max(200, finalDelay); // Tối thiểu 200ms
        
        Console.WriteLine($"⏰ {action} delay: {finalDelay}ms");
        Thread.Sleep(finalDelay);
    }
    
    // =====================================================
    // CẢI THIỆN PROFILE TRUST SCORE ĐỂ TRÁNH VERIFY
    // =====================================================
    
    /// <summary>
    /// Build profile history để tăng trust score
    /// </summary>
    static void BuildProfileHistory(IWebDriver driver)
    {
        try
        {
            Console.WriteLine("🏗️ Building profile history...");
            
            // 1. Visit Google.com trước
            driver.Navigate().GoToUrl("https://www.google.com");
            SmartDelay("navigate", 3000, 6000);
            
            // 2. Search một vài từ
            var searchBox = driver.FindElement(By.Name("q"));
            string[] searchTerms = { "weather", "news", "sports", "technology" };
            string searchTerm = searchTerms[_random.Next(searchTerms.Length)];
            
            HumanTypeAdvanced(searchBox, searchTerm);
            SmartDelay("think", 2000, 4000);
            
            // 3. Click search
            var searchButton = driver.FindElement(By.Name("btnK"));
            searchButton.Click();
            SmartDelay("navigate", 3000, 6000);
            
            // 4. Click vào một vài links
            var links = driver.FindElements(By.CssSelector("h3"));
            if (links.Count > 0)
            {
                var randomLink = links[_random.Next(0, Math.Min(links.Count, 3))];
                randomLink.Click();
                SmartDelay("read", 5000, 10000);
                
                // Scroll để "đọc"
                ((IJavaScriptExecutor)driver).ExecuteScript("window.scrollBy(0, 500);");
                SmartDelay("read", 3000, 6000);
            }
            
            // 5. Quay lại Google
            driver.Navigate().Back();
            SmartDelay("navigate", 2000, 4000);
            
            Console.WriteLine("✅ Profile history built successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi build profile history: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Cải thiện device fingerprint để tránh detection
    /// </summary>
    static void ImproveDeviceFingerprint(IWebDriver driver)
    {
        try
        {
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            
            // Script 1: Override screen properties
            string screenScript = @"
                Object.defineProperty(screen, 'width', { get: () => 1920 });
                Object.defineProperty(screen, 'height', { get: () => 1080 });
                Object.defineProperty(screen, 'availWidth', { get: () => 1920 });
                Object.defineProperty(screen, 'availHeight', { get: () => 1040 });
                Object.defineProperty(screen, 'colorDepth', { get: () => 24 });
                Object.defineProperty(screen, 'pixelDepth', { get: () => 24 });
            ";
            js.ExecuteScript(screenScript);
            
            // Script 2: Override timezone
            string timezoneScript = @"
                Object.defineProperty(Intl.DateTimeFormat.prototype, 'resolvedOptions', {
                    value: function() { return { timeZone: 'America/New_York' }; }
                });
            ";
            js.ExecuteScript(timezoneScript);
            
            // Script 3: Override language
            string languageScript = @"
                Object.defineProperty(navigator, 'language', { get: () => 'en-US' });
                Object.defineProperty(navigator, 'languages', { get: () => ['en-US', 'en'] });
            ";
            js.ExecuteScript(languageScript);
            
            Console.WriteLine("✅ Device fingerprint improved");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi improve device fingerprint: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Thêm random browsing behavior để build trust
    /// </summary>
    static void AddTrustBuildingBehavior(IWebDriver driver)
    {
        try
        {
            // 40% chance thêm trust building behavior
            if (_random.Next(1, 6) <= 2)
            {
                Console.WriteLine("🤝 Adding trust building behavior...");
                
                // Random actions để build trust
                int action = _random.Next(1, 6);
                switch (action)
                {
                    case 1: // Scroll để "đọc" trang
                        int scrollAmount = _random.Next(300, 800);
                        ((IJavaScriptExecutor)driver).ExecuteScript($"window.scrollBy({{top: {scrollAmount}, left: 0, behavior: 'smooth'}});");
                        SmartDelay("read", 3000, 6000);
                        break;
                        
                    case 2: // Hover trên elements
                        var elements = driver.FindElements(By.TagName("button"));
                        if (elements.Count > 0)
                        {
                            var randomElement = elements[_random.Next(0, Math.Min(elements.Count, 3))];
                            var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                            actions.MoveToElement(randomElement).Perform();
                            SmartDelay("read", 2000, 4000);
                        }
                        break;
                        
                    case 3: // "Đọc" nội dung lâu
                        SmartDelay("read", 8000, 15000);
                        break;
                        
                    case 4: // Random mouse movement
                        var actions2 = new OpenQA.Selenium.Interactions.Actions(driver);
                        int x = _random.Next(200, 1000);
                        int y = _random.Next(200, 700);
                        actions2.MoveByOffset(x, y).Perform();
                        SmartDelay("think", 2000, 4000);
                        break;
                        
                    case 5: // Focus vào field khác
                        var inputs = driver.FindElements(By.TagName("input"));
                        if (inputs.Count > 1)
                        {
                            var randomInput = inputs[_random.Next(0, Math.Min(inputs.Count, 3))];
                            randomInput.Click();
                            SmartDelay("think", 1000, 3000);
                        }
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi add trust building behavior: {ex.Message}");
        }
    }

    // =====================================================
    // CHỨC NĂNG MỚI: TẠO NHIỀU GMAIL VỚI PROFILE ROTATION
    // =====================================================
    
    /// <summary>
    /// Tạo nhiều Gmail tự động với profile rotation để tránh phát hiện
    /// </summary>
    static async Task CreateMultipleGmailsWithRotation()
    {
        Console.WriteLine("\n🎯 CHẾ ĐỘ TẠO NHIỀU GMAIL VỚI PROFILE ROTATION");
        Console.WriteLine("═══════════════════════════════════════════════════");
        
        // Hiển thị profiles hiện có
        AdvancedChromeConfig.ShowAllProfilesInfo();
        
        // Hỏi số lượng Gmail muốn tạo
        Console.Write("\n📊 Nhập số lượng Gmail muốn tạo: ");
        if (!int.TryParse(Console.ReadLine(), out int totalGmails) || totalGmails <= 0)
        {
            Console.WriteLine("❌ Số lượng không hợp lệ!");
            return;
        }
        
        // Hiển thị khuyến nghị
        AdvancedChromeConfig.ShowScalingRecommendation(totalGmails);
        
        // Xác nhận
        Console.Write($"\n⚠️ Bạn có chắc muốn tạo {totalGmails} Gmail với profile rotation? (y/n): ");
        if (Console.ReadLine()?.ToLower() != "y")
        {
            Console.WriteLine("❌ Đã hủy!");
            return;
        }
        
        // Hỏi về Gmail sync
        Console.Write("\nBạn có muốn đăng nhập và đồng bộ Gmail sau khi tạo? (y/n): ");
        bool enableSync = Console.ReadLine()?.ToLower().StartsWith("y") == true;
        
        // Hỏi về spacing (phút)
        Console.Write("\nNhập spacing giữa mỗi lần tạo (phút, khuyến nghị 30): ");
        if (!int.TryParse(Console.ReadLine(), out int spacingMinutes) || spacingMinutes < 5)
        {
            spacingMinutes = 30; // Mặc định 30 phút
        }
        
        int spacingMilliseconds = spacingMinutes * 60 * 1000;
        
        Console.WriteLine($"\n✅ Bắt đầu tạo {totalGmails} Gmail với spacing {spacingMinutes} phút");
        Console.WriteLine("═══════════════════════════════════════════════════\n");
        
        int successCount = 0;
        int failCount = 0;
        
        for (int i = 0; i < totalGmails; i++)
        {
            // Rotation profile trước mỗi lần tạo
            string currentProfile = AdvancedChromeConfig.RotateToNextProfile();
            
            Console.WriteLine($"\n{new string('=', 50)}");
            Console.WriteLine($"🔄 TẠO GMAIL {i + 1}/{totalGmails}");
            Console.WriteLine($"📁 Profile: {currentProfile}");
            Console.WriteLine($"{new string('=', 50)}\n");
            
            try
            {
                // Tạo Gmail (tương tự logic ở chế độ 1)
                // TODO: Extract logic tạo Gmail thành một method riêng để tái sử dụng
                Console.WriteLine($"⚠️ Chức năng này đang được phát triển!");
                Console.WriteLine($"   Hiện tại hãy sử dụng chế độ 1 với manual profile selection");
                Console.WriteLine($"   Hoặc chạy script nhiều lần và thay đổi profile manually");
                
                // Placeholder - bạn cần extract logic từ Main() vào đây
                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi tạo Gmail {i + 1}: {ex.Message}");
                failCount++;
            }
            
            // Spacing giữa các lần tạo (trừ lần cuối)
            if (i < totalGmails - 1)
            {
                Console.WriteLine($"\n⏰ Chờ {spacingMinutes} phút trước khi tạo Gmail tiếp theo...");
                Console.WriteLine($"   (Có thể nhấn Ctrl+C để dừng)");
                Thread.Sleep(spacingMilliseconds);
            }
        }
        
        // Tổng kết
        Console.WriteLine("\n═══════════════════════════════════════════════════");
        Console.WriteLine("📊 KẾT QUẢ TỔNG KẾT:");
        Console.WriteLine($"   ✅ Thành công: {successCount}/{totalGmails}");
        Console.WriteLine($"   ❌ Thất bại: {failCount}/{totalGmails}");
        Console.WriteLine($"   📈 Tỷ lệ thành công: {(successCount * 100.0 / totalGmails):F1}%");
        Console.WriteLine("═══════════════════════════════════════════════════\n");
    }
}
