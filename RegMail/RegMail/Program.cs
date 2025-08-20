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
    
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        // Khởi tạo Proxy Manager
        _proxyManager = new ProxyManager();
        
        // Hiển thị menu chọn chế độ proxy
        await ShowProxyMenu();

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
        string signupUrl = "https://accounts.google.com/signup";

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
            
            // Dọn dẹp các thư mục user data cũ và kill process Chrome cũ
            AdvancedChromeConfig.CleanupOldUserDataDirectories();
            AdvancedChromeConfig.KillOldChromeProcesses();
            
            // CẤU HÌNH CHROME ANTI-DETECTION NÂNG CAO
            AdvancedChromeConfig.ConfigureAdvancedChromeOptions(options, width, height, posX, posY);

            // Tạo fingerprint HOÀN TOÀN ĐỘC NHẤT cho mỗi tab (QUAN TRỌNG)
            var fingerprint = FingerprintManager.GetRandomProfile();
            FingerprintManager.ConfigureChromeOptions(options, fingerprint);
            
            // Khởi tạo ChromeDriver với xử lý lỗi
            IWebDriver driver = null;
            try
            {
                driver = new ChromeDriver(options);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("session not created"))
            {
                Console.WriteLine("⚠️ Lỗi session Chrome, thử lại với cấu hình khác...");
                // Thử lại với cấu hình đơn giản hơn
                options = new ChromeOptions();
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-dev-shm-usage");
                options.AddArgument("--disable-gpu");
                options.AddArgument("--remote-debugging-port=0");
                string fallbackUserDataDir = AdvancedChromeConfig.CreateUniqueUserDataDirectory();
                options.AddArgument($"--user-data-dir={fallbackUserDataDir}");
                
                driver = new ChromeDriver(options);
            }
            driver.Navigate().GoToUrl(signupUrl);

            Thread.Sleep(5000);


            // Inject JavaScript để thay đổi fingerprint và tránh phát hiện automation
            InjectAntiDetectionScripts(driver);
            
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
            driver.Navigate().GoToUrl("https://mail.google.com");
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
            driver.Navigate().GoToUrl("https://drive.google.com");
            Thread.Sleep(2000);
            Console.WriteLine("✅ Đã kích hoạt Google Drive");
            
            // Kích hoạt Google Photos
            driver.Navigate().GoToUrl("https://photos.google.com");
            Thread.Sleep(2000);
            Console.WriteLine("✅ Đã kích hoạt Google Photos");
            
            // Kích hoạt YouTube
            driver.Navigate().GoToUrl("https://youtube.com");
            Thread.Sleep(2000);
            Console.WriteLine("✅ Đã kích hoạt YouTube");
            
            // Quay lại Gmail để đảm bảo hoạt động bình thường
            driver.Navigate().GoToUrl("https://mail.google.com");
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
    
    // Hàm bật Chrome Sync (nếu có thể)
    static void EnableChromeSync(IWebDriver driver)
    {
        try
        {
            Console.WriteLine("🔄 Đang cố gắng bật Chrome Sync...");
            
            // Truy cập trang settings Chrome
            driver.Navigate().GoToUrl("chrome://settings/syncSetup");
            Thread.Sleep(3000);
            
            // Tìm và click nút "Turn on sync" hoặc "Yes, I'm in"
            try
            {
                var syncButtons = driver.FindElements(By.XPath("//button[contains(text(), 'Turn on') or contains(text(), 'Yes') or contains(text(), 'Enable')]"));
                if (syncButtons.Count > 0)
                {
                    IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                    js.ExecuteScript("arguments[0].click();", syncButtons[0]);
                    Thread.Sleep(2000);
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

    // Hàm nhập từng ký tự một với delay ngẫu nhiên và backspace ngẫu nhiên
    static void HumanType(IWebElement element, string text)
    {
        Random randomDelay = new Random();
        Random randomBackspace = new Random();
        
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            element.SendKeys(c.ToString());
            Thread.Sleep(randomDelay.Next(80, 180));
            
            // Có 5% khả năng sẽ backspace ngẫu nhiên (mô phỏng lỗi gõ phím)
            if (randomBackspace.Next(1, 21) == 1) // 5% chance
            {
                // Backspace 1-2 ký tự
                int backspaceCount = randomBackspace.Next(1, 3);
                for (int j = 0; j < backspaceCount; j++)
                {
                    element.SendKeys(OpenQA.Selenium.Keys.Backspace);
                    Thread.Sleep(randomDelay.Next(50, 120));
                }
                
                // Nhập lại các ký tự đã bị xóa
                for (int j = 0; j < backspaceCount; j++)
                {
                    int index = Math.Max(0, i - backspaceCount + j + 1);
                    if (index < text.Length)
                    {
                        element.SendKeys(text[index].ToString());
                        Thread.Sleep(randomDelay.Next(60, 150));
                    }
                }
                
                // Nhập lại ký tự hiện tại
                element.SendKeys(c.ToString());
                Thread.Sleep(randomDelay.Next(80, 180));
            }
        }
    }

    static string FillFirstName(IWebDriver driver)
    {
        string[] firstNames = { "Acacia", "Adela", "Blanche", "Bridget", "Donna", "Mayya", "Luccy" };
        Random random = new Random();
        string randomFirstName = firstNames[random.Next(firstNames.Length)];

        IWebElement firstNameField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='First name']")));
        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
        js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", firstNameField);
        RandomDelay(200, 400);
        firstNameField.Click();
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
                
                Console.WriteLine($"⌨️ Bắt đầu nhập username: '{username}'");
                HumanTypeAdvanced(usernameField, username, enableBackspace: true, enablePause: true, enableDoubleType: true);
                
                // Kiểm tra kết quả cuối cùng
                try
                {
                    string finalValue = usernameField.GetAttribute("value");
                    Console.WriteLine($"📝 Kết quả cuối cùng - Mong muốn: '{username}', Thực tế: '{finalValue}'");
                    if (finalValue != username)
                    {
                        Console.WriteLine($"⚠️ CÓ SỰ KHÁC BIỆT! Đang sửa lại...");
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
        
        Console.WriteLine($"🔐 Bắt đầu nhập password: '{password}'");
        HumanTypeAdvanced(passwordField, password, enableBackspace: true, enablePause: true, enableDoubleType: true);
        
        // Kiểm tra kết quả cuối cùng cho password
        try
        {
            string finalValue = passwordField.GetAttribute("value");
            Console.WriteLine($"🔐 Kết quả cuối cùng password - Mong muốn: '{password}', Thực tế: '{finalValue}'");
            if (finalValue != password)
            {
                Console.WriteLine($"⚠️ CÓ SỰ KHÁC BIỆT PASSWORD! Đang sửa lại...");
                passwordField.Clear();
                Thread.Sleep(300);
                passwordField.SendKeys(password);
                Thread.Sleep(200);
                string correctedValue = passwordField.GetAttribute("value");
                Console.WriteLine($"🔧 Sau khi sửa password - Mong muốn: '{password}', Thực tế: '{correctedValue}'");
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
        
        Console.WriteLine($"🔐 Bắt đầu nhập confirm password: '{password}'");
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

    // Hàm kiểm tra trạng thái trang
    static void CheckPageState(IWebDriver driver, string context)
    {
        try
        {
            Console.WriteLine($"🔍 Kiểm tra trạng thái trang {context}...");
            
            // Kiểm tra ready state
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            string readyState = js.ExecuteScript("return document.readyState;").ToString();
            Console.WriteLine($"📊 Document ready state: {readyState}");
            
            // Kiểm tra có đang load không
            var loadingElements = driver.FindElements(By.XPath("//div[contains(@class, 'loading') or contains(@class, 'spinner') or @aria-label='Loading']"));
            
            
            // Kiểm tra lỗi validation
            var errorElements = driver.FindElements(By.XPath("//div[contains(@class, 'error') or contains(@class, 'invalid') or contains(text(), 'error') or contains(text(), 'invalid')]"));
            if (errorElements.Count > 0)
            {
                foreach (var error in errorElements.Take(3)) // Chỉ hiển thị 3 lỗi đầu
                {
                    string errorText = error.Text.Trim();
                }
            }
            
            // Kiểm tra nút Next
            try
            {
                var nextButton = driver.FindElement(By.XPath("//span[contains(text(), 'Next')]"));
                string disabled = nextButton.GetAttribute("disabled");
                string ariaDisabled = nextButton.GetAttribute("aria-disabled");
            }
            catch
            {
                Console.WriteLine($"🔘 Không tìm thấy nút Next");
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi kiểm tra trạng thái trang: {ex.Message}");
        }
    }

    static void ClickNextButton(IWebDriver driver, int currentPage = 1)
    {
        try
        {
            // Lưu URL hiện tại để kiểm tra xem có chuyển trang không
            string currentUrl = driver.Url;
            
            
            // Tìm nút Next
            IWebElement nextButton = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//span[contains(text(), 'Next')]")));
            
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            // Scroll button vào view nếu cần
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", nextButton);
            Thread.Sleep(300);
            js.ExecuteScript("arguments[0].click();", nextButton);
            
            // Chờ và kiểm tra xem trang có chuyển tiếp không
            bool pageChanged = false;
            int maxWaitTime = 15; // Tối đa 30 giây
            int waitTime = 0;
           
            
            while (!pageChanged && waitTime < maxWaitTime)
            {
                Thread.Sleep(500);
                waitTime++;
                
                try
                {
                    string newUrl = driver.Url;
                    if (newUrl != currentUrl)
                    {
                        pageChanged = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Lỗi khi kiểm tra trạng thái trang: {ex.Message}");
                }
            }
            
            if (!pageChanged)
            {
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
                                Console.WriteLine("🔄 Đang reload trang (chỉ ở trang đầu tiên)...");
                                // Reload trang
                                driver.Navigate().Refresh();
                                Thread.Sleep(3000); // Chờ trang load xong
                                
                                // Tìm và click nút Next sau khi reload
                                var nextButtonAfterReload = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                                    .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//span[contains(text(), 'Next')]")));
                                
                                js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", nextButtonAfterReload);
                                Thread.Sleep(300);
                                js.ExecuteScript("arguments[0].click();", nextButtonAfterReload);
                                
                                // Chờ thêm 5 giây để xem có chuyển trang không
                                Thread.Sleep(5000);
                                string urlAfterReload = driver.Url;
                                
                                if (urlAfterReload != currentUrl)
                                {
                                    Console.WriteLine($"✅ Reload và click thành công! URL mới: {urlAfterReload}");
                                }
                                else
                                {
                                    Console.WriteLine($"❌ Vẫn không chuyển trang được sau khi reload. URL cuối: {urlAfterReload}");
                                }
                            }
                            catch (Exception reloadEx)
                            {
                                Console.WriteLine($"❌ Không thể reload và click lại: {reloadEx.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Không reload trang vì đang ở trang {currentPage} (chỉ reload ở trang đầu tiên)");
                        }
                    }
                }
                catch (Exception retryEx)
                {
                    Console.WriteLine($"❌ Không thể click lại: {retryEx.Message}");
                }
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
            // Tìm nút Next trên màn hình Review your account info
            IWebElement nextButton = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Next']]")));
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", nextButton);
            Thread.Sleep(200);
            js.ExecuteScript("arguments[0].click();", nextButton);
            Thread.Sleep(1000);
            Console.WriteLine("✅ Đã ấn nút Next ở màn hình Review account info");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không ấn được nút Next ở màn hình Review account info: {ex.Message}");
        }
    }

    static void FillDayAndYearNew(IWebDriver driver)
    {
        try
        {
            Random random = new Random();
            int day = random.Next(1, 29);
            int year = random.Next(1985, 2010);

            IWebElement dayField = driver.FindElement(By.XPath("//input[@aria-label='Day']"));
            dayField.Clear();
            HumanType(dayField, day.ToString());

            IWebElement yearField = driver.FindElement(By.XPath("//input[@aria-label='Year']"));
            yearField.Clear();
            HumanType(yearField, year.ToString());

            Console.WriteLine("Đã nhập ngày: " + day + " - năm: " + year);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi nhập ngày tháng năm: " + ex.Message);
        }
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

        Thread.Sleep(1000);

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
            
            Thread.Sleep(200);
            
            // Click element
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

            Thread.Sleep(1000); // Đợi dropdown hiện ra

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
        string url = "https://dailyotp.com/api/rent-number?appBrand=Google / Gmail / Youtube&countryCode=US&serverName=Server 1&api_key=4cdba4a83cb5e06bf4f81bb491f7a434vUo9b9CciGZ1VPPjbDcj";

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
                // Nếu tìm thấy, click vào element đầu tiên
                var createOwnOption = createOwnElements[0];
                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                js.ExecuteScript("arguments[0].click();", createOwnOption);
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
            // Tìm nút Skip trên popup recovery email (nếu có)
            IWebElement skipButton = new WebDriverWait(driver, TimeSpan.FromSeconds(5))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Skip']]")));
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", skipButton);
            Thread.Sleep(200);
            js.ExecuteScript("arguments[0].click();", skipButton);
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
            // Cuộn xuống cuối trang để nút I agree hiện ra
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
            Thread.Sleep(1000);
            // Tìm nút I agree
            IWebElement agreeButton = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='I agree']]")));
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", agreeButton);
            Thread.Sleep(200);
            js.ExecuteScript("arguments[0].click();", agreeButton);
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
            // Tìm nút Confirm trên popup Confirm personalization
            IWebElement confirmButton = new WebDriverWait(driver, TimeSpan.FromSeconds(5))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Confirm']]")));
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", confirmButton);
            Thread.Sleep(200);
            js.ExecuteScript("arguments[0].click();", confirmButton);
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
            string url2FA = "https://myaccount.google.com/signinoptions/twosv";
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
            
            // Cách 1: Click bằng JavaScript
            try
            {
                js.ExecuteScript("arguments[0].click();", addPhoneBtn);
                clickSuccess = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Click JavaScript thất bại: {ex.Message}");
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
            Thread.Sleep(500);
            // Tìm và click nút Next
            IWebElement nextButton = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Next']]")));
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", nextButton);
            Thread.Sleep(200);
            js.ExecuteScript("arguments[0].click();", nextButton);
            Thread.Sleep(1000);
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
            // Tìm nút Save trên popup Confirm your phone number
            IWebElement saveButton = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Save']]")));
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", saveButton);
            Thread.Sleep(200);
            js.ExecuteScript("arguments[0].click();", saveButton);
            Thread.Sleep(5000); // Đợi load xong
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
            // Đợi popup có nút Done xuất hiện và click
            IWebElement doneButton = new WebDriverWait(driver, TimeSpan.FromSeconds(30))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Done']]")));
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", doneButton);
            Thread.Sleep(200);
            js.ExecuteScript("arguments[0].click();", doneButton);
            Thread.Sleep(1000);
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
            // Truy cập vào trang Authenticator app
            string urlAuthApp = "https://myaccount.google.com/two-step-verification/authenticator";
            driver.Navigate().GoToUrl(urlAuthApp);
            Thread.Sleep(3000);
            // Tìm và click nút Set up authenticator
            IWebElement setupBtn = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[contains(text(),'Set up authenticator')]]")));
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", setupBtn);
            Thread.Sleep(200);
            js.ExecuteScript("arguments[0].click();", setupBtn);
            Thread.Sleep(1000);
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
            
            // Tìm kiếm bằng CSS selectors
            /*if (popup == null || !IsValidAuthenticatorKey(popupText))
            {
                Console.WriteLine("🔍 Tìm kiếm bằng CSS selectors...");
                var cssSelectors = new[]
                {
                    "strong",
                    ".mkJZb strong",
                    ".AOmWL strong",
                    ".mzEcT strong",
                    ".qPtGzb strong",
                    ".XyKopc strong",
                    ".qRUolc strong",
                    ".GheHHf strong"
                };
                
                foreach (var cssSelector in cssSelectors)
                {
                    try
                    {
                        var elements = driver.FindElements(By.CssSelector(cssSelector));
                        foreach (var element in elements)
                        {
                            try
                            {
                                string elementText = element.Text.Trim();
                                Console.WriteLine($"🔍 CSS '{cssSelector}': '{elementText}'");
                                
                                if (IsValidAuthenticatorKey(elementText))
                                {
                                    popupText = elementText;
                                    Console.WriteLine($"✅ Tìm thấy key bằng CSS '{cssSelector}': {elementText}");
                                    break;
                                }
                            }
                            catch
                            {
                                continue;
                            }
                        }
                        if (IsValidAuthenticatorKey(popupText))
                            break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Lỗi CSS selector '{cssSelector}': {ex.Message}");
                    }
                }
            }
            
            // Nếu vẫn không tìm thấy, thử tìm tất cả div có chứa text
            if (popup == null || !IsValidAuthenticatorKey(popupText))
            {
                Console.WriteLine("🔍 Tìm kiếm tất cả div có chứa key...");
                try
                {
                    var allDivs = driver.FindElements(By.TagName("div"));
                    foreach (var div in allDivs)
                    {
                        try
                        {
                            string divText = div.Text;
                            if (divText.Contains("key") && IsValidAuthenticatorKey(divText))
                            {
                                popup = div;
                                popupText = divText;
                                Console.WriteLine($"✅ Tìm thấy div chứa key: {divText}");
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
                    Console.WriteLine($"⚠️ Lỗi khi tìm div: {ex.Message}");
                }
            }
            
            if (popup == null || string.IsNullOrEmpty(popupText) || !IsValidAuthenticatorKey(popupText))
            {
                Console.WriteLine("❌ Không tìm thấy popup chứa key Authenticator!");
                Console.WriteLine("🔍 Đang tìm kiếm key trong HTML source...");
                try
                {
                    string pageSource = driver.PageSource;
                    Console.WriteLine($"📄 Độ dài HTML: {pageSource.Length} ký tự");
                    
                    // Tìm kiếm key trực tiếp trong HTML source
                    var keyPatterns = new[]
                    {
                        @"<strong[^>]*>([a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4})</strong>",
                        @"([a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4})",
                        @"([a-z0-9]{4}\s+[0-9][a-z0-9]{3}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[0-9][a-z0-9]{3}\s+[a-z0-9]{4})"
                    };
                    
                    foreach (var pattern in keyPatterns)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(pageSource, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string foundKey = match.Groups[1].Value.Trim();
                            if (IsValidAuthenticatorKey(foundKey))
                            {
                                Console.WriteLine($"✅ Tìm thấy key trong HTML source: {foundKey}");
                                return foundKey;
                            }
                        }
                    }
                    
                    // Tìm kiếm các từ khóa liên quan
                    if (pageSource.Contains("key") || pageSource.Contains("hbu7") || pageSource.Contains("2j67"))
                    {
                        Console.WriteLine("✅ Tìm thấy từ khóa liên quan trong HTML");
                        // In ra phần HTML chứa từ khóa
                        int keyIndex = pageSource.IndexOf("key", StringComparison.OrdinalIgnoreCase);
                        if (keyIndex >= 0 && keyIndex < pageSource.Length - 1000)
                        {
                            Console.WriteLine($"📄 HTML xung quanh 'key': {pageSource.Substring(keyIndex, 1000)}");
                        }
                        
                        // Tìm kiếm cụ thể key từ hình ảnh
                        if (pageSource.Contains("hbu7 2j67 bru3 r3ed ttjc zhm3 3sew yqpy"))
                        {
                            Console.WriteLine("✅ Tìm thấy key cụ thể từ hình ảnh!");
                            return "hbu7 2j67 bru3 r3ed ttjc zhm3 3sew yqpy";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Không thể lấy HTML: {ex.Message}");
                }
                return null;
            }*/
            
            // Cải thiện regex để tìm key chính xác hơn
            // Pattern cho key dạng: hbu7 2j67 bru3 r3ed ttjc zhm3 3sew yqpy
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
            
            // Thử tìm kiếm bằng JavaScript
            /*Console.WriteLine("🔍 Thử tìm kiếm bằng JavaScript...");
            try
            {
                // Tìm tất cả thẻ strong
                var script = @"
                    var strongs = document.getElementsByTagName('strong');
                    var results = [];
                    for (var i = 0; i < strongs.length; i++) {
                        var text = strongs[i].textContent.trim();
                        if (text.length > 20 && text.includes(' ')) {
                            results.push(text);
                        }
                    }
                    return results;
                ";
                
                var jsResults = ((IJavaScriptExecutor)driver).ExecuteScript(script) as System.Collections.ArrayList;
                if (jsResults != null)
                {
                    foreach (var result in jsResults)
                    {
                        string text = result.ToString();
                        Console.WriteLine($"🔍 JavaScript tìm thấy: '{text}'");
                        if (IsValidAuthenticatorKey(text))
                        {
                            Console.WriteLine($"✅ Tìm thấy key bằng JavaScript: {text}");
                            return text;
                        }
                    }
                }
                
                // Tìm kiếm trong tất cả text nodes
                var textScript = @"
                    function getAllTextNodes() {
                        var walker = document.createTreeWalker(
                            document.body,
                            NodeFilter.SHOW_TEXT,
                            null,
                            false
                        );
                        var textNodes = [];
                        var node;
                        while (node = walker.nextNode()) {
                            var text = node.textContent.trim();
                            if (text.length > 20 && text.includes(' ') && /[a-z0-9]{4}\s+[a-z0-9]{4}/.test(text)) {
                                textNodes.push(text);
                            }
                        }
                        return textNodes;
                    }
                    return getAllTextNodes();
                ";
                
                var textResults = ((IJavaScriptExecutor)driver).ExecuteScript(textScript) as System.Collections.ArrayList;
                if (textResults != null)
                {
                    foreach (var result in textResults)
                    {
                        string text = result.ToString();
                        Console.WriteLine($"🔍 JavaScript text node: '{text}'");
                        if (IsValidAuthenticatorKey(text))
                        {
                            Console.WriteLine($"✅ Tìm thấy key trong text node: {text}");
                            return text;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi JavaScript: {ex.Message}");
            }*/
            
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
            Thread.Sleep(500);
            // Tìm và click nút Verify
            IWebElement verifyBtn = driver.FindElement(By.XPath("//span[contains(text(), 'Verify')]"));
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", verifyBtn);
            Thread.Sleep(200);
            js.ExecuteScript("arguments[0].click();", verifyBtn);
            Thread.Sleep(1000);
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
        string url2FA = "https://myaccount.google.com/two-step-verification/phone-numbers";
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

        // Thử click bằng JavaScript trước
        try
        {
            js.ExecuteScript("arguments[0].click();", trashBtn);
            Console.WriteLine("✅ Đã click vào biểu tượng thùng rác bằng JavaScript");
        }
        catch
        {
            // Nếu JavaScript click không hoạt động, thử click thông thường
            trashBtn.Click();
            Console.WriteLine("✅ Đã click vào biểu tượng thùng rác bằng Selenium");
        }

        Thread.Sleep(1000);

        // Kiểm tra xem có dialog xác nhận xuất hiện không
        try
        {
            // Tìm và click nút xác nhận xóa (nếu có)
            IWebElement verifyBtn = driver.FindElement(By.XPath("//span[contains(text(), 'OK')]"));
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", verifyBtn);
            Thread.Sleep(200);
            js.ExecuteScript("arguments[0].click();", verifyBtn);
            Console.WriteLine("✅ Đã xác nhận xóa số điện thoại");
            Thread.Sleep(1000);
        }
        catch
        {
            Console.WriteLine("ℹ️ Không tìm thấy dialog xác nhận, có thể đã xóa trực tiếp");
        }

        Console.WriteLine("✅ Đã xóa số điện thoại 2FA thành công");

    }

    // Hàm kiểm tra IP hiện tại
    static async Task<string> CheckCurrentIP()
    {
        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                string response = await client.GetStringAsync("https://api.ipify.org");
                return response.Trim();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không thể kiểm tra IP: {ex.Message}");
            return "Unknown";
        }
    }
    
    // Hàm hiển thị IP hiện tại với hướng dẫn rotation
    static async Task CheckAndDisplayCurrentIP()
    {
        string currentIP = await CheckCurrentIP();
        
        Console.Write("\nBạn có muốn test rotation IP ngay không? (y/n): ");
        if (Console.ReadLine()?.ToLower().StartsWith("y") == true)
        {
            Console.WriteLine("\n⏳ Hãy thực hiện airplane mode rotation và quay lại...");
            Console.Write("Nhấn Enter sau khi hoàn tất: ");
            Console.ReadLine();
            
            Console.WriteLine("🔄 Đang kiểm tra IP mới...");
            string newIP = await CheckCurrentIP();
            
            if (newIP != currentIP && newIP != "Unknown")
            {
                Console.WriteLine($"🎉 THÀNH CÔNG! IP đã thay đổi: {currentIP} → {newIP}");
            }
            else if (newIP == currentIP)
            {
                Console.WriteLine($"⚠️ IP vẫn giống cũ: {currentIP}");
                Console.WriteLine("💡 Thử lại với thời gian airplane mode dài hơn (30-60s)");
            }
            else
            {
                Console.WriteLine("❌ Không thể kiểm tra IP mới");
            }
        }
    }

    // Menu quản lý proxy và fingerprint
    static async Task ShowProxyMenu()
    {
        while (true)
        {
            Console.WriteLine("\n=== MENU QUẢN LÝ PROXY & FINGERPRINT ===");
            Console.WriteLine("1. Xem danh sách proxy hiện tại");
            Console.WriteLine("2. Test tất cả proxy");
            Console.WriteLine("3. Thêm proxy mới");
            Console.WriteLine("4. Tải lại danh sách proxy từ file");
            Console.WriteLine("5. Xóa dữ liệu Chrome (xóa fingerprint cũ)");
            Console.WriteLine("6. Tạo fingerprint mới và test");
            Console.WriteLine("7. Xóa tất cả Chrome profiles đã lưu");
            Console.WriteLine("8. Hiển thị danh sách Chrome profiles");
            Console.WriteLine("9. Kiểm tra IP hiện tại (cho hotspot 4G)");
            Console.WriteLine("10. Kiểm tra tính nhất quán fingerprint ngẫu nhiên");
            Console.WriteLine("11. Test fingerprint thực tế với Chrome instances");
            Console.WriteLine("12. Bắt đầu tạo tài khoản Gmail");
            Console.WriteLine("0. Thoát");
            Console.Write("Chọn tùy chọn: ");

            string choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    ShowProxyList();
                    break;
                case "2":
                    await TestAllProxies();
                    break;
                case "3":
                    AddNewProxy();
                    break;
                case "4":
                    _proxyManager.LoadProxies();
                    break;
                case "5":
                    ClearChromeData();
                    break;
                case "6":
                    TestNewFingerprint();
                    break;
                case "7":
                    ClearAllChromeProfiles();
                    break;
                case "8":
                    ShowChromeProfiles();
                    break;
                case "9":
                    await CheckAndDisplayCurrentIP();
                    break;
                case "10":
                    TestFingerprintConsistency();
                    break;
                case "11":
                    TestRealFingerprintConsistency();
                    break;
                case "12":
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

    static void ShowProxyList()
    {
        var proxies = _proxyManager.GetAllProxies();
        if (proxies.Count == 0)
        {
            Console.WriteLine("📝 Không có proxy nào trong danh sách");
            return;
        }

        Console.WriteLine($"📋 Danh sách {proxies.Count} proxy:");
        for (int i = 0; i < proxies.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {proxies[i]}");
        }
    }

    static async Task TestAllProxies()
    {
        Console.WriteLine("🔍 Bắt đầu test tất cả proxy...");
        var workingProxies = await _proxyManager.TestAllProxies();
        
        if (workingProxies.Count == 0)
        {
            Console.WriteLine("⚠️ Không có proxy nào hoạt động!");
        }
        else
        {
            Console.WriteLine($"✅ Có {workingProxies.Count} proxy hoạt động");
        }
    }

    static void AddNewProxy()
    {
        Console.Write("Nhập host proxy (VD: 192.168.1.100): ");
        string host = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(host))
        {
            Console.WriteLine("❌ Host không được để trống!");
            return;
        }

        Console.Write("Nhập port proxy (VD: 8080): ");
        if (!int.TryParse(Console.ReadLine(), out int port) || port <= 0 || port > 65535)
        {
            Console.WriteLine("❌ Port không hợp lệ!");
            return;
        }

        Console.Write("Proxy có cần xác thực không? (y/n): ");
        bool needAuth = Console.ReadLine()?.ToLower().StartsWith("y") == true;

        string username = null;
        string password = null;

        if (needAuth)
        {
            Console.Write("Nhập username: ");
            username = Console.ReadLine()?.Trim();
            
            Console.Write("Nhập password: ");
            password = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("❌ Username và password không được để trống!");
                return;
            }
        }

        _proxyManager.AddProxy(host, port, username, password);
        Console.WriteLine("✅ Đã thêm proxy thành công!");
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
    
    // Hàm hiển thị danh sách Chrome profiles
    static void ShowChromeProfiles()
    {
        try
        {
            Console.WriteLine("📋 Danh sách Chrome profiles đã lưu:");
            
            string userDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "Google", "Chrome", "User Data");
            
            if (!Directory.Exists(userDataPath))
            {
                Console.WriteLine("📁 Không tìm thấy thư mục Chrome User Data");
                return;
            }
            
            var regMailProfiles = Directory.GetDirectories(userDataPath)
                .Where(dir => Path.GetFileName(dir).StartsWith("RegMail_Profile_"))
                .OrderBy(dir => Path.GetFileName(dir))
                .ToArray();
            
            if (regMailProfiles.Length == 0)
            {
                Console.WriteLine("📝 Không có profile RegMail nào được lưu");
                return;
            }
            
            Console.WriteLine($"\n🔍 Tìm thấy {regMailProfiles.Length} profile RegMail:");
            for (int i = 0; i < regMailProfiles.Length; i++)
            {
                var profile = regMailProfiles[i];
                var profileName = Path.GetFileName(profile);
                var createdTime = Directory.GetCreationTime(profile);
                var sizeInfo = GetDirectorySize(profile);
                
                Console.WriteLine($"   {i + 1}. {profileName}");
                Console.WriteLine($"      📅 Tạo: {createdTime:dd/MM/yyyy HH:mm}");
                Console.WriteLine($"      💾 Kích thước: {sizeInfo}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi khi hiển thị Chrome profiles: {ex.Message}");
        }
    }
    
    // Hàm tính kích thước thư mục
    static string GetDirectorySize(string dirPath)
    {
        try
        {
            long totalSize = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);
            
            if (totalSize < 1024)
                return $"{totalSize} bytes";
            else if (totalSize < 1024 * 1024)
                return $"{totalSize / 1024:F1} KB";
            else if (totalSize < 1024 * 1024 * 1024)
                return $"{totalSize / (1024 * 1024):F1} MB";
            else
                return $"{totalSize / (1024 * 1024 * 1024):F1} GB";
        }
        catch
        {
            return "Không xác định";
        }
    }

    static void InjectAntiDetectionScripts(IWebDriver driver)
    {
        try
        {
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

            // 1. KHẮC PHỤC navigator.webdriver = true (QUAN TRỌNG NHẤT)
            js.ExecuteScript(@"
                // Xóa hoàn toàn webdriver property
                delete Object.getPrototypeOf(navigator).webdriver;
                
                // Override webdriver property
                Object.defineProperty(navigator, 'webdriver', {
                    get: () => undefined,
                    configurable: true
                });
                
                // Xóa các property liên quan đến automation
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Array;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Promise;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Symbol;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Object;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Function;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_String;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Number;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Boolean;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Date;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_RegExp;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Error;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_ArrayBuffer;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_DataView;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Float32Array;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Float64Array;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Int8Array;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Int16Array;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Int32Array;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Uint8Array;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Uint16Array;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Uint32Array;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Uint8ClampedArray;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Map;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Set;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_WeakMap;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_WeakSet;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Proxy;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Reflect;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Promise;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Generator;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_GeneratorFunction;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_AsyncFunction;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_AsyncGenerator;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_AsyncGeneratorFunction;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Iterator;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_AsyncIterator;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Symbol;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_SymbolAsyncIterator;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_SymbolHasInstance;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_SymbolIsConcatSpreadable;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_SymbolIterator;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_SymbolMatch;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_SymbolMatchAll;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_SymbolReplace;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_SymbolSearch;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_SymbolSpecies;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_SymbolSplit;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_SymbolToPrimitive;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_SymbolToStringTag;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_SymbolUnscopables;
            ");

            // 2. KHẮC PHỤC Plugins/MIME trống - Tạo plugins thật
            js.ExecuteScript(@"
                // Tạo plugins thật thay vì array rỗng
                const realPlugins = [
                    {
                        name: 'Chrome PDF Plugin',
                        filename: 'internal-pdf-viewer',
                        description: 'Portable Document Format',
                        length: 1
                    },
                    {
                        name: 'Chrome PDF Viewer',
                        filename: 'mhjfbmdgcfjbbpaeojofohoefgiehjai',
                        description: '',
                        length: 1
                    },
                    {
                        name: 'Native Client',
                        filename: 'internal-nacl-plugin',
                        description: '',
                        length: 1
                    }
                ];
                
                Object.defineProperty(navigator, 'plugins', {
                    get: () => realPlugins,
                    configurable: true
                });
                
                // Tạo mimeTypes tương ứng
                const realMimeTypes = [
                    {
                        type: 'application/pdf',
                        suffixes: 'pdf',
                        description: 'Portable Document Format',
                        enabledPlugin: realPlugins[0]
                    },
                    {
                        type: 'application/x-google-chrome-pdf',
                        suffixes: 'pdf',
                        description: 'Portable Document Format',
                        enabledPlugin: realPlugins[1]
                    },
                    {
                        type: 'application/x-nacl',
                        suffixes: '',
                        description: 'Native Client Executable',
                        enabledPlugin: realPlugins[2]
                    },
                    {
                        type: 'application/x-pnacl',
                        suffixes: '',
                        description: 'Portable Native Client Executable',
                        enabledPlugin: realPlugins[2]
                    }
                ];
                
                Object.defineProperty(navigator, 'mimeTypes', {
                    get: () => realMimeTypes,
                    configurable: true
                });
            ");

            // 3. KHẮC PHỤC Languages/permissions khác lạ - Tạo languages tự nhiên
            js.ExecuteScript(@"
                // Tạo languages array tự nhiên
                const naturalLanguages = ['en-US', 'en', 'vi-VN', 'vi'];
                Object.defineProperty(navigator, 'languages', {
                    get: () => naturalLanguages,
                    configurable: true
                });
                
                Object.defineProperty(navigator, 'language', {
                    get: () => 'en-US',
                    configurable: true
                });
                
                // Override permissions API để trả về kết quả tự nhiên
                const originalQuery = window.navigator.permissions.query;
                window.navigator.permissions.query = (parameters) => {
                    if (parameters.name === 'notifications') {
                        return Promise.resolve({ state: 'prompt' });
                    }
                    if (parameters.name === 'geolocation') {
                        return Promise.resolve({ state: 'prompt' });
                    }
                    if (parameters.name === 'microphone') {
                        return Promise.resolve({ state: 'prompt' });
                    }
                    if (parameters.name === 'camera') {
                        return Promise.resolve({ state: 'prompt' });
                    }
                    if (parameters.name === 'persistent-storage') {
                        return Promise.resolve({ state: 'granted' });
                    }
                    return originalQuery(parameters);
                };
            ");

            // 4. KHẮC PHỤC Event chuột/bàn phím thiếu tự nhiên - Override event listeners
            js.ExecuteScript(@"
                // Override addEventListener để thêm randomness vào events
                const originalAddEventListener = EventTarget.prototype.addEventListener;
                EventTarget.prototype.addEventListener = function(type, listener, options) {
                    if (type === 'mousemove' || type === 'click' || type === 'keydown' || type === 'keyup') {
                        const wrappedListener = function(event) {
                            // Thêm randomness vào event timing
                            if (Math.random() < 0.1) {
                                setTimeout(() => listener.call(this, event), Math.random() * 10);
                            } else {
                                listener.call(this, event);
                            }
                        };
                        return originalAddEventListener.call(this, type, wrappedListener, options);
                    }
                    return originalAddEventListener.call(this, type, listener, options);
                };
                
                // Override getBoundingClientRect để thêm randomness
                const originalGetBoundingClientRect = Element.prototype.getBoundingClientRect;
                Element.prototype.getBoundingClientRect = function() {
                    const rect = originalGetBoundingClientRect.call(this);
                    // Thêm randomness nhỏ vào coordinates
                    rect.x += (Math.random() - 0.5) * 0.1;
                    rect.y += (Math.random() - 0.5) * 0.1;
                    return rect;
                };
            ");

            // 5. KHẮC PHỤC Chrome launch flags lạ - Override chrome object
            js.ExecuteScript(@"
                // Tạo chrome object tự nhiên
                window.chrome = {
                    runtime: {
                        onConnect: undefined,
                        onMessage: undefined,
                        sendMessage: undefined,
                        connect: undefined,
                        id: undefined,
                        getManifest: function() { return {}; },
                        getURL: function(path) { return 'chrome-extension://' + Math.random().toString(36).substr(2, 9) + '/' + path; }
                    },
                    loadTimes: function() {
                        return {
                            commitLoadTime: Date.now() / 1000,
                            connectionInfo: 'h2',
                            finishDocumentLoadTime: Date.now() / 1000,
                            finishLoadTime: Date.now() / 1000,
                            firstPaintAfterLoadTime: Date.now() / 1000,
                            navigationType: 'Other',
                            npnNegotiatedProtocol: 'h2',
                            requestTime: Date.now() / 1000,
                            startLoadTime: Date.now() / 1000,
                            wasAlternateProtocolAvailable: false,
                            wasFetchedViaSpdy: true,
                            wasNpnNegotiated: true
                        };
                    },
                    csi: function() {
                        return {
                            onloadT: Date.now(),
                            pageT: Date.now(),
                            startE: Date.now(),
                            tran: 15
                        };
                    },
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
                    }
                };
            ");

            // 6. KHẮC PHỤC Timing hành vi không giống người thật - Override timing functions
            js.ExecuteScript(@"
                // Override Date.now() để thêm randomness nhỏ
                const originalDateNow = Date.now;
                Date.now = function() {
                    return originalDateNow() + (Math.random() - 0.5) * 2;
                };
                
                // Override performance.now() để thêm randomness
                const originalPerformanceNow = performance.now;
                performance.now = function() {
                    return originalPerformanceNow() + (Math.random() - 0.5) * 1;
                };
                
                // Override setTimeout để thêm randomness
                const originalSetTimeout = window.setTimeout;
                window.setTimeout = function(func, delay, ...args) {
                    const randomDelay = delay + (Math.random() - 0.5) * 10;
                    return originalSetTimeout(func, Math.max(0, randomDelay), ...args);
                };
                
                // Override setInterval để thêm randomness
                const originalSetInterval = window.setInterval;
                window.setInterval = function(func, delay, ...args) {
                    const randomDelay = delay + (Math.random() - 0.5) * 5;
                    return originalSetInterval(func, Math.max(0, randomDelay), ...args);
                };
            ");

            // 7. THÊM CÁC OVERRIDE KHÁC ĐỂ TRÁNH PHÁT HIỆN
            js.ExecuteScript(@"
                // Override toString để ẩn automation
                const originalToString = Function.prototype.toString;
                Function.prototype.toString = function() {
                    const str = originalToString.call(this);
                    if (str.includes('webdriver') || str.includes('selenium')) {
                        return 'function() { [native code] }';
                    }
                    return str;
                };
                
                // Override console.log để ẩn debug info
                const originalConsoleLog = console.log;
                console.log = function(...args) {
                    const message = args.join(' ');
                    if (message.includes('webdriver') || message.includes('selenium') || message.includes('automation')) {
                        return;
                    }
                    return originalConsoleLog.apply(console, args);
                };
                
                // Override fetch để thêm headers tự nhiên
                const originalFetch = window.fetch;
                window.fetch = function(url, options = {}) {
                    if (!options.headers) {
                        options.headers = {};
                    }
                    options.headers['Accept'] = 'text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8';
                    options.headers['Accept-Language'] = 'en-US,en;q=0.5';
                    options.headers['Accept-Encoding'] = 'gzip, deflate, br';
                    options.headers['DNT'] = '1';
                    options.headers['Connection'] = 'keep-alive';
                    options.headers['Upgrade-Insecure-Requests'] = '1';
                    return originalFetch(url, options);
                };
            ");

            Console.WriteLine("✅ Đã inject thành công các script chống phát hiện automation nâng cao");
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
        IWebElement nextButton = driver.FindElement(By.XPath("//span[contains(text(), 'Next')]"));
        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
        js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", nextButton);
        Thread.Sleep(200);
        js.ExecuteScript("arguments[0].click();", nextButton);
        Thread.Sleep(1000);
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
                    Console.WriteLine($"📊 Kiểm tra định kỳ [{currentPosition}/{text.Length}]: expected='{expectedValue}', actual='{currentValue}'");
                    
                    // Nếu có sự khác biệt lớn, sửa lại
                    if (currentValue.Length < expectedValue.Length - 2 || currentValue.Length > expectedValue.Length + 2)
                    {
                        Console.WriteLine($"⚠️ Phát hiện sự khác biệt lớn, đang sửa lại...");
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
            Console.WriteLine($"🔍 Kiểm tra cuối cùng - Mong muốn: '{text}', Thực tế: '{finalValue}'");
            
            if (finalValue != text)
            {
                Console.WriteLine($"🔧 Phát hiện lỗi cuối cùng, đang sửa lại...");
                element.Clear();
                Thread.Sleep(200);
                element.SendKeys(text);
                Thread.Sleep(200);
                
                string correctedValue = element.GetAttribute("value");
                Console.WriteLine($"🔧 Sau khi sửa cuối cùng: '{correctedValue}'");
                
                if (correctedValue != text)
                {
                    Console.WriteLine($"⚠️ Vẫn còn lỗi sau khi sửa! Mong muốn: '{text}', Thực tế: '{correctedValue}'");
                }
                else
                {
                    Console.WriteLine($"✅ Đã sửa thành công!");
                }
            }
            else
            {
                Console.WriteLine($"✅ Kết quả cuối cùng chính xác: '{finalValue}'");
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
    // Hàm kiểm tra tính nhất quán của fingerprint ngẫu nhiên
    static void TestFingerprintConsistency()
    {
        Console.WriteLine("🧪 Bắt đầu kiểm tra tính nhất quán của fingerprint ngẫu nhiên...");
        Console.WriteLine("📊 Sẽ tạo 10 fingerprint ngẫu nhiên và so sánh...");
        
        var fingerprints = new List<FingerprintInfo>();
        
        // Tạo 10 fingerprint ngẫu nhiên
        for (int i = 0; i < 10; i++)
        {
            var fingerprint = FingerprintManager.GenerateRandomFingerprint();
            fingerprints.Add(fingerprint);
            Console.WriteLine($"\n🔍 Fingerprint {i + 1}:");
            Console.WriteLine($"   📱 Profile: {fingerprint.ProfileName}");
            Console.WriteLine($"   🌐 User Agent: {fingerprint.UserAgent}");
            Console.WriteLine($"   🌍 Language: {fingerprint.Language}");
            Console.WriteLine($"   🖥️ Platform: {fingerprint.Platform}");
            Console.WriteLine($"   📺 Resolution: {fingerprint.ScreenResolution}");
            Console.WriteLine($"   ⏰ Timezone: {fingerprint.Timezone}");
            Console.WriteLine($"   💾 Memory: {fingerprint.DeviceMemory}GB");
            Console.WriteLine($"   🔧 CPU Cores: {fingerprint.HardwareConcurrency}");
            Console.WriteLine($"   🎮 GPU Vendor: {fingerprint.WebGLVendor}");
            Console.WriteLine($"   🎮 GPU Renderer: {fingerprint.WebGLRenderer}");
            Console.WriteLine($"   🎯 Touch Support: {fingerprint.TouchSupport}");
        }
        
        // Phân tích tính nhất quán
        Console.WriteLine("\n📈 PHÂN TÍCH TÍNH NHẤT QUÁN:");
        Console.WriteLine("================================");
        
        // Kiểm tra User Agent
        var userAgents = fingerprints.Select(f => f.UserAgent).Distinct().ToList();
        Console.WriteLine($"🌐 User Agents duy nhất: {userAgents.Count}/10");
        if (userAgents.Count < 10)
        {
            Console.WriteLine("⚠️ CÓ TRÙNG LẶP User Agent!");
            var duplicates = fingerprints.GroupBy(f => f.UserAgent)
                .Where(g => g.Count() > 1)
                .Select(g => new { UserAgent = g.Key, Count = g.Count() });
            foreach (var dup in duplicates)
            {
                Console.WriteLine($"   - '{dup.UserAgent}' xuất hiện {dup.Count} lần");
            }
        }
        else
        {
            Console.WriteLine("✅ User Agents hoàn toàn khác nhau");
        }
        
        // Kiểm tra Platform
        var platforms = fingerprints.Select(f => f.Platform).Distinct().ToList();
        Console.WriteLine($"🖥️ Platforms duy nhất: {platforms.Count}/10");
        if (platforms.Count < 10)
        {
            Console.WriteLine("⚠️ CÓ TRÙNG LẶP Platform!");
            var duplicates = fingerprints.GroupBy(f => f.Platform)
                .Where(g => g.Count() > 1)
                .Select(g => new { Platform = g.Key, Count = g.Count() });
            foreach (var dup in duplicates)
            {
                Console.WriteLine($"   - '{dup.Platform}' xuất hiện {dup.Count} lần");
            }
        }
        else
        {
            Console.WriteLine("✅ Platforms hoàn toàn khác nhau");
        }
        
        // Kiểm tra Screen Resolution
        var resolutions = fingerprints.Select(f => f.ScreenResolution).Distinct().ToList();
        Console.WriteLine($"📺 Resolutions duy nhất: {resolutions.Count}/10");
        if (resolutions.Count < 10)
        {
            Console.WriteLine("⚠️ CÓ TRÙNG LẶP Resolution!");
            var duplicates = fingerprints.GroupBy(f => f.ScreenResolution)
                .Where(g => g.Count() > 1)
                .Select(g => new { Resolution = g.Key, Count = g.Count() });
            foreach (var dup in duplicates)
            {
                Console.WriteLine($"   - '{dup.Resolution}' xuất hiện {dup.Count} lần");
            }
        }
        else
        {
            Console.WriteLine("✅ Resolutions hoàn toàn khác nhau");
        }
        
        // Kiểm tra Timezone
        var timezones = fingerprints.Select(f => f.Timezone).Distinct().ToList();
        Console.WriteLine($"⏰ Timezones duy nhất: {timezones.Count}/10");
        if (timezones.Count < 10)
        {
            Console.WriteLine("⚠️ CÓ TRÙNG LẶP Timezone!");
            var duplicates = fingerprints.GroupBy(f => f.Timezone)
                .Where(g => g.Count() > 1)
                .Select(g => new { Timezone = g.Key, Count = g.Count() });
            foreach (var dup in duplicates)
            {
                Console.WriteLine($"   - '{dup.Timezone}' xuất hiện {dup.Count} lần");
            }
        }
        else
        {
            Console.WriteLine("✅ Timezones hoàn toàn khác nhau");
        }
        
        // Kiểm tra WebGL Vendor
        var vendors = fingerprints.Select(f => f.WebGLVendor).Distinct().ToList();
        Console.WriteLine($"🎮 GPU Vendors duy nhất: {vendors.Count}/10");
        if (vendors.Count < 10)
        {
            Console.WriteLine("⚠️ CÓ TRÙNG LẶP GPU Vendor!");
            var duplicates = fingerprints.GroupBy(f => f.WebGLVendor)
                .Where(g => g.Count() > 1)
                .Select(g => new { Vendor = g.Key, Count = g.Count() });
            foreach (var dup in duplicates)
            {
                Console.WriteLine($"   - '{dup.Vendor}' xuất hiện {dup.Count} lần");
            }
        }
        else
        {
            Console.WriteLine("✅ GPU Vendors hoàn toàn khác nhau");
        }
        
        // Kiểm tra WebGL Renderer
        var renderers = fingerprints.Select(f => f.WebGLRenderer).Distinct().ToList();
        Console.WriteLine($"🎮 GPU Renderers duy nhất: {renderers.Count}/10");
        if (renderers.Count < 10)
        {
            Console.WriteLine("⚠️ CÓ TRÙNG LẶP GPU Renderer!");
            var duplicates = fingerprints.GroupBy(f => f.WebGLRenderer)
                .Where(g => g.Count() > 1)
                .Select(g => new { Renderer = g.Key, Count = g.Count() });
            foreach (var dup in duplicates)
            {
                Console.WriteLine($"   - '{dup.Renderer}' xuất hiện {dup.Count} lần");
            }
        }
        else
        {
            Console.WriteLine("✅ GPU Renderers hoàn toàn khác nhau");
        }
        
        // Kiểm tra Device Memory
        var memories = fingerprints.Select(f => f.DeviceMemory).Distinct().ToList();
        Console.WriteLine($"💾 Device Memories duy nhất: {memories.Count}/10");
        if (memories.Count < 10)
        {
            Console.WriteLine("⚠️ CÓ TRÙNG LẶP Device Memory!");
            var duplicates = fingerprints.GroupBy(f => f.DeviceMemory)
                .Where(g => g.Count() > 1)
                .Select(g => new { Memory = g.Key, Count = g.Count() });
            foreach (var dup in duplicates)
            {
                Console.WriteLine($"   - '{dup.Memory}GB' xuất hiện {dup.Count} lần");
            }
        }
        else
        {
            Console.WriteLine("✅ Device Memories hoàn toàn khác nhau");
        }
        
        // Kiểm tra Hardware Concurrency
        var cores = fingerprints.Select(f => f.HardwareConcurrency).Distinct().ToList();
        Console.WriteLine($"🔧 CPU Cores duy nhất: {cores.Count}/10");
        if (cores.Count < 10)
        {
            Console.WriteLine("⚠️ CÓ TRÙNG LẶP CPU Cores!");
            var duplicates = fingerprints.GroupBy(f => f.HardwareConcurrency)
                .Where(g => g.Count() > 1)
                .Select(g => new { Cores = g.Key, Count = g.Count() });
            foreach (var dup in duplicates)
            {
                Console.WriteLine($"   - '{dup.Cores} cores' xuất hiện {dup.Count} lần");
            }
        }
        else
        {
            Console.WriteLine("✅ CPU Cores hoàn toàn khác nhau");
        }
        
        // Tính tổng quan về tính nhất quán
        int totalUniqueAttributes = userAgents.Count + platforms.Count + resolutions.Count + 
                                  timezones.Count + vendors.Count + renderers.Count + 
                                  memories.Count + cores.Count;
        int totalPossibleAttributes = 8 * 10; // 8 thuộc tính x 10 fingerprint
        double consistencyPercentage = (double)totalUniqueAttributes / totalPossibleAttributes * 100;
        
        Console.WriteLine("\n📊 KẾT QUẢ TỔNG QUAN:");
        Console.WriteLine("=====================");
        Console.WriteLine($"🎯 Tỷ lệ nhất quán: {consistencyPercentage:F1}%");
        Console.WriteLine($"📈 Thuộc tính duy nhất: {totalUniqueAttributes}/{totalPossibleAttributes}");
        
        if (consistencyPercentage >= 90)
        {
            Console.WriteLine("✅ FINGERPRINT RẤT NHẤT QUÁN - Tốt cho automation!");
        }
        else if (consistencyPercentage >= 70)
        {
            Console.WriteLine("⚠️ FINGERPRINT KHÁ NHẤT QUÁN - Có thể cải thiện");
        }
        else
        {
            Console.WriteLine("❌ FINGERPRINT KHÔNG NHẤT QUÁN - Cần cải thiện!");
        }
        
        // Đề xuất cải thiện
        Console.WriteLine("\n💡 ĐỀ XUẤT CẢI THIỆN:");
        Console.WriteLine("=====================");
        
        if (userAgents.Count < 10)
        {
            Console.WriteLine("🔧 Cần thêm User Agents vào danh sách");
        }
        if (platforms.Count < 10)
        {
            Console.WriteLine("🔧 Cần thêm Platforms vào danh sách");
        }
        if (resolutions.Count < 10)
        {
            Console.WriteLine("🔧 Cần thêm Screen Resolutions vào danh sách");
        }
        if (timezones.Count < 10)
        {
            Console.WriteLine("🔧 Cần thêm Timezones vào danh sách");
        }
        if (vendors.Count < 10)
        {
            Console.WriteLine("🔧 Cần thêm GPU Vendors vào danh sách");
        }
        if (renderers.Count < 10)
        {
            Console.WriteLine("🔧 Cần thêm GPU Renderers vào danh sách");
        }
        
        Console.WriteLine("\n🎯 KHUYẾN NGHỊ:");
        Console.WriteLine("===============");
        Console.WriteLine("• Sử dụng fingerprint ngẫu nhiên cho mỗi tab Chrome");
        Console.WriteLine("• Xóa dữ liệu Chrome trước khi tạo fingerprint mới");
        Console.WriteLine("• Thay đổi IP (airplane mode) giữa các lần chạy");
        Console.WriteLine("• Sử dụng proxy khác nhau cho mỗi tab");
    }

    // Hàm kiểm tra fingerprint thực tế khi chạy automation
    static void TestRealFingerprintConsistency()
    {
        Console.WriteLine("🧪 Bắt đầu kiểm tra fingerprint thực tế khi chạy automation...");
        Console.WriteLine("📊 Sẽ tạo 5 Chrome instances với fingerprint khác nhau...");
        
        var fingerprints = new List<FingerprintInfo>();
        var drivers = new List<IWebDriver>();
        
        try
        {
            // Tạo 5 Chrome instances với fingerprint khác nhau
            for (int i = 0; i < 5; i++)
            {
                Console.WriteLine($"\n🔍 Tạo Chrome instance {i + 1}...");
                
                // Tạo fingerprint ngẫu nhiên
                var fingerprint = FingerprintManager.GenerateRandomFingerprint();
                fingerprints.Add(fingerprint);
                
                // Tạo Chrome options với fingerprint
                ChromeOptions options = new ChromeOptions();
                options.AddArgument("--headless"); // Chạy ẩn để test nhanh
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-dev-shm-usage");
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
                
                // Cấu hình fingerprint
                FingerprintManager.ConfigureChromeOptions(options, fingerprint);
                
                // Tạo driver
                IWebDriver driver = new ChromeDriver(options);
                drivers.Add(driver);
                
                Console.WriteLine($"✅ Đã tạo Chrome instance {i + 1} với fingerprint: {fingerprint.ProfileName}");
            }
            
            // Kiểm tra tính nhất quán
            Console.WriteLine("\n📈 PHÂN TÍCH TÍNH NHẤT QUÁN THỰC TẾ:");
            Console.WriteLine("=====================================");
            
            // Kiểm tra User Agent
            var userAgents = fingerprints.Select(f => f.UserAgent).Distinct().ToList();
            Console.WriteLine($"🌐 User Agents duy nhất: {userAgents.Count}/5");
            if (userAgents.Count < 5)
            {
                Console.WriteLine("⚠️ CÓ TRÙNG LẶP User Agent!");
                var duplicates = fingerprints.GroupBy(f => f.UserAgent)
                    .Where(g => g.Count() > 1)
                    .Select(g => new { UserAgent = g.Key, Count = g.Count() });
                foreach (var dup in duplicates)
                {
                    Console.WriteLine($"   - '{dup.UserAgent}' xuất hiện {dup.Count} lần");
                }
            }
            else
            {
                Console.WriteLine("✅ User Agents hoàn toàn khác nhau");
            }
            
            // Kiểm tra Platform
            var platforms = fingerprints.Select(f => f.Platform).Distinct().ToList();
            Console.WriteLine($"🖥️ Platforms duy nhất: {platforms.Count}/5");
            if (platforms.Count < 5)
            {
                Console.WriteLine("⚠️ CÓ TRÙNG LẶP Platform!");
                var duplicates = fingerprints.GroupBy(f => f.Platform)
                    .Where(g => g.Count() > 1)
                    .Select(g => new { Platform = g.Key, Count = g.Count() });
                foreach (var dup in duplicates)
                {
                    Console.WriteLine($"   - '{dup.Platform}' xuất hiện {dup.Count} lần");
                }
            }
            else
            {
                Console.WriteLine("✅ Platforms hoàn toàn khác nhau");
            }
            
            // Kiểm tra Screen Resolution
            var resolutions = fingerprints.Select(f => f.ScreenResolution).Distinct().ToList();
            Console.WriteLine($"📺 Resolutions duy nhất: {resolutions.Count}/5");
            if (resolutions.Count < 5)
            {
                Console.WriteLine("⚠️ CÓ TRÙNG LẶP Resolution!");
                var duplicates = fingerprints.GroupBy(f => f.ScreenResolution)
                    .Where(g => g.Count() > 1)
                    .Select(g => new { Resolution = g.Key, Count = g.Count() });
                foreach (var dup in duplicates)
                {
                    Console.WriteLine($"   - '{dup.Resolution}' xuất hiện {dup.Count} lần");
                }
            }
            else
            {
                Console.WriteLine("✅ Resolutions hoàn toàn khác nhau");
            }
            
            // Kiểm tra Timezone
            var timezones = fingerprints.Select(f => f.Timezone).Distinct().ToList();
            Console.WriteLine($"⏰ Timezones duy nhất: {timezones.Count}/5");
            if (timezones.Count < 5)
            {
                Console.WriteLine("⚠️ CÓ TRÙNG LẶP Timezone!");
                var duplicates = fingerprints.GroupBy(f => f.Timezone)
                    .Where(g => g.Count() > 1)
                    .Select(g => new { Timezone = g.Key, Count = g.Count() });
                foreach (var dup in duplicates)
                {
                    Console.WriteLine($"   - '{dup.Timezone}' xuất hiện {dup.Count} lần");
                }
            }
            else
            {
                Console.WriteLine("✅ Timezones hoàn toàn khác nhau");
            }
            
            // Kiểm tra WebGL Vendor
            var vendors = fingerprints.Select(f => f.WebGLVendor).Distinct().ToList();
            Console.WriteLine($"🎮 GPU Vendors duy nhất: {vendors.Count}/5");
            if (vendors.Count < 5)
            {
                Console.WriteLine("⚠️ CÓ TRÙNG LẶP GPU Vendor!");
                var duplicates = fingerprints.GroupBy(f => f.WebGLVendor)
                    .Where(g => g.Count() > 1)
                    .Select(g => new { Vendor = g.Key, Count = g.Count() });
                foreach (var dup in duplicates)
                {
                    Console.WriteLine($"   - '{dup.Vendor}' xuất hiện {dup.Count} lần");
                }
            }
            else
            {
                Console.WriteLine("✅ GPU Vendors hoàn toàn khác nhau");
            }
            
            // Kiểm tra WebGL Renderer
            var renderers = fingerprints.Select(f => f.WebGLRenderer).Distinct().ToList();
            Console.WriteLine($"🎮 GPU Renderers duy nhất: {renderers.Count}/5");
            if (renderers.Count < 5)
            {
                Console.WriteLine("⚠️ CÓ TRÙNG LẶP GPU Renderer!");
                var duplicates = fingerprints.GroupBy(f => f.WebGLRenderer)
                    .Where(g => g.Count() > 1)
                    .Select(g => new { Renderer = g.Key, Count = g.Count() });
                foreach (var dup in duplicates)
                {
                    Console.WriteLine($"   - '{dup.Renderer}' xuất hiện {dup.Count} lần");
                }
            }
            else
            {
                Console.WriteLine("✅ GPU Renderers hoàn toàn khác nhau");
            }
            
            // Kiểm tra Device Memory
            var memories = fingerprints.Select(f => f.DeviceMemory).Distinct().ToList();
            Console.WriteLine($"💾 Device Memories duy nhất: {memories.Count}/5");
            if (memories.Count < 5)
            {
                Console.WriteLine("⚠️ CÓ TRÙNG LẶP Device Memory!");
                var duplicates = fingerprints.GroupBy(f => f.DeviceMemory)
                    .Where(g => g.Count() > 1)
                    .Select(g => new { Memory = g.Key, Count = g.Count() });
                foreach (var dup in duplicates)
                {
                    Console.WriteLine($"   - '{dup.Memory}GB' xuất hiện {dup.Count} lần");
                }
            }
            else
            {
                Console.WriteLine("✅ Device Memories hoàn toàn khác nhau");
            }
            
            // Kiểm tra Hardware Concurrency
            var cores = fingerprints.Select(f => f.HardwareConcurrency).Distinct().ToList();
            Console.WriteLine($"🔧 CPU Cores duy nhất: {cores.Count}/5");
            if (cores.Count < 5)
            {
                Console.WriteLine("⚠️ CÓ TRÙNG LẶP CPU Cores!");
                var duplicates = fingerprints.GroupBy(f => f.HardwareConcurrency)
                    .Where(g => g.Count() > 1)
                    .Select(g => new { Cores = g.Key, Count = g.Count() });
                foreach (var dup in duplicates)
                {
                    Console.WriteLine($"   - '{dup.Cores} cores' xuất hiện {dup.Count} lần");
                }
            }
            else
            {
                Console.WriteLine("✅ CPU Cores hoàn toàn khác nhau");
            }
            
            // Tính tổng quan về tính nhất quán
            int totalUniqueAttributes = userAgents.Count + platforms.Count + resolutions.Count + 
                                      timezones.Count + vendors.Count + renderers.Count + 
                                      memories.Count + cores.Count;
            int totalPossibleAttributes = 8 * 5; // 8 thuộc tính x 5 fingerprint
            double consistencyPercentage = (double)totalUniqueAttributes / totalPossibleAttributes * 100;
            
            Console.WriteLine("\n📊 KẾT QUẢ TỔNG QUAN:");
            Console.WriteLine("=====================");
            Console.WriteLine($"🎯 Tỷ lệ nhất quán: {consistencyPercentage:F1}%");
            Console.WriteLine($"📈 Thuộc tính duy nhất: {totalUniqueAttributes}/{totalPossibleAttributes}");
            
            if (consistencyPercentage >= 90)
            {
                Console.WriteLine("✅ FINGERPRINT RẤT NHẤT QUÁN - Tốt cho automation!");
            }
            else if (consistencyPercentage >= 70)
            {
                Console.WriteLine("⚠️ FINGERPRINT KHÁ NHẤT QUÁN - Có thể cải thiện");
            }
            else
            {
                Console.WriteLine("❌ FINGERPRINT KHÔNG NHẤT QUÁN - Cần cải thiện!");
            }
            
            // Test thực tế với một trang web
            Console.WriteLine("\n🌐 TEST THỰC TẾ VỚI TRANG WEB:");
            Console.WriteLine("===============================");
            
            for (int i = 0; i < drivers.Count; i++)
            {
                try
                {
                    var driver = drivers[i];
                    var fingerprint = fingerprints[i];
                    
                    Console.WriteLine($"\n🔍 Test Chrome instance {i + 1}...");
                    
                    // Truy cập trang test fingerprint
                    driver.Navigate().GoToUrl("https://bot.sannysoft.com");
                    Thread.Sleep(3000);
                    
                    // Lấy thông tin từ trang
                    var pageTitle = driver.Title;
                    Console.WriteLine($"   📄 Title: {pageTitle}");
                    
                    // Kiểm tra xem có bị phát hiện là bot không
                    var pageSource = driver.PageSource;
                    if (pageSource.Contains("bot") || pageSource.Contains("automation") || pageSource.Contains("selenium"))
                    {
                        Console.WriteLine("   ⚠️ CÓ THỂ BỊ PHÁT HIỆN LÀ BOT!");
                    }
                    else
                    {
                        Console.WriteLine("   ✅ KHÔNG BỊ PHÁT HIỆN LÀ BOT");
                    }
                    
                    Console.WriteLine($"   📱 Fingerprint: {fingerprint.ProfileName}");
                    Console.WriteLine($"   🌐 User Agent: {fingerprint.UserAgent}");
                    Console.WriteLine($"   🖥️ Platform: {fingerprint.Platform}");
                    Console.WriteLine($"   📺 Resolution: {fingerprint.ScreenResolution}");
                    Console.WriteLine($"   ⏰ Timezone: {fingerprint.Timezone}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Lỗi khi test Chrome instance {i + 1}: {ex.Message}");
                }
            }
            
            Console.WriteLine("\n🎯 KHUYẾN NGHỊ CHO AUTOMATION:");
            Console.WriteLine("===============================");
            Console.WriteLine("• Sử dụng fingerprint ngẫu nhiên cho mỗi tab Chrome");
            Console.WriteLine("• Xóa dữ liệu Chrome trước khi tạo fingerprint mới");
            Console.WriteLine("• Thay đổi IP (airplane mode) giữa các lần chạy");
            Console.WriteLine("• Sử dụng proxy khác nhau cho mỗi tab");
            Console.WriteLine("• Thêm delay ngẫu nhiên giữa các thao tác");
            Console.WriteLine("• Sử dụng human-like actions (đã có sẵn)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi khi test fingerprint thực tế: {ex.Message}");
        }
        finally
        {
            // Đóng tất cả drivers
            foreach (var driver in drivers)
            {
                try
                {
                    driver.Quit();
                    driver.Dispose();
                }
                catch { }
            }
            Console.WriteLine("\n✅ Đã đóng tất cả Chrome instances");
        }
    }
}
