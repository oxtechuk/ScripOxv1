using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ScripOx.Desktop.Models;
using Serilog;

namespace ScripOx.Desktop.Services;

public class CrawlerStats
{
    public int TotalTargets   { get; set; }
    public int ProcessedCount { get; set; }
    public int EmailsFound    { get; set; }
    public int PhonesFound    { get; set; }
    public int SuccessCount   { get; set; }
    public int ErrorCount     { get; set; }
    public int BlockedCount   { get; set; }
    public int ProgressPct    => TotalTargets > 0 ? (int)Math.Min(100, Math.Round((double)ProcessedCount / TotalTargets * 100)) : 0;
}

public class CrawlerLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string   Level     { get; set; } = "INFO";
    public string   Message   { get; set; } = string.Empty;
    public string   TimeText  => Timestamp.ToString("HH:mm:ss");

    public override string ToString() => $"[{TimeText}] {Message}";
}

public class CrawlResult
{
    public long          CompanyId { get; set; }
    public string        Name      { get; set; } = string.Empty;
    public string        Url       { get; set; } = string.Empty;
    public List<string>  Emails    { get; set; } = [];
    public List<string>  Phones    { get; set; } = [];
    public bool          IsSuccess => Emails.Count > 0 || Phones.Count > 0;
}

/// <summary>
/// High-performance, anti-bot resilient asynchronous crawler for extracting emails and phone numbers.
/// Includes rotating User-Agents, realistic browser headers, stealth delay, and per-request timeouts.
/// </summary>
public class ContactCrawlerService
{
    private readonly CompanyService _companyService;
    private readonly ExtractionJobService _jobService;
    private readonly HttpClient _httpClient;

