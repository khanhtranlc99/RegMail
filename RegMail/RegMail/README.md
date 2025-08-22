# RegMail - Gmail Account Creator

Ứng dụng tự động tạo tài khoản Gmail với các tính năng bảo mật và chống phát hiện.

## 🔐 Bảo Mật

### Cấu hình API Keys

**QUAN TRỌNG**: Không bao giờ commit API keys trực tiếp vào repository!

1. **Tạo file cấu hình local**:
   ```bash
   # Tạo file App.config.local (sẽ được .gitignore)
   cp App.config App.config.local
   ```

2. **Cập nhật API keys trong App.config.local**:
   ```xml
   <appSettings>
     <add key="DailyOTP_API_Key" value="YOUR_ACTUAL_API_KEY_HERE" />
     <!-- Các cấu hình khác... -->
   </appSettings>
   ```

3. **Sử dụng biến môi trường** (khuyến nghị):
   ```bash
   # Windows
   set DailyOTP_API_Key=your_api_key_here
   
   # Linux/Mac
   export DailyOTP_API_Key=your_api_key_here
   ```

### Cấu hình Chrome

Ứng dụng hỗ trợ 2 chế độ cấu hình Chrome:

1. **Chế độ tối thiểu** (mặc định):
   - Ít flags, ổn định hơn
   - Giảm khả năng bị phát hiện
   - Phù hợp cho testing

2. **Chế độ nâng cao**:
   - Nhiều flags bảo mật
   - Hiệu suất cao hơn
   - Có thể gây lỗi UI

Cấu hình trong `App.config`:
```xml
<add key="Chrome_Use_Minimal_Flags" value="true" />
<add key="Chrome_Enable_Sync" value="false" />
<add key="Chrome_Headless_Mode" value="false" />
```

## 🚀 Sử Dụng

1. **Cài đặt dependencies**:
   ```bash
   dotnet restore
   ```

2. **Cấu hình API keys** (xem phần Bảo Mật ở trên)

3. **Chạy ứng dụng**:
   ```bash
   dotnet run
   ```

## 📁 Cấu Trúc Dự Án

```
RegMail/
├── Program.cs              # Main logic
├── ConfigManager.cs        # Quản lý cấu hình
├── ChromeOptionsManager.cs # Quản lý Chrome options
├── FingerManager.cs        # Quản lý fingerprint
├── ProxyManager.cs         # Quản lý proxy
├── App.config             # Cấu hình chính
├── App.config.local       # Cấu hình local (không commit)
└── .gitignore            # Bảo vệ file nhạy cảm
```

## ⚙️ Cấu Hình

### API Configuration
- `DailyOTP_API_Key`: API key cho DailyOTP
- `DailyOTP_RentNumber_URL`: URL thuê số điện thoại
- `DailyOTP_GetMessages_URL`: URL lấy tin nhắn OTP

### Google URLs
- `Google_Signup_URL`: URL đăng ký Gmail
- `Google_Mail_URL`: URL Gmail
- `Google_Drive_URL`: URL Google Drive
- `Google_Photos_URL`: URL Google Photos
- `Google_YouTube_URL`: URL YouTube

### Chrome Configuration
- `Chrome_Enable_Sync`: Bật/tắt Chrome Sync
- `Chrome_Use_Minimal_Flags`: Sử dụng flags tối thiểu
- `Chrome_Headless_Mode`: Chạy ẩn

## 🔧 Troubleshooting

### Lỗi Chrome Session
- Thử chuyển sang chế độ flags tối thiểu
- Kiểm tra ChromeDriver version
- Xóa thư mục user data cũ

### Lỗi API
- Kiểm tra API key trong cấu hình
- Kiểm tra kết nối internet
- Thử lại sau vài phút

## 📝 Changelog

### v2.0.0
- ✅ Tách cấu hình ra file riêng
- ✅ Sử dụng biến môi trường cho API keys
- ✅ Giảm Chrome flags cực đoan
- ✅ Tắt Chrome Sync tự động
- ✅ Cải thiện bảo mật

### v1.0.0
- ✅ Tạo tài khoản Gmail tự động
- ✅ Hỗ trợ 2FA
- ✅ Fingerprint management
- ✅ Proxy rotation

## ⚠️ Lưu Ý

1. **Bảo mật**: Luôn sử dụng file cấu hình local cho API keys
2. **Rate limiting**: Không chạy quá nhiều tab cùng lúc
3. **Proxy**: Sử dụng proxy chất lượng để tránh bị block
4. **Testing**: Test kỹ trước khi sử dụng production

## 📄 License

Dự án này chỉ dành cho mục đích học tập và nghiên cứu.
