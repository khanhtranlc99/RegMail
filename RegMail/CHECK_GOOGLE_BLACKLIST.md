# 🔍 KIỂM TRA LAPTOP/IP CÓ BỊ GOOGLE ĐÁNH DẤU

## ⚠️ DẤU HIỆU BỊ GOOGLE ĐÁNH DẤU

### Dấu hiệu NGHIÊM TRỌNG:
- 🔴 **100% verify** khi tạo Gmail mới
- 🔴 **CAPTCHA liên tục** khi search Google
- 🔴 **"Unusual traffic"** khi mở Google
- 🔴 Không thể tạo Gmail dù đã đổi proxy
- 🔴 Verify ngay từ bước đầu tiên (điền tên)

### Dấu hiệu CẢNH BÁO:
- ⚠️ Verify rate > 50%
- ⚠️ Thỉnh thoảng có CAPTCHA khi search
- ⚠️ Google đề xuất verify phone số liên tục
- ⚠️ Không thể bỏ qua phone verification

### Dấu hiệu BÌNH THƯỜNG:
- ✅ Verify rate 20-40%
- ✅ Thỉnh thoảng verify 1 lần
- ✅ Không có CAPTCHA khi search bình thường

## 🧪 KIỂM TRA FINGERPRINT (QUAN TRỌNG!)

### Test 1: Bot Detection Test
**Website**: https://bot.sannysoft.com/

**Cách test**:
1. Mở Chrome bình thường (KHÔNG dùng automation)
2. Truy cập: https://bot.sannysoft.com/
3. Kiểm tra kết quả:

```
✅ TỐT - TẤT CẢ GREEN:
- WebDriver: false ✅
- Chrome: present ✅  
- Permissions: consistent ✅
- Plugins: present ✅
- Languages: consistent ✅

🔴 TỆ - CÓ RED:
- WebDriver: true ❌ (NGUY HIỂM!)
- Chrome: missing ❌
- Permissions: inconsistent ❌
- Plugins: 0 ❌
```

### Test 2: Headless Chrome Detection
**Website**: https://intoli.com/blog/not-possible-to-block-chrome-headless/chrome-headless-test.html

**Kết quả tốt**:
```
✅ Chrome Headless: FALSE
✅ Chrome Automation: FALSE
✅ User-Agent: Matches
```

**Kết quả tệ**:
```
❌ Chrome Headless: TRUE
❌ Chrome Automation: TRUE
❌ User-Agent: Mismatch
```

### Test 3: Canvas Fingerprint
**Website**: https://browserleaks.com/canvas

**Cách test**:
1. Chạy test 3 lần
2. So sánh fingerprint hash
3. Nếu **HASH GIỐNG NHAU** 3 lần → ✅ Tốt
4. Nếu **HASH KHÁC NHAU** mỗi lần → ❌ Tệ (Google biết là bot)

### Test 4: WebGL Fingerprint
**Website**: https://browserleaks.com/webgl

**Kiểm tra**:
- Vendor: Phải có (Intel/NVIDIA/AMD/Apple)
- Renderer: Phải có tên GPU cụ thể
- Nếu cả 2 đều "unmasked" hoặc "generic" → ❌ Nghi ngờ

### Test 5: IP Reputation Check
**Website**: https://www.ipqualityscore.com/free-ip-lookup-proxy-vpn-test

**Nhập IP của bạn, kiểm tra**:
```
✅ TỐT:
- Fraud Score: < 50
- Proxy: No
- VPN: No
- Tor: No
- Recent Abuse: No

🔴 TỆ:
- Fraud Score: > 75 (BỊ BLACKLIST!)
- Proxy: Yes (Google biết!)
- Recent Abuse: Yes (IP đã bị report)
```

### Test 6: Google reCAPTCHA Score
**Website**: https://www.google.com/recaptcha/api2/demo

**Cách test**:
1. Tick vào "I'm not a robot"
2. Kiểm tra:
   - ✅ **Tick ngay lập tức** → Score cao (0.7-1.0) → Tốt!
   - ⚠️ **Phải chọn hình** → Score trung bình (0.3-0.7) → Tạm ổn
   - 🔴 **Chọn hình nhiều lần** → Score thấp (< 0.3) → Tệ!
   - 🔴 **"Try again later"** → BỊ BLACKLIST!

## 🔬 TEST VỚI CODE CỦA BẠN

