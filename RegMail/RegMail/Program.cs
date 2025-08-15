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

        Console.Write("\nNhập số lượng tab Chrome cần mở: ");
        if (!int.TryParse(Console.ReadLine(), out int tabCount) || tabCount <= 0)
        {
            Console.WriteLine("Số lượng tab không hợp lệ!");
            return;
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
            
            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--guest");
            options.AddArgument("--new-window");
            options.AddArgument("--window-size=" + width + "," + height);
            options.AddArgument("--window-position=" + posX + "," + posY);
            
            // Thêm các tùy chọn để tránh verification (MỚI)
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            options.AddArgument("--disable-ipc-flooding-protection");
            options.AddArgument("--disable-background-timer-throttling");
            options.AddArgument("--disable-backgrounding-occluded-windows");
            options.AddArgument("--disable-renderer-backgrounding");
            options.AddArgument("--disable-field-trial-config");
            options.AddArgument("--disable-back-forward-cache");
            options.AddArgument("--enable-features=NetworkService,NetworkServiceInProcess");
            options.AddArgument("--disable-component-extensions-with-background-pages");
            options.AddArgument("--disable-default-apps");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--force-color-profile=srgb");
            options.AddArgument("--metrics-recording-only");
            options.AddArgument("--no-first-run");
            options.AddArgument("--password-store=basic");
            options.AddArgument("--use-mock-keychain");
            options.AddArgument("--disable-component-update");
            options.AddArgument("--disable-domain-reliability");
            options.AddArgument("--disable-sync");
            
            // Ẩn các dấu hiệu automation
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);
            
            // Thêm user agent để tự nhiên hơn
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
            // Tạo fingerprint từ danh sách profile có sẵn cho mỗi tab
            var fingerprint = FingerprintManager.GetRandomProfile();
            FingerprintManager.ConfigureChromeOptions(options, fingerprint);
            
            Console.WriteLine($"\n🔄 Tab {i + 1}: Sử dụng fingerprint '{fingerprint.ProfileName}'");
            
            // Cấu hình proxy cho Chrome
            var proxy = _proxyManager.GetNextProxy();
            if (proxy != null)
            {
               _proxyManager.ConfigureChromeOptions(options, proxy);
            }
            
            IWebDriver driver = new ChromeDriver(options);
            driver.Navigate().GoToUrl(signupUrl);

            Thread.Sleep(5000);

            // Inject JavaScript để thay đổi fingerprint và tránh phát hiện automation
            InjectAntiDetectionScripts(driver);

            // Thao tác người dùng thật trước khi điền form
            HumanLikeActions(driver);

            string firstName = FillFirstName(driver);
            string lastName = FillLastName(driver);
            ClickNextButton(driver);
            HumanLikeActions(driver);
            FillDayAndYearNew(driver);
            FillMonthNew(driver);
            FillGenderNew(driver);
            ClickNextButton(driver);
            HumanLikeActions(driver);
            ClickNextButton(driver);
            RandomDelay();
            ClickCreateOwnGmail(driver);
            RandomDelay();

            string email = FillUsername(driver, firstName, lastName);
            string password = FillPassword(driver);
            
            // Lưu Gmail và password vào biến global
            currentGmail = email;
            currentPassword = password;
            
            ClickNextButton(driver);

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
            ClickPrivacyAgreeButton(driver);
            ClickConfirmPersonalizationButton(driver);
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

    // Hàm nhập từng ký tự một với delay ngẫu nhiên
    static void HumanType(IWebElement element, string text)
    {
        Random randomDelay = new Random();
        foreach (char c in text)
        {
            element.SendKeys(c.ToString());
            Thread.Sleep(randomDelay.Next(80, 180));
        }
    }

    static string FillFirstName(IWebDriver driver)
    {
        string[] firstNames = { "Acacia", "Adela", "Blanche", "Bridget", "Donna" };
        Random random = new Random();
        string randomFirstName = firstNames[random.Next(firstNames.Length)];

        IWebElement firstNameField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='First name']")));
        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
        js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", firstNameField);
        RandomDelay(200, 400);
        firstNameField.Click();
        RandomDelay(100, 200);
        HumanType(firstNameField, randomFirstName);

        return randomFirstName;
    }

    static string FillLastName(IWebDriver driver)
    {
        string[] lastNames = { "Emery", "Fergal", "Augustus", "Cadell", "Garrick" };
        Random random = new Random();
        string randomLastName = lastNames[random.Next(lastNames.Length)];

        IWebElement lastNameField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='Last name (optional)']")));
        // Nhập từng ký tự một
        lastNameField.Clear();
        HumanType(lastNameField, randomLastName);

        return randomLastName;
    }

    static string FillUsername(IWebDriver driver, string firstName, string lastName)
    {
        int x = 1;
        bool success = false;
        string username = "";
        while (!success && x < 100)
        {
            username = firstName.ToLower() + "90" + lastName.ToLower() + x;
            
            try
            {
                username = firstName.ToLower() + "90" + lastName.ToLower() + x;
                // Tìm ô nhập cho 'Create a Gmail address'
                IWebElement usernameField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                    .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='Create a Gmail address']")));
                usernameField.Clear();
                // Nhập từng ký tự một
                HumanType(usernameField, username);

                ClickNextButton(driver);
                Thread.Sleep(2000);
            }
            catch (WebDriverTimeoutException)
            {
                // Nếu không tìm thấy ô "Create a Gmail address", điền trực tiếp vào ô Username
                IWebElement usernameField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                    .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='Username']")));
                usernameField.Clear();
                HumanType(usernameField, username);
            }

            ClickNextButton(driver);
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
        HumanType(passwordField, password);

        IWebElement confirmPasswordField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='Confirm']")));
        confirmPasswordField.Clear();
        HumanType(confirmPasswordField, password);

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

    static void ClickNextButton(IWebDriver driver)
    {
        try
        {
            IWebElement nextButton = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//span[contains(text(), 'Next')]")));

            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            // Scroll button vào view nếu cần
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", nextButton);
            Thread.Sleep(200);
            js.ExecuteScript("arguments[0].click();", nextButton);
            Thread.Sleep(1000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi click Next: {ex.Message}");
            // Thử lại một lần nữa nếu gặp lỗi
            try
            {
                IWebElement nextButton = driver.FindElement(By.XPath("//span[contains(text(), 'Next')]"));
                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", nextButton);
                Thread.Sleep(200);
                js.ExecuteScript("arguments[0].click();", nextButton);
                Thread.Sleep(1000);
            }
            catch (Exception retryEx)
            {
                Console.WriteLine($"❌ Vẫn không click được Next: {retryEx.Message}");
            }
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

    static void ClickCreateOwnGmail(IWebDriver driver)
    {
        try
        {
            // Tìm element chứa text "Create your own Gmail address"
            var createOwnOption = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(d => d.FindElement(By.XPath("//*[contains(text(), 'Create your own Gmail address')]")));

            // Click vào option này (thường là label hoặc span)
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].click();", createOwnOption);

            Console.WriteLine("✅ Đã chọn 'Create your own Gmail address'");
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Không click được 'Create your own Gmail address': " + ex.Message);
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
                Console.WriteLine("✅ Click thành công bằng JavaScript");
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
                    Console.WriteLine("🖱️ Thử click bằng Actions...");
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
                    Console.WriteLine("🖱️ Thử click thường...");
                    addPhoneBtn.Click();
                    clickSuccess = true;
                    Console.WriteLine("✅ Click thành công bằng Selenium");
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
            Console.WriteLine("✅ Đã ấn nút Done sau khi xác nhận số điện thoại 2FA");
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
            Console.WriteLine("🔍 Đang tìm link 'Can't scan it?'...");
            
            // Đợi một chút để đảm bảo popup QR code đã hiển thị
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
                    Console.WriteLine("🖱️ Thử click trực tiếp...");
                    cantScanLink.Click();
                    Thread.Sleep(1000);
                    Console.WriteLine("✅ Đã click trực tiếp thành công");
                    return;
                }
                catch (Exception ex1)
                {
                    Console.WriteLine($"⚠️ Click trực tiếp thất bại: {ex1.Message}");
                }

                // Thử JavaScript click
                try
                {
                    Console.WriteLine("🖱️ Thử JavaScript click...");
                    js.ExecuteScript("arguments[0].click();", cantScanLink);
                    Thread.Sleep(1000);
                    Console.WriteLine("✅ Đã JavaScript click thành công");
                    return;
                }
                catch (Exception ex1)
                {
                    Console.WriteLine($"⚠️ JavaScript click thất bại: {ex1.Message}");
                }

                // Thử Actions click
                try
                {
                    Console.WriteLine("🖱️ Thử Actions click...");
                    var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                    actions.MoveToElement(cantScanLink).Click().Perform();
                    Thread.Sleep(1000);
                    Console.WriteLine("✅ Đã Actions click thành công");
                    return;
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"⚠️ Actions click thất bại: {ex2.Message}");
                }

                // Thử hover trước rồi click
                try
                {
                    Console.WriteLine("🖱️ Thử hover trước rồi click...");
                    var actions = new OpenQA.Selenium.Interactions.Actions(driver);

                    // Hover vào element trước
                    actions.MoveToElement(cantScanLink).Perform();
                    Thread.Sleep(500);

                    // Sau đó click
                    actions.Click().Perform();
                    Thread.Sleep(1000);
                    Console.WriteLine("✅ Đã hover và click thành công");
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
            Console.WriteLine("🔍 Đang tìm popup chứa key Authenticator...");
            
            // Đợi popup xuất hiện và tìm element chứa key
            IWebElement popup = null;
            string popupText = "";
            
            // Tìm kiếm trực tiếp thẻ strong chứa key
            if (popup == null || !IsValidAuthenticatorKey(popupText))
            {
                Console.WriteLine("🔍 Tìm kiếm trực tiếp thẻ strong chứa key...");
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
                                Console.WriteLine($"✅ Tìm thấy key trong thẻ strong: {strongText}");
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
            Console.WriteLine("9. Bắt đầu tạo tài khoản Gmail");
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
            
            // 1. Ẩn webdriver property (QUAN TRỌNG NHẤT)
            js.ExecuteScript(@"
                Object.defineProperty(navigator, 'webdriver', {
                    get: () => undefined,
                });
                delete navigator.__defineGetter__;
                delete navigator.__defineSetter__;
                delete navigator.__lookupGetter__;
                delete navigator.__lookupSetter__;
            ");

            // 2. Ẩn tất cả automation properties (CỰC QUAN TRỌNG)
            js.ExecuteScript(@"
                // Chrome automation detector
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Array;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Promise;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Symbol;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_JSON;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Object;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Proxy;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Reflect;
                
                // Additional Chrome automation flags
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_AsyncFunction;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Promise_resolve;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_String;
                
                // Remove automation indicators
                ['$chrome_asyncScriptInfo', '$cdc_asdjflasutopfhvcZLmcfl_'].forEach(prop => {
                    delete window[prop];
                });
            ");

            // 3. Tạo chrome object tự nhiên và hoàn chỉnh hơn
            js.ExecuteScript(@"
                if (!window.chrome) {
                    window.chrome = {
                        runtime: {
                            onConnect: null,
                            onMessage: null,
                            connect: function() { return {}; },
                            sendMessage: function() {},
                            onInstalled: { addListener: function() {} }
                        },
                        storage: {
                            local: {
                                get: function() { return Promise.resolve({}); },
                                set: function() { return Promise.resolve(); }
                            }
                        }
                    };
                }
            ");

            // 4. Ẩn tất cả dấu hiệu selenium/automation/testing tools
            js.ExecuteScript(@"
                var toDelete = [
                    'callSelenium', '_Selenium_IDE_Recorder', 'callPhantom', '__phantomas',
                    '__selenium_unwrapped', '__webdriver_evaluate', '__driver_evaluate',
                    '__webdriver_script_function', '__webdriver_script_func', '__webdriver_script_fn',
                    '__fxdriver_evaluate', '__driver_unwrapped', '__webdriver_unwrapped',
                    '__selenium_evaluate', '__fxdriver_unwrapped', '_selenium', 'calledSelenium',
                    '_$webdriver_asynchronousExecute', '__webDriverCssSelector', '__$webdriverAsyncExecutor',
                    'webdriver', '_phantom', '__nightmare', '_selenium_ide_recorder',
                    'domAutomation', 'domAutomationController', '__webdriver_script_function'
                ];
                
                toDelete.forEach(prop => {
                    try {
                        delete window[prop];
                        delete document[prop];
                        delete navigator[prop];
                    } catch(e) {}
                });
            ");

            // 5. Làm giả plugins và mimeTypes tự nhiên hơn
            js.ExecuteScript(@"
                try {
                    const mockPlugins = [
                        { name: 'Chrome PDF Plugin', filename: 'internal-pdf-viewer', description: 'Portable Document Format' },
                        { name: 'Chrome PDF Viewer', filename: 'mhjfbmdgcfjbbpaeojofohoefgiehjai', description: 'Portable Document Format' },
                        { name: 'Native Client', filename: 'internal-nacl-plugin', description: 'Native Client' }
                    ];
                    
                    Object.defineProperty(navigator, 'plugins', {
                        get: function() { return mockPlugins; }
                    });
                    
                    Object.defineProperty(navigator, 'mimeTypes', {
                        get: function() { 
                            return [
                                { type: 'application/pdf', suffixes: 'pdf', description: 'Portable Document Format' }
                            ]; 
                        }
                    });
                } catch(e) {}
            ");

            // 6. Cải thiện permissions API để tự nhiên hơn
            js.ExecuteScript(@"
                try {
                    const originalQuery = window.navigator.permissions.query;
                    window.navigator.permissions.query = function(parameters) {
                        const permissions = {
                            'notifications': 'denied',
                            'geolocation': 'prompt', 
                            'camera': 'prompt',
                            'microphone': 'prompt'
                        };
                        const state = permissions[parameters.name] || 'denied';
                        return Promise.resolve({ state: state });
                    };
                } catch(e) {}
            ");

            // 7. Làm giả các thuộc tính thiết bị tự nhiên hơn (MỚI)
            js.ExecuteScript(@"
                try {
                    // Giả mạo battery API
                    Object.defineProperty(navigator, 'getBattery', {
                        get: function() {
                            return function() {
                                return Promise.resolve({
                                    level: 0.85 + Math.random() * 0.15,
                                    charging: Math.random() > 0.5,
                                    chargingTime: Math.random() * 7200,
                                    dischargingTime: Math.random() * 14400
                                });
                            };
                        }
                    });
                    
                    // Giả mạo connection API
                    Object.defineProperty(navigator, 'connection', {
                        get: function() {
                            return {
                                effectiveType: '4g',
                                downlink: 10,
                                rtt: 50
                            };
                        }
                    });
                } catch(e) {}
            ");

            // 8. Thêm event listeners tự nhiên (MỚI)
            js.ExecuteScript(@"
                try {
                    // Giả mạo mouse movements
                    document.addEventListener('mousemove', function(e) {
                        // Chỉ để có event listener, không cần xử lý gì
                    }, { passive: true });
                    
                    // Giả mạo keyboard events  
                    document.addEventListener('keydown', function(e) {
                        // Chỉ để có event listener, không cần xử lý gì
                    }, { passive: true });
                    
                    // Giả mạo scroll events
                    window.addEventListener('scroll', function(e) {
                        // Chỉ để có event listener, không cần xử lý gì
                    }, { passive: true });
                } catch(e) {}
            ");

            Console.WriteLine("✅ Đã inject thành công các script chống phát hiện automation (cải tiến nâng cao)");
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
                int actionType = _random.Next(0, 6); // Giảm số loại hành động xuống các thao tác tự nhiên nhất
                switch (actionType)
                {
                    case 0: // Cuộn trang nhẹ nhàng (thao tác phổ biến nhất)
                        int scrollY = _random.Next(50, 200);
                        ((IJavaScriptExecutor)driver).ExecuteScript($"window.scrollBy({{top: {scrollY}, left: 0, behavior: 'smooth'}});");
                        Thread.Sleep(_random.Next(800, 2000));
                        break;
                        
                    case 1: // Di chuyển chuột tự nhiên 
                        try
                        {
                            var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                            int x = _random.Next(100, width - 100);
                            int y = _random.Next(100, height - 100);
                            actions.MoveByOffset(x, y).Perform();
                            Thread.Sleep(_random.Next(500, 1500));
                        }
                        catch { }
                        break;
                        
                    case 2: // Dừng lại đọc (giả vờ đọc nội dung)
                        Thread.Sleep(_random.Next(1500, 4000));
                        break;
                        
                    case 3: // Hover trên các element để mô phỏng việc đọc
                        try
                        {
                            var elements = driver.FindElements(By.TagName("span"));
                            if (elements.Count > 0)
                            {
                                var randomElement = elements[_random.Next(0, Math.Min(elements.Count, 5))];
                                var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                                actions.MoveToElement(randomElement).Perform();
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
                        
                    case 5: // Di chuyển chuột tự nhiên nhưng không click
                        try
                        {
                            var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                            int x = _random.Next(100, width - 100);
                            int y = _random.Next(100, height - 100);
                            actions.MoveByOffset(x, y).Perform();
                            Thread.Sleep(_random.Next(300, 800));
                        }
                        catch { }
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
}
