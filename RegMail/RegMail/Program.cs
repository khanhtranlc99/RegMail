﻿using System;
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

            string firstName = FillFirstName(driver);
            string lastName = FillLastName(driver);
            ClickNextButton(driver);
            FillDayAndYearNew(driver);
            FillMonthNew(driver);
            FillGenderNew(driver);
            ClickNextButton(driver);
            ClickNextButton(driver);
            ClickCreateOwnGmail(driver);

            string email = FillUsername(driver, firstName, lastName);
            string password = FillPassword(driver);
            ClickNextButton(driver);

            await HandleRequestSever(driver, email, password);

            Console.WriteLine($"✅ Tài khoản Gmail: {email}, Password: {password}");
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
        // Nhập từng ký tự một
        firstNameField.Clear();
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

    static async Task HandleRequestSever(IWebDriver driver, string userNameParam, string passwordParam)
    {
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
}