### Tạo file test fingerprint:
Chạy code này để kiểm tra stealth scripts có hoạt động không:

```csharp
// Thêm vào Program.cs - sau dòng InjectAntiDetectionScripts(driver);

static void TestStealthScripts(IWebDriver driver)
{
    IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
    
    Console.WriteLine("\n🧪 TESTING STEALTH SCRIPTS:");
    Console.WriteLine("═══════════════════════════════════════════");
    
    // Test 1: navigator.webdriver
    var webdriver = js.ExecuteScript("return navigator.webdriver;");
    string webdriverResult = webdriver == null ? "undefined ✅" : "true ❌";
    Console.WriteLine($"1. navigator.webdriver: {webdriverResult}");
    
    // Test 2: chrome.runtime
    var chromeRuntime = js.ExecuteScript("return window.chrome && window.chrome.runtime;");
    string chromeResult = chromeRuntime != null ? "present ✅" : "missing ❌";
    Console.WriteLine($"2. chrome.runtime: {chromeResult}");
    
    // Test 3: plugins
    var pluginsLength = js.ExecuteScript("return navigator.plugins.length;");
    string pluginsResult = Convert.ToInt32(pluginsLength) > 0 ? $"{pluginsLength} plugins ✅" : "0 plugins ❌";
    Console.WriteLine($"3. navigator.plugins: {pluginsResult}");
    
    // Test 4: languages
    var languages = js.ExecuteScript("return navigator.languages;");
    Console.WriteLine($"4. navigator.languages: {languages} ✅");
    
    // Test 5: permissions
    bool permissionsOK = true;
    try
    {
        js.ExecuteScript("navigator.permissions.query({name: 'notifications'})");
        Console.WriteLine($"5. permissions.query: working ✅");
    }
    catch
    {
        Console.WriteLine($"5. permissions.query: error ❌");
        permissionsOK = false;
    }
    
    // Test 6: User-Agent
    var userAgent = js.ExecuteScript("return navigator.userAgent;");
    bool hasChrome = userAgent.ToString().Contains("Chrome");
    Console.WriteLine($"6. User-Agent: {(hasChrome ? "valid ✅" : "invalid ❌")}");
    
    // Tổng kết
    Console.WriteLine("═══════════════════════════════════════════");
    
    if (webdriver == null && chromeRuntime != null && Convert.ToInt32(pluginsLength) > 0 && permissionsOK && hasChrome)
    {
        Console.WriteLine("✅ STEALTH SCRIPTS HOẠT ĐỘNG TỐT!");
        Console.WriteLine("   → Khả năng bypass Google detection: CAO");
    }
    else
    {
        Console.WriteLine("❌ STEALTH SCRIPTS CÓ VẤN ĐỀ!");
        Console.WriteLine("   → Khả năng bị Google phát hiện: CAO");
        Console.WriteLine("   → Cần kiểm tra lại code!");
    }
    Console.WriteLine("═══════════════════════════════════════════\n");
}
```

**Cách dùng**:
```csharp
// Trong Main(), sau dòng 236:
InjectAntiDetectionScripts(driver);
TestStealthScripts(driver); // ← Thêm dòng này
```

## 📊 KIỂM TRA IP/PROXY

### Test IP hiện tại:
```
1. Truy cập: https://whoer.net/
2. Kiểm tra:
   - IP Score: > 80% ✅ | < 50% ❌
   - Anonymity: High ✅ | Low ❌
   - DNS: No leaks ✅ | Leaks ❌
   - Time zone: Matches IP location ✅
```

### Test Proxy Quality:
```
1. Truy cập: https://www.deviceinfo.me/
2. Kiểm tra:
   - WebRTC IP: Phải khớp với proxy IP
   - DNS: Phải khớp với proxy location
   - Time zone: Phải khớp với proxy location
   
Nếu có leak → ❌ Google biết IP thật của bạn!
```

## 🔥 TEST THỰC TẾ VỚI GOOGLE

### Test 1: Google Search
```
1. Mở browser automation
2. Truy cập: https://www.google.com/
3. Search: "what is my ip"
4. Kiểm tra:
   - ✅ Không có CAPTCHA → Tốt
   - ⚠️ Có CAPTCHA 1 lần → Tạm ổn
   - 🔴 CAPTCHA liên tục → Bị đánh dấu!
```

