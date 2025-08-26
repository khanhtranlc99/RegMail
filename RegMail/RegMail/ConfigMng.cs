using System.Configuration;

namespace RegMail
{
    public static class ConfigManager
    {
        // API Configuration
        public static string DailyOTP_API_Key => ConfigurationManager.AppSettings["DailyOTP_API_Key"];
        public static string DailyOTP_RentNumber_URL => ConfigurationManager.AppSettings["DailyOTP_RentNumber_URL"];
        public static string DailyOTP_GetMessages_URL => ConfigurationManager.AppSettings["DailyOTP_GetMessages_URL"];
        public static string IP_Check_URL => ConfigurationManager.AppSettings["IP_Check_URL"];

        // Google URLs
        public static string Google_Signup_URL => ConfigurationManager.AppSettings["Google_Signup_URL"];
        public static string Google_Mail_URL => ConfigurationManager.AppSettings["Google_Mail_URL"];
        public static string Google_Drive_URL => ConfigurationManager.AppSettings["Google_Drive_URL"];
        public static string Google_Photos_URL => ConfigurationManager.AppSettings["Google_Photos_URL"];
        public static string Google_YouTube_URL => ConfigurationManager.AppSettings["Google_YouTube_URL"];
        public static string Google_2FA_URL => ConfigurationManager.AppSettings["Google_2FA_URL"];
        public static string Google_Authenticator_URL => ConfigurationManager.AppSettings["Google_Authenticator_URL"];
        public static string Google_PhoneNumbers_URL => ConfigurationManager.AppSettings["Google_PhoneNumbers_URL"];

        // Chrome Configuration
        public static bool Chrome_Enable_Sync => bool.Parse(ConfigurationManager.AppSettings["Chrome_Enable_Sync"]);
        public static bool Chrome_Use_Minimal_Flags => bool.Parse(ConfigurationManager.AppSettings["Chrome_Use_Minimal_Flags"]);
        public static bool Chrome_Headless_Mode => bool.Parse(ConfigurationManager.AppSettings["Chrome_Headless_Mode"]);
        
        // Auto Login Configuration
        public static bool AutoLogin_Enabled => bool.Parse(ConfigurationManager.AppSettings["AutoLogin_Enabled"]);
        public static string AutoLogin_Default_Email => ConfigurationManager.AppSettings["AutoLogin_Default_Email"];
        public static string AutoLogin_Default_Password => ConfigurationManager.AppSettings["AutoLogin_Default_Password"];
        public static bool AutoLogin_Use_Config_If_Empty => bool.Parse(ConfigurationManager.AppSettings["AutoLogin_Use_Config_If_Empty"]);
    }
}
