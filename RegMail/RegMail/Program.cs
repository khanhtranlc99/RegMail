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

            ClickNextButton(driver);
            string firstName = FillFirstName(driver);
            string lastName = FillLastName(driver);
            ClickNextButton(driver);
            FillDayAndYearNew(driver);
            FillMonthNew(driver);
            FillGenderNew(driver);
            ClickNextButton(driver);
     
            string email = FillUsername(driver, firstName, lastName);
            string password = FillPassword(driver);
            ClickNextButton(driver);

            await HandleRequestSever(driver, email, password);

            Console.WriteLine($"✅ Tài khoản Gmail: {email}, Password: {password}");
        }
    }

    static string FillFirstName(IWebDriver driver)
    {
        string[] firstNames = { "Acacia", "Adela", "Blanche", "Bridget", "Donna" };
        Random random = new Random();
        string randomFirstName = firstNames[random.Next(firstNames.Length)];

        IWebElement firstNameField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='First name']")));
        firstNameField.SendKeys(randomFirstName);

        return randomFirstName;
    }

    static string FillLastName(IWebDriver driver)
    {
        string[] lastNames = { "Emery", "Fergal", "Augustus", "Cadell", "Garrick" };
        Random random = new Random();
        string randomLastName = lastNames[random.Next(lastNames.Length)];

        IWebElement lastNameField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='Last name (optional)']")));
        lastNameField.SendKeys(randomLastName);

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
       
            IWebElement usernameField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='Username']")));
            usernameField.Clear();
            usernameField.SendKeys(username);

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
        passwordField.SendKeys(password);

        IWebElement confirmPasswordField = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath("//input[@aria-label='Confirm']")));
        confirmPasswordField.SendKeys(password);

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
            js.ExecuteScript("arguments[0].click();", nextButton);

            Thread.Sleep(1000);
        }
        catch (Exception) { }
    }

    static void FillDayAndYearNew(IWebDriver driver)
    {
        try
        {
            Random random = new Random();
            int day = random.Next(1, 29);
            int year = random.Next(1985, 2010);

            IWebElement dayField = driver.FindElement(By.XPath("//input[@aria-label='Day']"));
            dayField.SendKeys(day.ToString());

            IWebElement yearField = driver.FindElement(By.XPath("//input[@aria-label='Year']"));
            yearField.SendKeys(year.ToString());

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
            Random random = new Random();
            string[] months = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
            string month = months[random.Next(months.Length)];

            IWebElement monthDropdown = driver.FindElement(By.Id("month"));
            SelectElement selectMonth = new SelectElement(monthDropdown);
            selectMonth.SelectByText(month);

            Console.WriteLine("Đã chọn tháng: " + month);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi chọn tháng: " + ex.Message);
        }
    }

    static void FillGenderNew(IWebDriver driver)
    {
        try
        {
            string[] genders = { "Male", "Female" };
            Random random = new Random();
            string gender = genders[random.Next(genders.Length)];

            IWebElement genderDropdown = driver.FindElement(By.Id("gender"));
            SelectElement selectGender = new SelectElement(genderDropdown);
            selectGender.SelectByText(gender);

            Console.WriteLine("Đã chọn giới tính: " + gender);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi chọn giới tính: " + ex.Message);
        }
    }

    static async Task HandleRequestSever(IWebDriver driver, string userNameParam, string passwordParam)
    {
        var client = new HttpClient();
        string url = "https://dailyotp.com/api/rent-number?appBrand=Google / Gmail / Youtube&countryCode=US&serverName=Server 1&api_key=74f6b03afb90f76b7355081687d70faf5mUcE7lFK4xIZKCBOVS3";

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
        string url = $"https://dailyotp.com/api/get-messages?transId={transId}&api_key=74f6b03afb90f76b7355081687d70faf5mUcE7lFK4xIZKCBOVS3";
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
        string filePath = @"D:\RegMail\TestWriteInExel\ExcelDataGmailData.xlsx";

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
}