### Test 2: Gmail Signup Page
```
1. Mở: https://accounts.google.com/signup
2. Điền tên + họ
3. Click Next
4. Kiểm tra:
   - ✅ Chuyển sang bước chọn Gmail → Tốt
   - ⚠️ Đề xuất phone verify → Bình thường
   - 🔴 BẮT BUỘC phone verify ngay → Bị đánh dấu!
```

### Test 3: Tạo Gmail thật
```
Tạo 1 Gmail thử nghiệm và theo dõi:
- Tạo thành công không verify: ✅ Profile tốt
- Verify 1 lần sau đó OK: ⚠️ Profile tạm ổn  
- Verify nhiều lần: 🔴 Profile bị đánh dấu
- Không tạo được: 🔴 Profile/IP bị blacklist
```

## 📈 CÁCH TÍNH "TRUST SCORE"

```
TRUST SCORE = (Các yếu tố tích cực) - (Các yếu tố tiêu cực)

YẾU TỐ TÍCH CỰC (+):
+20: Profile > 1 tháng tuổi
+15: IP residential (không phải datacenter)
+10: Không có CAPTCHA khi search Google
+10: Stealth scripts hoạt động tốt
+10: Fingerprint nhất quán
+5:  Có cookies/cache từ trước

YẾU TỐ TIÊU CỰC (-):
-30: navigator.webdriver = true
-25: IP bị blacklist (Fraud Score > 75)
-20: CAPTCHA liên tục
-15: Profile mới (< 1 ngày)
-10: Datacenter IP
-10: Verify rate > 70%

ĐÁNH GIÁ:
> 40:  ✅ Rất tốt - Tạo được 10+ Gmail/ngày
20-40: ⚠️ Tạm ổn - Tạo được 3-5 Gmail/ngày
0-20:  🔴 Nguy hiểm - Chỉ nên tạo 1-2 Gmail/ngày
< 0:   ☠️ Bị blacklist - PHẢI THAY ĐỔI TẤT CẢ
```

## 🛠️ CÁCH XỬ LÝ NẾU BỊ ĐÁNH DẤU

### Mức độ 1: Nghi ngờ nhẹ (Trust Score 20-40)
```bash
✅ Giải pháp:
1. Giảm số lượng Gmail/ngày xuống 2-3
2. Tăng spacing lên 60 phút
3. Đổi sang proxy khác vùng
4. Chờ 2-3 ngày để "nguội" đi
```

### Mức độ 2: Bị theo dõi (Trust Score 0-20)
```bash
⚠️ Giải pháp:
1. RESET profile hiện tại:
   AdvancedChromeConfig.ResetStableProfile();

2. ĐỔI IP/PROXY hoàn toàn (khác vùng, khác ISP)

3. Tạo profile MỚI (qa_new_1)

4. Chờ 1 tuần trước khi tạo Gmail
   (Dùng profile mới browse web bình thường để build trust)

5. Test lại với các website ở trên
```

### Mức độ 3: BỊ BLACKLIST (Trust Score < 0)
```bash
🔴 Giải pháp:
1. DỪNG tạo Gmail ngay lập tức!

2. XÓA TẤT CẢ:
   - Xóa profiles: C:\Users\...\RegMail\chrome_profiles\
   - Clear cookies/cache
   - Reset Windows fingerprint (đổi computer name)

3. ĐỔI HOÀN TOÀN:
   - Đổi IP public (reset modem, đổi ISP)
   - Đổi proxy (residential proxy khác vùng)
   - Tạo profiles hoàn toàn mới

4. OPTIONAL: Đổi máy/VM
   - Nếu IP gia đình bị blacklist → Dùng VPS/VPN khác
   - Nếu máy bị blacklist → Dùng máy ảo hoặc máy khác

5. CHỜ 2-4 TUẦN trước khi thử lại

6. Khi thử lại:
   - Chỉ tạo 1 Gmail/ngày
   - Monitor carefully
   - Nếu vẫn verify → Dừng hẳn
```

## 📋 CHECKLIST HÀNG NGÀY

### Trước khi tạo Gmail:
```
□ Test Google search → Không CAPTCHA
□ Test whoer.net → Score > 80%
□ Test bot.sannysoft.com → Tất cả GREEN  
□ Check IP fraud score → < 50
□ Kiểm tra profile age → > 1 ngày
□ Spacing từ lần tạo trước → > 30 phút
```

