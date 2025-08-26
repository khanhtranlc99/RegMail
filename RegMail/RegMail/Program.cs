using ClosedXML.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
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
        
        
        // Hiển thị menu chọn chế độ proxy
        await ShowProxyMenu();

        // Hiển thị menu chế độ hoạt động
        Console.WriteLine("\n🎯 Chọn chế độ hoạt động:");
        Console.WriteLine("1. Tạo tài khoản Gmail mới");
        Console.WriteLine("2. Đăng nhập với email đã có (sử dụng persistent fingerprint)");
        Console.WriteLine("4. Test tính nhất quán của persistent fingerprint");
        Console.WriteLine("6. 📁 Quản lý Chrome Profiles");
        Console.Write("Lựa chọn của bạn (1-6): ");
        
        string choice = Console.ReadLine();
        
        
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
            ProxyManager.ProxySpec p = null;
            try
            {
                p = ProxyManager.PickRandomWorkingHttpFromFileAsync(path, 8, 20).GetAwaiter().GetResult();
                Console.WriteLine("🌐 Chọn được HTTP proxy còn sống: " + p);
            }
            catch (Exception e)
            {
                Console.WriteLine("⚠️ Không tìm được HTTP proxy sống: " + e.Message);
                var all = ProxyManager.LoadProxiesFromFile(path);
                if (all.Count == 0) throw;
                p = ProxyManager.PickRandom(all);
                Console.WriteLine("↩️ Fallback chọn ngẫu nhiên: " + p);
            }
            // Biến theo dõi trang hiện tại (1 = trang đầu tiên)
            int currentPage = 1;
            
            
            ChromeOptions options = new ChromeOptions();
            
            // CẤU HÌNH CHROME ANTI-DETECTION NÂNG CAO
            AdvancedChromeConfig.ConfigureAdvancedChromeOptions(options, width, height, posX, posY);

            // Tạo account identifier deterministic cho persistent fingerprint
            // Sử dụng tab index và timestamp để tạo account unique identifier
            string accountIdentifier = $"gmail_tab_{i}_{DateTime.Now:yyyyMMdd_HHmm}";
            string userDataDir = AdvancedChromeConfig.CreateUniqueUserDataDirectory();
            Console.WriteLine($"📁 UserDataDir cho tab {i + 1}: {userDataDir}");

            if (ConfigManager.Chrome_Use_Minimal_Flags)
            {
                options = ChromeOptionsManager.CreateMinimalOptions(userDataDir);
                Console.WriteLine("ℹ️ Sử dụng cấu hình Chrome tối thiểu");
            }
            else
            {
                options = ChromeOptionsManager.CreateAdvancedOptions(userDataDir);
                Console.WriteLine("ℹ️ Sử dụng cấu hình Chrome nâng cao");
            }
            
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
                options = ChromeOptionsManager.CreateMinimalOptions(userDataDir);
                driver = new ChromeDriver(options);
            }
            ProxyManager.ApplyToChrome(options, p, false);
            driver.Navigate().GoToUrl(signupUrl);

            Thread.Sleep(5000);


            // Inject JavaScript để thay đổi fingerprint và tránh phát hiện automation
            //InjectAntiDetectionScripts(driver);
            
            // Thêm xử lý lỗi tổng thể cho quá trình tạo Gmail
            try
            {

            string firstName = FillFirstName(driver);
            string lastName = FillLastName(driver);
            ClickNextButton(driver, currentPage++);
            FillDayAndYearNew(driver);
            FillMonthNew(driver);
            FillGenderNew(driver);
            ClickNextButton(driver, currentPage++);
            HumanLikeActions(driver);
            ClickNextButton(driver, currentPage++);
            RandomDelay();
            
            // Kiểm tra xem có cần click "Create your own Gmail address" hay không
            TryClickCreateOwnGmail(driver);
            RandomDelay();

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

    // Hàm click tự nhiên với fallback JavaScript - ưu tiên click tự nhiên
    static bool HumanLikeClick(IWebDriver driver, IWebElement element, string elementDescription = "element")
    {
        try
        {
            // Cách 1: Click tự nhiên trước (tạo ra đầy đủ mouse events)
            element.Click();
            Console.WriteLine($"✅ Đã click tự nhiên vào {elementDescription}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Click tự nhiên thất bại cho {elementDescription}: {ex.Message}");
            
            // Cách 2: Fallback JavaScript click chỉ khi thật sự cần thiết
            try
            {
                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                js.ExecuteScript("arguments[0].click();", element);
                Console.WriteLine($"✅ Đã fallback JavaScript click cho {elementDescription}");
                return true;
            }
            catch (Exception jsEx)
            {
                Console.WriteLine($"❌ JavaScript click cũng thất bại cho {elementDescription}: {jsEx.Message}");
                
                // Cách 3: Fallback Actions click
                try
                {
                    var actions = new Actions(driver);
                    actions.MoveToElement(element).Click().Perform();
                    Console.WriteLine($"✅ Đã fallback Actions click cho {elementDescription}");
                    return true;
                }
                catch (Exception actionsEx)
                {
                    Console.WriteLine($"❌ Actions click cũng thất bại cho {elementDescription}: {actionsEx.Message}");
                    return false;
                }
            }
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

    // Hàm đợi URL thay đổi thông minh
    static bool WaitForUrlChange(IWebDriver driver, string currentUrl, int timeoutSeconds = 15)
    {
        try
        {
            var urlChanged = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds))
                .Until(d => !d.Url.Equals(currentUrl));
            return true;
        }
        catch (WebDriverTimeoutException)
        {
            return false;
        }
    }

    // Hàm debug để phân tích page structure khi gặp lỗi selector
    static void DebugPageStructure(IWebDriver driver, string context = "")
    {
        try
        {
            Console.WriteLine($"\n🔍 DEBUG PAGE STRUCTURE - {context}");
            Console.WriteLine($"📍 Current URL: {driver.Url}");
            Console.WriteLine($"📄 Page Title: {driver.Title}");
            
            // Tìm tất cả input fields
            var inputs = driver.FindElements(By.TagName("input"));
            Console.WriteLine($"\n📝 Found {inputs.Count} input elements:");
            
            for (int i = 0; i < Math.Min(inputs.Count, 15); i++) // Limit để không spam
            {
                try
                {
                    var input = inputs[i];
                    var type = input.GetAttribute("type") ?? "";
                    var name = input.GetAttribute("name") ?? "";
                    var id = input.GetAttribute("id") ?? "";
                    var ariaLabel = input.GetAttribute("aria-label") ?? "";
                    var placeholder = input.GetAttribute("placeholder") ?? "";
                    var className = input.GetAttribute("class") ?? "";
                    
                    Console.WriteLine($"  [{i+1}] type='{type}' name='{name}' id='{id}' aria-label='{ariaLabel}' placeholder='{placeholder}' class='{className.Substring(0, Math.Min(className.Length, 50))}'");
                }
                catch { }
            }
            
            // Tìm tất cả dropdown elements
            var dropdowns = driver.FindElements(By.XPath("//select | //span[contains(@role, 'button')] | //div[contains(@role, 'button')] | //span[contains(text(), 'Month')] | //span[contains(text(), 'Gender')]"));
            Console.WriteLine($"\n📋 Found {dropdowns.Count} potential dropdown elements:");
            
            for (int i = 0; i < Math.Min(dropdowns.Count, 10); i++)
            {
                try
                {
                    var dropdown = dropdowns[i];
                    var tagName = dropdown.TagName;
                    var text = dropdown.Text?.Trim().Substring(0, Math.Min(dropdown.Text?.Trim().Length ?? 0, 30)) ?? "";
                    var role = dropdown.GetAttribute("role") ?? "";
                    var ariaLabel = dropdown.GetAttribute("aria-label") ?? "";
                    var className = dropdown.GetAttribute("class") ?? "";
                    
                    Console.WriteLine($"  [{i+1}] <{tagName}> text='{text}' role='{role}' aria-label='{ariaLabel}' class='{className.Substring(0, Math.Min(className.Length, 40))}'");
                }
                catch { }
            }
            
            Console.WriteLine("🔍 End Debug\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Debug page structure error: {ex.Message}");
        }
    }

    // Hàm đợi element biến mất (cho loading indicators)
    static bool WaitForElementToDisappear(IWebDriver driver, By locator, int timeoutSeconds = 10)
    {
        try
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            wait.Until(d => d.FindElements(locator).Count == 0);
            return true;
        }
        catch (WebDriverTimeoutException)
        {
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
        string url = $"{ConfigManager.DailyOTP_RentNumber_URL}?appBrand=Google / Gmail / Youtube&countryCode=US&serverName=Server 1&api_key={ConfigManager.DailyOTP_API_Key}";

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
        string url = $"{ConfigManager.DailyOTP_GetMessages_URL}?transId={transId}&api_key={ConfigManager.DailyOTP_API_Key}";
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

    // Menu quản lý proxy và fingerprint
    static async Task ShowProxyMenu()
    {
        while (true)
        {
            Console.WriteLine("\n=== MENU QUẢN LÝ PROXY & FINGERPRINT ===");
            
            Console.WriteLine("5. Tải lại và test tất cả proxy");
            Console.WriteLine("6. Chọn proxy để sử dụng");
            Console.WriteLine("7. Xóa dữ liệu Chrome (xóa fingerprint cũ)");
            Console.WriteLine("8. Tạo fingerprint mới và test");
            Console.WriteLine("9. Xóa tất cả Chrome profiles đã lưu");
            Console.WriteLine("11. Bắt đầu tạo tài khoản Gmail");
            Console.WriteLine("0. Thoát");
            Console.Write("Chọn tùy chọn: ");

            string choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                
                
                case "7":
                    ClearChromeData();
                    break;
                case "8":
                    TestNewFingerprint();
                    break;
                case "9":
                    ClearAllChromeProfiles();
                    break;
                case "11":
                    return; // Thoát menu và tiếp tục chương trình
                case "0":
                    Environment.Exit(0);
                    break;
                default:
                    Console.WriteLine("❌ Lựa chọn không hợp lệ!");
                    break;
            }
        }
    }

    static void ClearChromeData()
    {
        Console.WriteLine("🗑️ Bắt đầu xóa dữ liệu Chrome...");
        Console.WriteLine("⚠️ Điều này sẽ xóa tất cả profile, cache và fingerprint cũ!");
        Console.Write("Bạn có chắc chắn muốn tiếp tục? (y/n): ");
        
        if (Console.ReadLine()?.ToLower().StartsWith("y") == true)
        {
            FingerprintManager.ClearChromeData();
            Console.WriteLine("✅ Đã xóa dữ liệu Chrome thành công!");
            Console.WriteLine("🔄 Bây giờ bạn có thể tạo fingerprint mới hoàn toàn");
        }
        else
        {
            Console.WriteLine("❌ Đã hủy xóa dữ liệu Chrome");
        }
    }

    static void TestNewFingerprint()
    {
        Console.WriteLine("🧪 Bắt đầu test fingerprint mới...");
        
        try
        {
            // Hiển thị danh sách profile có sẵn
            FingerprintManager.ShowAvailableProfiles();
            
            Console.WriteLine("\n🎯 Chọn loại fingerprint để test:");
            Console.WriteLine("1. Windows Desktop");
            Console.WriteLine("2. Mac Desktop");
            Console.WriteLine("3. Linux Desktop");
            Console.WriteLine("4. Android Mobile");
            Console.WriteLine("5. iOS Mobile");
            Console.WriteLine("6. European Desktop");
            Console.WriteLine("7. Asian Desktop");
            Console.WriteLine("8. Gaming Desktop");
            Console.WriteLine("9. Business Desktop");
            Console.WriteLine("10. Student Laptop");
            Console.WriteLine("11. Random Profile (từ danh sách)");
            Console.WriteLine("12. Random Generated (hoàn toàn ngẫu nhiên)");
            
            Console.Write("\n📝 Nhập lựa chọn (1-12): ");
            string choice = Console.ReadLine();
            
            FingerprintInfo fingerprint = null;
            
            switch (choice)
            {
                case "1":
                    fingerprint = FingerprintManager.GetWindowsProfile();
                    break;
                case "2":
                    fingerprint = FingerprintManager.GetMacProfile();
                    break;
                case "3":
                    fingerprint = FingerprintManager.GetLinuxProfile();
                    break;
                case "4":
                    fingerprint = FingerprintManager.GetAndroidProfile();
                    break;
                case "5":
                    fingerprint = FingerprintManager.GetIOSProfile();
                    break;
                case "6":
                    fingerprint = FingerprintManager.GetEuropeanProfile();
                    break;
                case "7":
                    fingerprint = FingerprintManager.GetAsianProfile();
                    break;
                case "8":
                    fingerprint = FingerprintManager.GetGamingProfile();
                    break;
                case "9":
                    fingerprint = FingerprintManager.GetBusinessProfile();
                    break;
                case "10":
                    fingerprint = FingerprintManager.GetStudentProfile();
                    break;
                case "11":
                    fingerprint = FingerprintManager.GetRandomProfile();
                    break;
                case "12":
                    fingerprint = FingerprintManager.GenerateRandomFingerprint();
                    break;
                default:
                    Console.WriteLine("❌ Lựa chọn không hợp lệ! Sử dụng fingerprint ngẫu nhiên.");
                    fingerprint = FingerprintManager.GetRandomProfile();
                    break;
            }
            
            Console.WriteLine($"\n📱 Fingerprint được chọn: {fingerprint.ProfileName}");
            Console.WriteLine($"🌐 User Agent: {fingerprint.UserAgent}");
            Console.WriteLine($"🌍 Ngôn ngữ: {fingerprint.Language}");
            Console.WriteLine($"🖥️ Platform: {fingerprint.Platform}");
            Console.WriteLine($"📺 Độ phân giải: {fingerprint.ScreenResolution}");
            Console.WriteLine($"⏰ Timezone: {fingerprint.Timezone}");
            Console.WriteLine($"💾 Memory: {fingerprint.DeviceMemory}GB");
            Console.WriteLine($"🔧 CPU Cores: {fingerprint.HardwareConcurrency}");
            Console.WriteLine($"🎮 GPU: {fingerprint.WebGLRenderer}");
            
            // Tạo Chrome options với fingerprint mới
            ChromeOptions options = new ChromeOptions();
            FingerprintManager.ConfigureChromeOptions(options, fingerprint);
            
            Console.WriteLine("\n✅ Đã tạo fingerprint mới thành công!");
            Console.WriteLine("🔍 Bạn có thể test bằng cách mở Chrome với fingerprint này");
            Console.WriteLine("💡 Tip: Sử dụng fingerprint này trong automation để tránh bị phát hiện!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi khi tạo fingerprint mới: {ex.Message}");
        }
    }

    // Hàm xóa tất cả Chrome profiles đã lưu
    static void ClearAllChromeProfiles()
    {
        try
        {
            Console.WriteLine("🗑️ Bắt đầu xóa tất cả Chrome profiles đã lưu...");
            
            string userDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "Google", "Chrome", "User Data");
            
            if (!Directory.Exists(userDataPath))
            {
                Console.WriteLine("📁 Không tìm thấy thư mục Chrome User Data");
                return;
            }
            
            var regMailProfiles = Directory.GetDirectories(userDataPath)
                .Where(dir => Path.GetFileName(dir).StartsWith("RegMail_Profile_"))
                .ToArray();
            
            if (regMailProfiles.Length == 0)
            {
                Console.WriteLine("📝 Không có profile RegMail nào để xóa");
                return;
            }
            
            Console.WriteLine($"🔍 Tìm thấy {regMailProfiles.Length} profile RegMail:");
            foreach (var profile in regMailProfiles)
            {
                Console.WriteLine($"   - {Path.GetFileName(profile)}");
            }
            
            Console.Write("\nBạn có chắc chắn muốn xóa tất cả? (y/n): ");
            if (Console.ReadLine()?.ToLower().StartsWith("y") == true)
            {
                int deletedCount = 0;
                foreach (var profile in regMailProfiles)
                {
                    try
                    {
                        Directory.Delete(profile, true);
                        deletedCount++;
                        Console.WriteLine($"✅ Đã xóa: {Path.GetFileName(profile)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Không thể xóa {Path.GetFileName(profile)}: {ex.Message}");
                    }
                }
                Console.WriteLine($"🎉 Đã xóa thành công {deletedCount}/{regMailProfiles.Length} profile!");
            }
            else
            {
                Console.WriteLine("❌ Đã hủy xóa profiles");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi khi xóa Chrome profiles: {ex.Message}");
        }
    }



    static void InjectAntiDetectionScripts(IWebDriver driver)
    {
        try
        {
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

            // 1. Ẩn webdriver property
            js.ExecuteScript(@"
                Object.defineProperty(navigator, 'webdriver', {
                    get: () => undefined,
                });
            ");

            // 2. Thay đổi user agent
            js.ExecuteScript(@"
                Object.defineProperty(navigator, 'userAgent', {
                    get: () => 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
                });
            ");

            // 3. Thay đổi platform
            js.ExecuteScript(@"
                Object.defineProperty(navigator, 'platform', {
                    get: () => 'Win32',
                });
            ");

            // 4. Thay đổi language
            js.ExecuteScript(@"
                Object.defineProperty(navigator, 'language', {
                    get: () => 'en-US',
                });
            ");

            // 5. Thay đổi languages array
            js.ExecuteScript(@"
                Object.defineProperty(navigator, 'languages', {
                    get: () => ['en-US', 'en'],
                });
            ");

            // 6. Ẩn automation properties
            js.ExecuteScript(@"
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Array;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Promise;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Symbol;
            ");

            // 7. Thay đổi permissions
            js.ExecuteScript(@"
                const originalQuery = window.navigator.permissions.query;
                window.navigator.permissions.query = (parameters) => (
                    parameters.name === 'notifications' ?
                        Promise.resolve({ state: Notification.permission }) :
                        originalQuery(parameters)
                );
            ");

            // 8. Thay đổi plugins
            js.ExecuteScript(@"
                Object.defineProperty(navigator, 'plugins', {
                    get: () => [1, 2, 3, 4, 5],
                });
            ");

            // 9. Thay đổi mimeTypes
            js.ExecuteScript(@"
                Object.defineProperty(navigator, 'mimeTypes', {
                    get: () => [1, 2, 3, 4, 5],
                });
            ");

            // 10. Thay đổi hardwareConcurrency
            js.ExecuteScript(@"
                Object.defineProperty(navigator, 'hardwareConcurrency', {
                    get: () => 8,
                });
            ");

            // 11. Thay đổi deviceMemory
            js.ExecuteScript(@"
                Object.defineProperty(navigator, 'deviceMemory', {
                    get: () => 8,
                });
            ");

            // 12. Thay đổi connection
            js.ExecuteScript(@"
                Object.defineProperty(navigator, 'connection', {
                    get: () => ({
                        effectiveType: '4g',
                        rtt: 50,
                        downlink: 10,
                        saveData: false
                    }),
                });
            ");

            // 13. Thay đổi chrome object
            js.ExecuteScript(@"
                window.chrome = {
                    runtime: {},
                };
            ");

            // 14. Thay đổi permissions API
            js.ExecuteScript(@"
                const originalQuery = window.navigator.permissions.query;
                return window.navigator.permissions.query = (parameters) => (
                    parameters.name === 'notifications' ?
                        Promise.resolve({ state: Notification.permission }) :
                        originalQuery(parameters)
                );
            ");

            Console.WriteLine("✅ Đã inject thành công các script chống phát hiện automation");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi inject anti-detection scripts: {ex.Message}");
        }
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
        Random randomDelay = new Random();
        Random randomBehavior = new Random();
        
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
            
            // Nhập ký tự đúng
            element.SendKeys(currentChar.ToString());
            currentPosition++;
            Thread.Sleep(randomDelay.Next(80, 180));
            
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
}
