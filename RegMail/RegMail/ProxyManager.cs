using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RegMail
{
    public class ProxyManager
    {
        // ======== Data model ========
        public sealed class ProxySpec
        {
            public string Scheme;   // "http" | "socks5" | "socks4"
            public string Host;
            public int Port;
            public string Username;
            public string Password;

            public bool HasAuth { get { return !string.IsNullOrEmpty(Username); } }
            public bool IsHttp { get { return string.Equals(Scheme, "http", StringComparison.OrdinalIgnoreCase); } }
            public bool IsSocks { get { return Scheme != null && Scheme.StartsWith("socks", StringComparison.OrdinalIgnoreCase); } }

            public override string ToString()
            {
                return Scheme + "://" + Host + ":" + Port + (HasAuth ? (" (auth:" + Username + ")") : "");
            }
        }

        public static List<ProxySpec> LoadProxiesFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Đường dẫn file rỗng.");
            if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy file proxy.", path);

            var list = new List<ProxySpec>();
            foreach (var raw in File.ReadLines(path))
            {
                var line = (raw ?? "").Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#") || line.StartsWith("//")) continue;

                try
                {
                    var spec = ParseLine(line);
                    if (spec != null) list.Add(spec);
                }
                catch
                {
                    // Có thể log warning ở đây nếu cần
                }
            }
            return list;
        }
        public static ProxySpec PickRandom(IReadOnlyList<ProxySpec> all, Random rng = null)
        {
            if (all == null || all.Count == 0) throw new InvalidOperationException("Danh sách proxy trống.");
            if (rng == null) rng = new Random();
            int idx = rng.Next(all.Count);
            return all[idx];
        }
        public static async Task<ProxySpec> PickRandomWorkingHttpFromFileAsync(string path, int timeoutSeconds = 8, int maxToTry = 20)
        {
            var all = LoadProxiesFromFile(path).Where(p => p.IsHttp).ToList();
            if (all.Count == 0) throw new InvalidOperationException("File không có HTTP proxy nào.");

            Shuffle(all);
            if (maxToTry > 0) all = all.Take(maxToTry).ToList();

            foreach (var p in all)
            {
                bool ok = await TestHttpProxyAsync(p, timeoutSeconds);
                if (ok) return p;
            }
            throw new InvalidOperationException("Không tìm thấy HTTP proxy còn hoạt động.");
        }
        public static void ApplyToChrome(ChromeOptions options, ProxySpec p, bool headless = false)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (p == null) throw new ArgumentNullException(nameof(p));

            if (p.IsSocks && p.HasAuth)
                throw new NotSupportedException("SOCKS proxy có username/password không được Chrome hỗ trợ trực tiếp. Hãy dùng HTTP proxy hoặc forwarder HTTP→SOCKS5.");

            options.AddArgument("--proxy-bypass-list=localhost,127.0.0.1");
            options.AddArgument($"--proxy-server={p.Scheme}://{p.Host}:{p.Port}");

            if (p.IsHttp && p.HasAuth)
            {
                if (headless)
                {
                    Console.WriteLine("⚠️ Headless + HTTP proxy có auth: extension không chạy → khả năng bị 407. Cân nhắc chạy non-headless hoặc dùng forwarder.");
                }
                else
                {
                    string extZip = BuildAutoAuthExtensionZip(p.Username, p.Password);
                    options.AddExtension(extZip);
                }
            }
            Console.WriteLine($"✅ Applied proxy: {p}");
        }
        private static ProxySpec ParseLine(string line)
        {
            if (line.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                string s = NormalizeSchemes(line);

                Uri uri;
                if (!Uri.TryCreate(s, UriKind.Absolute, out uri))
                    throw new ArgumentException("URL proxy không hợp lệ: " + line);

                if (uri.Port <= 0 || uri.Port > 65535)
                    throw new ArgumentException("Port không hợp lệ: " + line);

                string scheme = uri.Scheme.ToLowerInvariant();
                EnsureChromeSupportedScheme(scheme);

                var spec = new ProxySpec();
                spec.Scheme = scheme;
                spec.Host = uri.Host;
                spec.Port = uri.Port;

                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var parts = uri.UserInfo.Split(new[] { ':' }, 2);
                    spec.Username = Uri.UnescapeDataString(parts[0]);
                    spec.Password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
                }
                return spec;
            }
            else
            {
                // "ip:port" hoặc "ip:port:user:pass"
                var parts = line.Split(':');
                if (parts.Length == 2)
                {
                    int port;
                    if (!int.TryParse(parts[1], out port) || port <= 0 || port > 65535)
                        throw new ArgumentException("Port không hợp lệ: " + line);

                    var spec = new ProxySpec();
                    spec.Scheme = "http";
                    spec.Host = parts[0];
                    spec.Port = port;
                    return spec;
                }
                else if (parts.Length == 4)
                {
                    int port;
                    if (!int.TryParse(parts[1], out port) || port <= 0 || port > 65535)
                        throw new ArgumentException("Port không hợp lệ: " + line);

                    var spec = new ProxySpec();
                    spec.Scheme = "http"; // mặc định http
                    spec.Host = parts[0];
                    spec.Port = port;
                    spec.Username = parts[2];
                    spec.Password = parts[3];
                    return spec;
                }
            }
            throw new ArgumentException("Dòng proxy không đúng định dạng: " + line);
        }
        private static string NormalizeSchemes(string s)
        {
            // C# 7.3 không có Replace(String, String, StringComparison). Làm thủ công:
            s = ReplaceInsensitive(s, "https://", "http://");
            s = ReplaceInsensitive(s, "socks5h://", "socks5://");
            s = ReplaceInsensitive(s, "socks4a://", "socks4://");
            return s;
        }

        private static void EnsureChromeSupportedScheme(string scheme)
        {
            if (scheme == "https") throw new NotSupportedException("Chrome không nhận https:// cho --proxy-server; dùng http://host:port.");
            if (scheme != "http" && scheme != "socks5" && scheme != "socks4")
                throw new NotSupportedException($"Chrome không hỗ trợ scheme '{scheme}'. Chỉ hỗ trợ http, socks4, socks5.");
        }
        private static string ReplaceInsensitive(string input, string search, string replacement)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(search)) return input;
            var sb = new StringBuilder();
            int pos = 0;
            int idx;
            while ((idx = input.IndexOf(search, pos, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                sb.Append(input, pos, idx - pos);
                sb.Append(replacement);
                pos = idx + search.Length;
            }
            sb.Append(input, pos, input.Length - pos);
            return sb.ToString();
        }
        private static void Shuffle<T>(IList<T> list)
        {
            var rng = new Random();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
        public static async Task<bool> TestHttpProxyAsync(ProxySpec p, int timeoutSeconds)
        {
            if (p == null || !p.IsHttp) return false;

            // Cho dev/test: bỏ kiểm tra SSL (tùy bạn có thể bỏ dòng này)
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            var handler = new HttpClientHandler();
            handler.Proxy = new WebProxy(p.Host, p.Port);
            if (p.HasAuth) handler.Proxy.Credentials = new NetworkCredential(p.Username, p.Password);
            handler.UseProxy = true;

            var http = new HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            try
            {
                var resp = await http.GetAsync("https://httpbin.org/ip").ConfigureAwait(false);
                return ((int)resp.StatusCode >= 200) && ((int)resp.StatusCode < 300);
            }
            catch
            {
                return false;
            }
            finally
            {
                http.Dispose();
            }
        }
        public static string BuildAutoAuthExtensionZip(string username, string password)
        {
            string dir = Path.Combine(Path.GetTempPath(), "AutoAuth_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            File.WriteAllText(Path.Combine(dir, "manifest.json"), @"{
  ""version"": ""1.0.0"",
  ""manifest_version"": 2,
  ""name"": ""AutoAuth Proxy"",
  ""permissions"": [""webRequest"", ""webRequestBlocking"", ""<all_urls>""],
  ""background"": { ""scripts"": [""background.js""] }
}");
            string bg = $@"chrome.webRequest.onAuthRequired.addListener(
  function (details) {{
    return {{ authCredentials: {{ username: ""{EscapeJs(username)}"", password: ""{EscapeJs(password)}"" }} }};
  }},
  {{ urls: [""<all_urls>""] }},
  ['blocking']
);";
            File.WriteAllText(Path.Combine(dir, "background.js"), bg);

            string zip = Path.Combine(Path.GetTempPath(), "AutoAuth_" + Guid.NewGuid().ToString("N") + ".zip");
            ZipFile.CreateFromDirectory(dir, zip);
            return zip;
        }

        private static string EscapeJs(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}