### Sau khi tạo Gmail:
```
□ Ghi lại verify rate
□ Nếu verify > 50% → Dừng và đổi proxy
□ Nếu không verify → Tiếp tục nhưng spacing 60 phút
□ Backup profile nếu tạo thành công 5+ Gmail
```

## 🎯 MONITORING DASHBOARD (Đề xuất)

Tạo file Excel/Sheet để tracking:

| Ngày | Profile | Proxy IP | Gmail Created | Verify? | CAPTCHA? | Trust Score | Notes |
|------|---------|----------|---------------|---------|----------|-------------|-------|
| 15/10 | qa1 | 1.2.3.4 | 3 | 1/3 | No | 35 | OK ✅ |
| 15/10 | qa2 | 5.6.7.8 | 3 | 2/3 | Yes | 20 | ⚠️ Đổi proxy |
| 16/10 | qa3 | 9.8.7.6 | 2 | 2/2 | Yes x3 | -10 | 🔴 STOP! |

**KHI NÀO CẢNH BÁO?**
- Verify rate tăng đột ngột
- CAPTCHA xuất hiện liên tục
- Trust score giảm < 20
- Cùng 1 profile: verify 3 lần liên tiếp

## 🔬 SCRIPT TỰ ĐỘNG KIỂM TRA

```csharp
// Thêm vào Program.cs
static async Task<int> CalculateTrustScore(IWebDriver driver, string proxyIP)
{
    int score = 0;
    IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
    
    Console.WriteLine("\n🔬 CALCULATING TRUST SCORE...");
    Console.WriteLine("═══════════════════════════════════════════");
    
    // Check 1: navigator.webdriver
    var webdriver = js.ExecuteScript("return navigator.webdriver;");
    if (webdriver == null)
    {
        score += 10;
        Console.WriteLine("✅ navigator.webdriver: undefined (+10)");
    }
    else
    {
        score -= 30;
        Console.WriteLine("❌ navigator.webdriver: true (-30)");
    }
    
    // Check 2: chrome.runtime
    var chromeRuntime = js.ExecuteScript("return window.chrome && window.chrome.runtime;");
    if (chromeRuntime != null)
    {
        score += 10;
        Console.WriteLine("✅ chrome.runtime: present (+10)");
    }
    else
    {
        score -= 10;
        Console.WriteLine("❌ chrome.runtime: missing (-10)");
    }
    
    // Check 3: Profile age (giả sử tracking trong file)
    var profilePath = AdvancedChromeConfig.GetCurrentProfileName();
    // TODO: Check profile creation date
    // If > 30 days: score += 20
    // If > 7 days: score += 10
    // If < 1 day: score -= 15
    
    // Check 4: IP quality (cần call API)
    // TODO: Check IP fraud score
    // If < 50: score += 15
    // If > 75: score -= 25
    
    Console.WriteLine("═══════════════════════════════════════════");
    Console.WriteLine($"📊 TRUST SCORE: {score}");
    
    if (score > 40)
        Console.WriteLine("✅ RẤT TỐT - An toàn tạo nhiều Gmail");
    else if (score > 20)
        Console.WriteLine("⚠️ TẠM ỔN - Tạo 3-5 Gmail/ngày");
    else if (score > 0)
        Console.WriteLine("🔴 NGUY HIỂM - Chỉ tạo 1-2 Gmail/ngày");
    else
        Console.WriteLine("☠️ BỊ BLACKLIST - DỪNG NGAY!");
    
    Console.WriteLine("═══════════════════════════════════════════\n");
    
    return score;
}
```

## 🎓 KẾT LUẬN

**Các công cụ chính để check**:
1. ✅ **bot.sannysoft.com** - Test automation detection
2. ✅ **whoer.net** - Test IP quality
3. ✅ **ipqualityscore.com** - Test IP blacklist
4. ✅ **Google reCAPTCHA demo** - Test Google trust
5. ✅ **Thử tạo 1 Gmail test** - Test thực tế

**Quy tắc vàng**:
- Check TRƯỚC mỗi session tạo Gmail
- Monitor TRONG quá trình (verify rate)
- Analyze SAU (ghi log để tìm pattern)

**Nếu thấy dấu hiệu bị đánh dấu → DỪNG NGAY và đổi profile/proxy!**

---
**Cập nhật**: 2025-10-15  
**Version**: 1.0 - Google Blacklist Detection Guide