    // Realistic desktop User-Agents to prevent bot fingerprinting
    private static readonly string[] DesktopUserAgents =
    [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36 Edg/125.0.0.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:125.0) Gecko/20100101 Firefox/125.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_4_1) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4.1 Safari/605.1.15"
    ];

    // Regex for Email detection
    private static readonly Regex EmailRegex = new(
        @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for Mailto links
    private static readonly Regex MailtoRegex = new(
        @"href\s*=\s*[""']mailto:([^""' ?#]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for Tel links
    private static readonly Regex TelRegex = new(
        @"href\s*=\s*[""']tel:([^""' ?#]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for Phone numbers
    private static readonly Regex PhoneRegex = new(
        @"(?:\+|00)?(?:[0-9]{1,3})?[\s\.\-]?\(?[0-9]{2,4}\)?[\s\.\-]?[0-9]{3,4}[\s\.\-]?[0-9]{3,5}",
        RegexOptions.Compiled);

    // Ignored extensions and dummy domains
    private static readonly HashSet<string> IgnoredEmailExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".css", ".js", ".woff", ".woff2", ".ttf"
    };

    private static readonly string[] IgnoredEmailDomains =
    [
        "example.com", "domain.com", "yoursite.com", "email.com",
        "sentry.io", "wixpress.com", "cloudflare.com", "bootstrap.com",
        "google.com", "googleapis.com", "w3.org", "schema.org", "gravatar.com"
    ];

    public event Action<CrawlerStats>? OnProgress;
    public event Action<CrawlerLogEntry>? OnLog;
    public event Action<CrawlResult>? OnItemExtracted;

    public bool IsRunning { get; private set; }

    public ContactCrawlerService(CompanyService companyService, ExtractionJobService jobService)
    {
        _companyService = companyService;
        _jobService     = jobService;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (req, cert, chain, errors) => true,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            UseCookies = true
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// Executes contact crawl for a list of companies using an anti-bot worker queue.
    /// </summary>
    public async Task<CrawlerStats> CrawlCompaniesAsync(
        List<Company> companies,
        int jobId,
        bool probeContactSubpath = true,
        bool stealthMode = true,
        int maxConcurrency = 2,
        CancellationToken cancellationToken = default)
    {
        var stats = new CrawlerStats
        {
            TotalTargets = companies.Count
        };

        IsRunning = true;
        var modeDesc = stealthMode ? "وضع التخفي الذكي (حماية مضادة للحظر)" : "الوضع السريع";
        LogMsg("🚀", $"بدء فحص {companies.Count} موقع بتشغيل: [{modeDesc}]...");

        var queue = new ConcurrentQueue<Company>(companies);
        int workersCount = Math.Clamp(maxConcurrency, 1, 4);
        var workerTasks = new List<Task>();

        for (int i = 0; i < workersCount; i++)
        {
            int workerId = i + 1;
            workerTasks.Add(Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested && queue.TryDequeue(out var company))
                {
                    try
                    {
                        // 1. Human jitter delay in stealth mode to avoid bot bans
                        if (stealthMode)
                        {
                            var delayMs = Random.Shared.Next(650, 1400);
                            await Task.Delay(delayMs, cancellationToken);
                        }

                        if (cancellationToken.IsCancellationRequested) break;

                        // 2. Perform crawl on this company with isolated error handling
                        var result = await CrawlSingleTargetAsync(
                            company.Id,
                            company.Name,
                            company.Website!,
                            probeContactSubpath,
                            stealthMode,
                            cancellationToken);

                        lock (stats)
                        {
                            stats.ProcessedCount++;
                            if (result.IsSuccess)
                            {
                                stats.SuccessCount++;
                                stats.EmailsFound += result.Emails.Count;
                                stats.PhonesFound += result.Phones.Count;
                            }
                            else
                            {
                                stats.ErrorCount++;
                            }
                        }

                        // 3. Save directly to database if contacts discovered
                        if (result.IsSuccess)
                        {
                            var firstEmail  = result.Emails.FirstOrDefault();
                            var firstPhone  = result.Phones.FirstOrDefault();
                            var secondPhone = result.Phones.Skip(1).FirstOrDefault();

                            if (company.Id > 0)
                            {
                                await _companyService.UpdateContactInfoAsync(company.Id, firstEmail, firstPhone, secondPhone);
                                LogMsg("💾", $"تم حفظ وتحديث [{company.Name}] في قاعدة البيانات (✉️: {firstEmail ?? "-"} | 📞: {firstPhone ?? "-"})");
                            }
                            else
                            {
                                string hostName = company.Name;
                                if (string.IsNullOrWhiteSpace(hostName) && Uri.TryCreate(result.Url, UriKind.Absolute, out var parsedUri))
                                {
                                    hostName = parsedUri.Host;
                                }
                                var newCompany = new Company
                                {
                                    Name = !string.IsNullOrWhiteSpace(hostName) ? hostName : "Web Lead",
                                    Website = result.Url,
                                    Email = firstEmail,
                                    Phone = firstPhone,
                                    Phone2 = secondPhone,
                                    Category = "Web Crawl",
                                    Priority = "medium"
                                };
                                long newId = await _companyService.CreateAsync(newCompany, 1);
                                result.CompanyId = newId;
                                LogMsg("💾", $"تم إدراج شركة جديدة برقم #{newId} وحفظ جهات الاتصال (✉️: {firstEmail ?? "-"} | 📞: {firstPhone ?? "-"})");
                            }
                        }

                        OnItemExtracted?.Invoke(result);
                        OnProgress?.Invoke(stats);

                        // Periodically sync job progress into SQLite
                        if (stats.ProcessedCount % 3 == 0 || stats.ProcessedCount == stats.TotalTargets)
                        {
                            _ = _jobService.UpdateJobProgressAsync(
                                jobId, stats.ProgressPct, stats.EmailsFound + stats.PhonesFound, stats.SuccessCount, stats.ErrorCount);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        lock (stats) { stats.ProcessedCount++; stats.ErrorCount++; }
                        LogMsg("⚠️", $"تخطي الشركة [{company.Name}]: {ex.Message}");
                    }
                }
            }, cancellationToken));
        }

        try
        {
            await Task.WhenAll(workerTasks);
        }
        catch (OperationCanceledException)
        {
            LogMsg("⏹", "تم إيقاف عملية الزحف بواسطة المستخدم.");
            await _jobService.CancelJobAsync(jobId);
        }
        finally
        {
            IsRunning = false;
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            LogMsg("✅", $"اكتمل الفحص بنجاح! تم استخراج {stats.EmailsFound} إيميل و {stats.PhonesFound} رقم هاتف من إجمالي {stats.TotalTargets} موقع.");
            await _jobService.CompleteJobAsync(jobId, stats.EmailsFound + stats.PhonesFound, stats.SuccessCount, stats.ErrorCount);
        }

        return stats;
    }

    /// <summary>
    /// Crawls a single company target (homepage + /contact) with anti-bot headers and timeouts.
    /// </summary>
    public async Task<CrawlResult> CrawlSingleTargetAsync(
        long companyId,
        string name,
        string rawUrl,
        bool probeContactSubpath,
        bool stealthMode,
        CancellationToken ct)
    {
        var result = new CrawlResult
        {
            CompanyId = companyId,
            Name      = name,
            Url       = rawUrl
        };

        var normalizedUrl = NormalizeUrl(rawUrl);
        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            LogMsg("⚠️", $"رابط غير صالح لـ [{name}]: {rawUrl}");
            return result;
        }

        var foundEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var foundPhones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        LogMsg("🌐", $"فحص الموقع: {normalizedUrl} ({name})");

        // 1. Fetch homepage with anti-bot headers
        var homeHtml = await FetchHtmlWithStealthAsync(normalizedUrl, ct);
        if (!string.IsNullOrWhiteSpace(homeHtml))
        {
            ExtractContactsFromHtml(homeHtml, foundEmails, foundPhones);
        }

        // 2. Probe /contact subpath if requested
        if (probeContactSubpath && !ct.IsCancellationRequested)
        {
            if (stealthMode)
            {
                await Task.Delay(Random.Shared.Next(350, 750), ct);
            }

            try
            {
                var baseUri = new Uri(normalizedUrl);
                var contactUri = new Uri(baseUri, "/contact").ToString();
                LogMsg("🔍", $"فحص مسار التواصل: {contactUri}");
                var contactHtml = await FetchHtmlWithStealthAsync(contactUri, ct);
                if (!string.IsNullOrWhiteSpace(contactHtml))
                {
                    ExtractContactsFromHtml(contactHtml, foundEmails, foundPhones);
                }
                else if (foundEmails.Count == 0 && foundPhones.Count == 0)
                {
                    // Fallback to /contact-us
                    var altUri = new Uri(baseUri, "/contact-us").ToString();
                    var altHtml = await FetchHtmlWithStealthAsync(altUri, ct);
                    if (!string.IsNullOrWhiteSpace(altHtml))
                    {
                        ExtractContactsFromHtml(altHtml, foundEmails, foundPhones);
                    }
                }
            }
            catch { }
        }

        result.Emails = foundEmails.ToList();
        result.Phones = foundPhones.ToList();

        if (result.Emails.Count > 0)
        {
            LogMsg("✉️", $"تم استخراج بريد ({result.Emails.Count}) لـ [{name}]: {string.Join(", ", result.Emails)}");
        }
        if (result.Phones.Count > 0)
        {
            LogMsg("📞", $"تم استخراج هاتف ({result.Phones.Count}) لـ [{name}]: {string.Join(", ", result.Phones)}");
        }

        return result;
    }

    /// <summary>
    /// Fetches HTML using rotating desktop headers, isolated per-request timeout, and bot-ban handling.
    /// </summary>
    private async Task<string?> FetchHtmlWithStealthAsync(string url, CancellationToken ct)
    {
        // 5-second strict individual request timeout
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(5.5));

        try
        {
            var uAgent = DesktopUserAgents[Random.Shared.Next(DesktopUserAgents.Length)];
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", uAgent);
            req.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            req.Headers.TryAddWithoutValidation("Accept-Language", "ar,en-US;q=0.9,en;q=0.8");
            req.Headers.TryAddWithoutValidation("Sec-Ch-Ua", "\"Chromium\";v=\"124\", \"Google Chrome\";v=\"124\", \"Not-A.Brand\";v=\"99\"");
            req.Headers.TryAddWithoutValidation("Sec-Ch-Ua-Mobile", "?0");
            req.Headers.TryAddWithoutValidation("Sec-Ch-Ua-Platform", "\"Windows\"");
            req.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            req.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            req.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "none");
            req.Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");
            req.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");

            using var response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);

            if ((int)response.StatusCode == 403 || (int)response.StatusCode == 429)
            {
                LogMsg("🛡️", $"حظر مؤقت من الموقع (كود {(int)response.StatusCode}) - تم التخطي للمتابعة");
                return null;
            }

            if (!response.IsSuccessStatusCode) return null;

            using var stream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
            using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);
            var buffer = new char[512 * 1024]; // 512 KB
            int read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
            return new string(buffer, 0, read);
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested) throw;
            // Single request timeout - ignore and proceed
            return null;
        }
        catch (HttpRequestException)
        {
            // If HTTPS failed due to SSL handshake, try HTTP once
            if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var httpFallback = "http://" + url[8..];
                    using var reqHttp = new HttpRequestMessage(HttpMethod.Get, httpFallback);
                    reqHttp.Headers.TryAddWithoutValidation("User-Agent", DesktopUserAgents[0]);
                    using var respHttp = await _httpClient.SendAsync(reqHttp, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
                    if (respHttp.IsSuccessStatusCode)
                    {
                        return await respHttp.Content.ReadAsStringAsync(linkedCts.Token);
                    }
                }
                catch { }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private void ExtractContactsFromHtml(string html, HashSet<string> emails, HashSet<string> phones)
    {
        if (string.IsNullOrWhiteSpace(html)) return;

        // 1. Mailto links
        foreach (Match m in MailtoRegex.Matches(html))
        {
            var email = CleanEmail(m.Groups[1].Value);
            if (IsValidEmail(email)) emails.Add(email);
        }

        // 2. Standard @ regex
        foreach (Match m in EmailRegex.Matches(html))
        {
            var email = CleanEmail(m.Value);
            if (IsValidEmail(email)) emails.Add(email);
        }

        // 3. Tel links
        foreach (Match m in TelRegex.Matches(html))
        {
            var phone = CleanPhone(m.Groups[1].Value);
            if (IsValidPhone(phone)) phones.Add(phone);
        }

        // 4. Pattern phones
        foreach (Match m in PhoneRegex.Matches(html))
        {
            var raw = m.Value.Trim();
            var cleaned = CleanPhone(raw);
            if (IsValidPhone(cleaned)) phones.Add(cleaned);
        }
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        var trimmed = url.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "https://" + trimmed;
        }
        return Uri.TryCreate(trimmed, UriKind.Absolute, out _) ? trimmed : string.Empty;
    }

    private static string CleanEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        var cleaned = email.Trim().TrimEnd('.', ',', ';', ':', ')', ']', '}');
        return cleaned;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length < 6 || email.Length > 70) return false;
        if (!email.Contains('@')) return false;

        foreach (var ext in IgnoredEmailExtensions)
        {
            if (email.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return false;
        }

        foreach (var dom in IgnoredEmailDomains)
        {
            if (email.Contains(dom, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    private static string CleanPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var trimmed = phone.Trim();
        var hasPlus = trimmed.StartsWith("+") || trimmed.StartsWith("00");
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());

        if (digits.Length < 7 || digits.Length > 16) return string.Empty;

        if (hasPlus && !digits.StartsWith("00"))
        {
            return "+" + digits;
        }
        if (digits.StartsWith("00"))
        {
            return "+" + digits[2..];
        }

        return digits;
    }

    private static bool IsValidPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        var digitsOnly = phone.Replace("+", "");
        if (digitsOnly.Distinct().Count() <= 2) return false;
        return digitsOnly.Length >= 8 && digitsOnly.Length <= 15;
    }

    private void LogMsg(string icon, string msg)
    {
        var entry = new CrawlerLogEntry
        {
            Message = $"{icon} {msg}"
        };
        OnLog?.Invoke(entry);
        Log.Information("{CrawlerLog}", entry.ToString());
    }
}
