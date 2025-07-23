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
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

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
            options.AddExcludedArgument("enable-automation");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            IWebDriver driver = new ChromeDriver(options);
            driver.Navigate().GoToUrl(signupUrl);

            Thread.Sleep(5000);

            ((IJavaScriptExecutor)driver).ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");

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

            string phoneNumber2FA = null; // Khai báo ngoài class Program
            await HandleRequestSever(driver, email, password, phoneNumber2FA);
            Console.WriteLine($"✅ Tài khoản Gmail: {email}, Password: {password}");

            // Nếu có popup recovery email thì ấn Skip
            ClickSkipRecoveryEmailButton(driver);
            // Ấn nút Next ở màn hình Review account info nếu xuất hiện
            ClickReviewNextButton(driver);
            // Ấn nút I agree ở màn hình Privacy and Terms nếu xuất hiện
            ClickPrivacyAgreeButton(driver);
            // Ấn nút Confirm nếu xuất hiện popup cá nhân hóa
            ClickConfirmPersonalizationButton(driver);
            // Truy cập vào trang bảo mật 2FA
            GoToGoogle2FA(driver);
            // Ấn nút Add phone number nếu xuất hiện
            ClickAddPhoneNumberButton(driver);
            // Điền số điện thoại đã thuê vào popup 2FA và ấn Next
            if (!string.IsNullOrEmpty(phoneNumber2FA))
                Fill2FAPhoneAndNext(driver, phoneNumber2FA);
            // Nếu có popup xác nhận số điện thoại thì ấn Save
            ClickConfirmPhoneSaveButton(driver);
            // Đợi và ấn nút Done nếu xuất hiện
            ClickDoneButtonAfterPhoneVerify(driver);
            // Truy cập vào Authenticator app và click setup
            GoToAuthenticatorAppAndSetup(driver);
            // Đợi popup QR và click Can't scan it
            ClickCantScanItLink(driver);
            // Lấy key, tạo mã OTP, lưu lại và ấn Next
            string authKey = ExtractAuthenticatorKey(driver);
            if (!string.IsNullOrEmpty(authKey))
            {
                string otpCode = GenerateOtpCode(authKey);
                // Lưu key và mã OTP ra file hoặc log (tuỳ nhu cầu)
                File.AppendAllText("authenticator_keys.txt", $"Key: {authKey} | OTP: {otpCode}\n");
                Console.WriteLine($"Đã lưu key và mã OTP vào file authenticator_keys.txt");
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
                if (!string.IsNullOrEmpty(authKey))
                {
                    string otpCode = GenerateOtpCode(authKey);
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

    static async Task HandleRequestSever(IWebDriver driver, string userNameParam, string passwordParam, string phoneNumber2FA)
    {
        phoneNumber2FA = null;
        var client = new HttpClient();
        string url = "https://dailyotp.com/api/rent-number?appBrand=Google / Gmail / Youtube&countryCode=US&serverName=Server 1&api_key=4cdba4a83cb5e06bf4f81bb491f7a434vUo9b9CciGZ1VPPjbDcj";

        HttpResponseMessage response = await client.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            string jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OtpResponse>(jsonResponse);

            Console.WriteLine($"Số thuê: {result.data.phoneNumber}");
            Console.WriteLine($"transId: {result.data.transId}");

            try
            {
                IWebElement phoneInput = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                    .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@type='tel']")));

                phoneInput.Clear();
                phoneInput.SendKeys(result.data.phoneNumber);
                Thread.Sleep(1000);

                ClickNextButton(driver);

                await HandleGetCode(driver, result.data.transId, userNameParam, passwordParam);
                phoneNumber2FA = result.data.phoneNumber; // Gán giá trị cho biến toàn cục
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Lỗi khi nhập số điện thoại hoặc nhấn Next: " + ex.Message);
            }
        }
        else
        {
            Console.WriteLine("❌ Lỗi khi gọi API.");
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

    static void ClickAddPhoneNumberButton(IWebDriver driver)
    {
        try
        {
            // Tìm nút Add phone number trên trang 2FA
            IWebElement addPhoneBtn = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//button[.//span[text()='Add phone number']]")));
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", addPhoneBtn);
            Thread.Sleep(200);
            js.ExecuteScript("arguments[0].click();", addPhoneBtn);
            Thread.Sleep(1000);
            Console.WriteLine("✅ Đã ấn nút Add phone number trên trang 2FA");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Không ấn được nút Add phone number: {ex.Message}");
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
            // Đợi popup QR xuất hiện và tìm link Can't scan it?
            IWebElement cantScanLink = new WebDriverWait(driver, TimeSpan.FromSeconds(20))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//a[contains(text(), \"Can't scan it\")]")));
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", cantScanLink);
            Thread.Sleep(200);
            js.ExecuteScript("arguments[0].click();", cantScanLink);
            Thread.Sleep(1000);
            Console.WriteLine("✅ Đã click vào link Can't scan it?");
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
            // Lấy text popup chứa key
            IWebElement popup = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//div[contains(text(),'Enter your email address and this key')]")));
            string popupText = popup.Text;
            // Regex tìm key (dạng nhiều nhóm ký tự, có thể có khoảng trắng)
            var match = System.Text.RegularExpressions.Regex.Match(popupText, @"([a-z0-9]{4,}\s+){3,}[a-z0-9]{4,}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string key = match.Value.Replace(" ", "").Trim();
                Console.WriteLine($"✅ Đã lấy key Authenticator: {key}");
                return key;
            }
            else
            {
                Console.WriteLine("❌ Không tìm thấy key Authenticator trong popup!");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi khi lấy key Authenticator: {ex.Message}");
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

    // Hàm mô phỏng thao tác người dùng thật: di chuột, rê chuột, cuộn trang, click linh tinh, delay ngẫu nhiên
    static void HumanLikeActions(IWebDriver driver)
    {
        Random rnd = new Random();
        int actionCount = rnd.Next(2, 5); // Số lần thực hiện hành động linh tinh
        int width = driver.Manage().Window.Size.Width;
        int height = driver.Manage().Window.Size.Height;

        for (int i = 0; i < actionCount; i++)
        {
            int actionType = rnd.Next(0, 4);
            switch (actionType)
            {
                case 0: // Di chuột đến vị trí ngẫu nhiên
                    int x = rnd.Next(0, width);
                    int y = rnd.Next(0, height);
                    try
                    {
                        var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                        actions.MoveByOffset(x, y).Perform();
                    }
                    catch { }
                    break;
                case 1: // Cuộn trang ngẫu nhiên
                    int scrollY = rnd.Next(50, 400);
                    ((IJavaScriptExecutor)driver).ExecuteScript($"window.scrollBy(0, {scrollY});");
                    break;
                case 2: // Click linh tinh vào vị trí ngẫu nhiên (tránh click vào các trường nhập liệu)
                    var body = driver.FindElement(By.TagName("body"));
                    try
                    {
                        var actions = new OpenQA.Selenium.Interactions.Actions(driver);
                        actions.MoveToElement(body, rnd.Next(0, width), rnd.Next(0, height)).Click().Perform();
                    }
                    catch { }
                    break;
                case 3: // Dừng lại ngẫu nhiên (giả vờ đọc)
                    int pause = rnd.Next(800, 2500);
                    Thread.Sleep(pause);
                    break;
            }
            // Delay ngẫu nhiên giữa các hành động
            Thread.Sleep(rnd.Next(300, 1200));
        }
        // Dừng lại lâu hơn ở cuối
        Thread.Sleep(rnd.Next(1000, 2500));
    }
}
