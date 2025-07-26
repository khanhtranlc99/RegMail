using System;
using System.Diagnostics;
using System.Text;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Threading;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using System.IO;
using System.Linq;
using SeleniumExtras.WaitHelpers;
using OtpNet;
using RegMail;

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
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        // Khởi tạo Proxy Manager
        _proxyManager = new ProxyManager();
        
        // Hiển thị menu chọn chế độ proxy
        await ShowProxyMenu();

        Console.Write("Nhập số lượng tab Chrome cần mở: ");
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

            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--guest");
            options.AddArgument("--new-window");
            options.AddArgument("--window-size=" + width + "," + height);
            options.AddArgument("--window-position=" + posX + "," + posY);
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
            ClickNextButton(driver);

            await HandleRequestSever(driver, email, password);
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
            // Truy cập vào Authenticator app và click setup
            GoToAuthenticatorAppAndSetup(driver);
            // Đợi popup QR và click Can't scan it
            ClickCantScanItLink(driver);
            // Lấy key, tạo mã OTP, lưu lại và ấn Next
            string authKeyWithSpaces = ExtractAuthenticatorKey(driver);
            if (!string.IsNullOrEmpty(authKeyWithSpaces))
            {
                // Loại bỏ khoảng trắng để tạo OTP (thư viện OtpNet yêu cầu key không có khoảng trắng)
                string authKeyWithoutSpaces = authKeyWithSpaces.Replace(" ", "");
                string otpCode = GenerateOtpCode(authKeyWithoutSpaces);
                Console.WriteLine($"✅ Đã lưu key và mã OTP vào file authenticator_keys.txt");
            }
            else
            {
                Console.WriteLine("❌ Không thể lấy key Authenticator!");
            }
            // Ấn nút Next
            try
            {
                IWebElement nextBtn = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                    .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Next']]")));
                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", nextBtn);
                Thread.Sleep(200);
                js.ExecuteScript("arguments[0].click();", nextBtn);
                Thread.Sleep(1000);
                Console.WriteLine("✅ Đã ấn nút Next sau khi lấy key Authenticator");
                // Sau khi ấn Next, điền mã OTP và ấn Verify
                if (!string.IsNullOrEmpty(authKeyWithSpaces))
                {
                    // Sử dụng lại key không có khoảng trắng để tạo OTP
                    string authKeyWithoutSpaces = authKeyWithSpaces.Replace(" ", "");
                    string otpCode = GenerateOtpCode(authKeyWithoutSpaces);
                    FillAuthenticatorCodeAndVerify(driver, otpCode);
                    // Quay lại và xóa số điện thoại 2FA
                    Remove2FAPhoneNumber(driver);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Không ấn được nút Next sau khi lấy key Authenticator: {ex.Message}");
            }

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
            // Tìm ô nhập cho 'Create a Gmail address'
            IWebElement usernameField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='Create a Gmail address']")));
            usernameField.Clear();
            // Nhập từng ký tự một
            HumanType(usernameField, username);

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
        if (response.IsSuccessStatusCode)
        {
            string jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OtpResponse>(jsonResponse);

            phoneNumber2FA = result.data.phoneNumber;
            Console.WriteLine($"Số thuê: {result.data.phoneNumber}");
            Console.WriteLine($"Số đã thuê: {phoneNumber2FA}");
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
            Console.WriteLine("❌ Lỗi khi gọi API.");
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
                        HandleWriteExcel(userNameParam, passwordParam);
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
            Console.WriteLine("⚠️ Quá thời gian chờ mã OTP.");
        }
    }

    static void HandleWriteExcel(string userNameParam, string passwordParam)
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
                worksheet.Cell(currentRow, 1).Value = userNameParam;
                worksheet.Cell(currentRow, 2).Value = passwordParam;

                // Lưu file
                workbook.Save();
                Console.WriteLine("✅ Đã ghi dữ liệu vào Excel!");
              
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine("❌ Không thể ghi vào file Excel. Có thể đang mở file. Chi tiết: " + ex.Message);
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
            Console.WriteLine("🔍 Đang tìm nút Add phone number...");
            
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
                    Console.WriteLine($"🔍 Thử selector: {selector}");
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
                Console.WriteLine("🖱️ Thử click bằng JavaScript...");
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
            
            // Thử nhiều cách tìm link khác nhau
            IWebElement cantScanLink = null;
            var linkSelectors = new[]
            {
                // Target chính xác button có jsname="Pr7Yme"
                "//button[@jsname='Pr7Yme']",
                // Target span có jsname="V67aGc" chứa text "Can't scan it?"
                "//span[@jsname='V67aGc']",
                // Target button chứa span có text "Can't scan it?"
                "//button[.//span[contains(text(), \"Can't scan it?\")]]",
                "//button[.//span[contains(text(), 'Can\\'t scan it?')]]",
                // Fallback selectors
                "//button[contains(text(), \"Can't scan it?\")]",
                "//button[contains(text(), 'Can\\'t scan it?')]",
                "//button[contains(text(), 'scan')]",
                "//button[contains(text(), 'Scan')]",
                "//span[contains(text(), 'scan')]/parent::button",
                "//span[contains(text(), 'Scan')]/parent::button",
                "//*[contains(text(), 'scan') and (self::a or self::button)]",
                "//*[contains(text(), 'Scan') and (self::a or self::button)]"
            };
            
            foreach (var selector in linkSelectors)
            {
                try
                {
                    Console.WriteLine($"🔍 Thử selector: {selector}");
                    cantScanLink = new WebDriverWait(driver, TimeSpan.FromSeconds(3))
                        .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath(selector)));
                    
                    if (cantScanLink != null)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    continue;
                }
            }
            
            
            if (cantScanLink != null)
            {
                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                    // Thử cách cuối cùng - click vào tọa độ
                    try
                    {
                        Console.WriteLine("🖱️ Thử click vào tọa độ...");
                        var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                        actions.MoveToElement(cantScanLink).Click().Perform();
                        Thread.Sleep(1000);
                        Console.WriteLine("✅ Đã click vào tọa độ thành công");
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"❌ Tất cả phương pháp click đều thất bại: {ex2.Message}");
                    }
                
                 // Thử cách cuối cùng - di chuột đến tọa độ cụ thể và click
                    try
                    {
                        Console.WriteLine("🖱️ Thử di chuột đến tọa độ cụ thể...");
                        var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                        
                        // Lấy vị trí và kích thước của element
                        var location = cantScanLink.Location;
                        var size = cantScanLink.Size;
                        Console.WriteLine($"📍 Element location: {location}, size: {size}");
                        
                        // Di chuột đến giữa element
                        var centerX = location.X + (size.Width / 2);
                        var centerY = location.Y + (size.Height / 2);
                        Console.WriteLine($"🎯 Click tại tọa độ: ({centerX}, {centerY})");
                        
                        // Di chuột đến tọa độ và click
                        actions.MoveByOffset(centerX, centerY).Click().Perform();
                        Thread.Sleep(1000);
                        Console.WriteLine("✅ Đã click tại tọa độ cụ thể thành công");
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"⚠️ Click tọa độ thất bại: {ex2.Message}");
                        
                        // Thử cách cuối cùng - hover trước rồi click
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
            }
            else
            {
                Console.WriteLine("🔍 Tìm kiếm tất cả button có chứa 'scan'...");
                try
                {
                    var allButtons = driver.FindElements(By.TagName("button"));
                    Console.WriteLine($"📝 Tìm thấy {allButtons.Count} button trên trang");

                    foreach (var button in allButtons)
                    {
                        try
                        {
                            string buttonText = button.Text.ToLower().Trim();
                            string buttonJsName = button.GetAttribute("jsname");
                            Console.WriteLine($"🔍 Button: Text='{buttonText}', jsname='{buttonJsName}'");

                            if (buttonText.Contains("scan") || buttonText.Contains("can't") || buttonText.Contains("cant") ||
                                buttonJsName == "Pr7Yme")
                            {
                                cantScanLink = button;
                                Console.WriteLine($"✅ Tìm thấy button phù hợp: {buttonText} (jsname: {buttonJsName})");
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
            Console.WriteLine("🔍 Đang tìm popup chứa key Authenticator...");
            
            // Đợi popup xuất hiện và tìm element chứa key
            IWebElement popup = null;
            string popupText = "";
            
            // Thử nhiều cách tìm popup khác nhau
            var popupSelectors = new[]
            {
                "//div[contains(text(),'Enter your email address and this key')]",
                "//div[contains(text(),'this key')]",
                "//div[contains(text(),'setup key')]",
                "//div[contains(@class,'dialog') and contains(text(),'key')]",
                "//div[contains(@class,'popup') and contains(text(),'key')]",
                "//div[contains(@class,'modal') and contains(text(),'key')]",
            };
            
            foreach (var selector in popupSelectors)
            {
                try
                {
                    Console.WriteLine($"🔍 Thử selector: {selector}");
                    popup = new WebDriverWait(driver, TimeSpan.FromSeconds(3))
                        .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath(selector)));
                    
                    if (popup != null)
                    {
                        popupText = popup.Text;
                        Console.WriteLine($"✅ Tìm thấy popup với selector: {selector}");
                        Console.WriteLine($"📄 Nội dung popup: {popupText}");
                        break;
                    }
                }
                catch
                {
                    Console.WriteLine($"⚠️ Selector không tìm thấy: {selector}");
                    continue;
                }
            }
            
            // Nếu vẫn không tìm thấy, thử tìm tất cả div có chứa text
            if (popup == null)
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
                            if (divText.Contains("key") && (divText.Contains("f5b6") || divText.Contains("lv3k") || divText.Contains("vbah")))
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
            
            if (popup == null || string.IsNullOrEmpty(popupText))
            {
                Console.WriteLine("❌ Không tìm thấy popup chứa key Authenticator!");
                Console.WriteLine("🔍 Đang in ra toàn bộ HTML để debug...");
                try
                {
                    string pageSource = driver.PageSource;
                    Console.WriteLine($"📄 Độ dài HTML: {pageSource.Length} ký tự");
                    
                    // Tìm kiếm các từ khóa liên quan
                    if (pageSource.Contains("key") || pageSource.Contains("f5b6") || pageSource.Contains("lv3k"))
                    {
                        Console.WriteLine("✅ Tìm thấy từ khóa liên quan trong HTML");
                        // In ra phần HTML chứa từ khóa
                        int keyIndex = pageSource.IndexOf("key", StringComparison.OrdinalIgnoreCase);
                        if (keyIndex >= 0 && keyIndex < pageSource.Length - 500)
                        {
                            Console.WriteLine($"📄 HTML xung quanh 'key': {pageSource.Substring(keyIndex, 500)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Không thể lấy HTML: {ex.Message}");
                }
                return null;
            }
            
            // Cải thiện regex để tìm key chính xác hơn
            // Pattern cho key dạng: f5b6 lv3k vbah k5dq bo5f be6j 4vs5 2h36
            var patterns = new[]
            {
                @"([a-z0-9]{4}\s+){7}[a-z0-9]{4}", // Pattern chính xác cho key 32 ký tự
                @"([a-z0-9]{4,}\s+){3,}[a-z0-9]{4,}", // Pattern linh hoạt hơn
                @"[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}", // Pattern cụ thể
                @"\b[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\s+[a-z0-9]{4}\b" // Pattern với word boundary
            };
            
            foreach (var pattern in patterns)
            {
                try
                {
                    Console.WriteLine($"🔍 Thử pattern: {pattern}");
                    var match = System.Text.RegularExpressions.Regex.Match(popupText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        string keyWithSpaces = match.Value.Trim();
                        string keyWithoutSpaces = keyWithSpaces.Replace(" ", "");
                        Console.WriteLine($"✅ Đã lấy key Authenticator: {keyWithoutSpaces}");
                        Console.WriteLine($"📝 Key gốc với khoảng trắng: {keyWithSpaces}");
                        
                        // Kiểm tra độ dài key (thường là 32 ký tự)
                        if (keyWithoutSpaces.Length == 32)
                        {
                            Console.WriteLine($"✅ Key có độ dài hợp lệ: {keyWithoutSpaces.Length} ký tự");
                            return keyWithSpaces; // Trả về key với khoảng trắng
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Key có độ dài không chuẩn: {keyWithoutSpaces.Length} ký tự");
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
            IWebElement verifyBtn = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Verify']]")));
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
        try
        {
            // Quay lại trang 2FA phone
            string url2FA = "https://myaccount.google.com/two-step-verification/phone-numbers";
            driver.Navigate().GoToUrl(url2FA);
            Thread.Sleep(3000);
            // Tìm và click vào biểu tượng thùng rác để xóa số điện thoại
            IWebElement trashBtn = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[@aria-label='Delete phone number' or @aria-label='Remove phone number' or .//span[@class='material-icons' and text()='delete']]")));
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", trashBtn);
            Thread.Sleep(200);
            js.ExecuteScript("arguments[0].click();", trashBtn);
            Thread.Sleep(1000);
            Console.WriteLine("✅ Đã ấn vào biểu tượng thùng rác để xóa số điện thoại 2FA");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không xóa được số điện thoại 2FA: {ex.Message}");
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
            Console.WriteLine("7. Bắt đầu tạo tài khoản Gmail");
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
        Random rnd = new Random();
        int actionCount = rnd.Next(3, 7); // Tăng số lần thực hiện hành động
        int width = driver.Manage().Window.Size.Width;
        int height = driver.Manage().Window.Size.Height;

        // Thêm thao tác mô phỏng người dùng thật hơn
        for (int i = 0; i < actionCount; i++)
        {
            int actionType = rnd.Next(0, 8); // Tăng số loại hành động
            switch (actionType)
            {
                case 0: // Di chuột đến vị trí ngẫu nhiên với tốc độ thay đổi
                    int x = rnd.Next(0, width);
                    int y = rnd.Next(0, height);
                    try
                    {
                        var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                        actions.MoveByOffset(x, y).Perform();
                        Thread.Sleep(rnd.Next(100, 500)); // Delay ngẫu nhiên
                    }
                    catch { }
                    break;
                case 1: // Cuộn trang ngẫu nhiên với tốc độ khác nhau
                    int scrollY = rnd.Next(50, 400);
                    int scrollSpeed = rnd.Next(100, 300);
                    ((IJavaScriptExecutor)driver).ExecuteScript($"window.scrollBy({{top: {scrollY}, left: 0, behavior: 'smooth'}});");
                    Thread.Sleep(scrollSpeed);
                    break;
                case 2: // Click linh tinh vào vị trí ngẫu nhiên (tránh click vào các trường nhập liệu)
                    try
                    {
                        var body = driver.FindElement(By.TagName("body"));
                        var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                        actions.MoveToElement(body, rnd.Next(0, width), rnd.Next(0, height)).Click().Perform();
                    }
                    catch { }
                    break;
                case 3: // Dừng lại ngẫu nhiên (giả vờ đọc)
                    int pause = rnd.Next(800, 3000);
                    Thread.Sleep(pause);
                    break;
                case 4: // Di chuột theo đường cong (mô phỏng người dùng thật)
                    try
                    {
                        var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                        for (int j = 0; j < 5; j++)
                        {
                            int curveX = rnd.Next(0, width);
                            int curveY = rnd.Next(0, height);
                            actions.MoveByOffset(curveX, curveY).Perform();
                            Thread.Sleep(rnd.Next(50, 200));
                        }
                    }
                    catch { }
                    break;
                case 5: // Cuộn ngang ngẫu nhiên
                    int scrollX = rnd.Next(-100, 100);
                    ((IJavaScriptExecutor)driver).ExecuteScript($"window.scrollBy({{top: 0, left: {scrollX}, behavior: 'smooth'}});");
                    Thread.Sleep(rnd.Next(200, 600));
                    break;
                case 6: // Hover chuột trên các element ngẫu nhiên
                    try
                    {
                        var elements = driver.FindElements(By.TagName("div"));
                        if (elements.Count > 0)
                        {
                            var randomElement = elements[rnd.Next(0, Math.Min(elements.Count, 10))];
                            var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                            actions.MoveToElement(randomElement).Perform();
                            Thread.Sleep(rnd.Next(300, 800));
                        }
                    }
                    catch { }
                    break;
                case 7: // Thay đổi focus ngẫu nhiên
                    try
                    {
                        ((IJavaScriptExecutor)driver).ExecuteScript("document.activeElement.blur();");
                        Thread.Sleep(rnd.Next(200, 500));
                        ((IJavaScriptExecutor)driver).ExecuteScript("document.body.focus();");
                    }
                    catch { }
                    break;
            }
            // Delay ngẫu nhiên giữa các hành động
            Thread.Sleep(rnd.Next(200, 800));
        }
        // Dừng lại lâu hơn ở cuối
        Thread.Sleep(rnd.Next(1500, 3500));
    }
}
