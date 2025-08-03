
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

class Program
{
    // ─────── Configuration ───────
    private const string BotToken = "7475030096:AAF8s7K7At2RFyCiZIEV88iCl4WPQAVd-H4";  // replace with your token
    private static TelegramBotClient botClient;
    private static long? chatId = null;        // populated when user sends /start
    private static HashSet<string> seenDates = new();

    static async Task Main()
    {
        // ─── 1) Start Telegram Bot ───
        botClient = new TelegramBotClient(BotToken);
        using var cts = new CancellationTokenSource();
        var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"🤖 @{me.Username} started. Send /start to register your chat.");

        // ─── 2) Prepare HTTP client ───
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        Console.OutputEncoding = Encoding.UTF8;

        // ─── 3) Build the profile URL ───
        string profileUrl = await BuildProfileUrlAsync(http);

        // ─── 4) One‑off test to verify notifications work ───
        Console.WriteLine("🏷️ Running one‑off test check now…");
        var testNew = await FetchNewDatesOnceAsync(http, profileUrl, seenDates);
        if (testNew.Count > 0)
        {
            Console.WriteLine($"✅ Test found {testNew.Count} new date(s): {string.Join(", ", testNew)}");
            if (chatId.HasValue)
            {
                foreach (var d in testNew)
                    await botClient.SendTextMessageAsync(chatId.Value, $"🔔 [TEST] New date available: {d}");
            }
        }
        else
        {
            Console.WriteLine("ℹ️ Test found no new dates (all already seen).");
        }

        // ─── 5) Start the continuous monitoring loop ───
        await MonitorForNewDatesAsync(http, profileUrl);
    }

    private static async Task<string> BuildProfileUrlAsync(HttpClient http)
    {
        var servicesHtml = await http.GetStringAsync("https://apnt.app/zavgorodnyaya_olga2/services");
        var doc = new HtmlDocument();
        doc.LoadHtml(servicesHtml);

        var script = doc.DocumentNode
            .SelectSingleNode("//script[contains(text(),'ONLINE_CID')]");
        var cidMatch = Regex.Match(script.InnerText, @"cid=[A-Za-z0-9]+");
        string cidScript = cidMatch.Value;

        var serviceNode = doc.DocumentNode
            .SelectNodes("//div[contains(@class,'service')]")
            .First(div =>
                div.SelectSingleNode(".//div[@class='name']")
                   .InnerText.Contains("Мужская стрижка")
            );
        string serviceId = serviceNode.GetAttributeValue("data-id", "");

        string url = $"https://apnt.app/zavgorodnyaya_olga2?services={serviceId}&{cidScript}";
        Console.WriteLine($"🔗 Profile URL: {url}");
        return url;
    }

    /// <summary>
    /// Fetches available dates once, adds any brand‑new ones to seenDates,
    /// and returns the list of newly found dates.
    /// </summary>
    private static async Task<List<string>> FetchNewDatesOnceAsync(
        HttpClient http,
        string profileUrl,
        HashSet<string> seenDates)
    {
        var newlyFound = new List<string>();

        var html = await http.GetStringAsync(profileUrl);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var days = doc.DocumentNode.SelectNodes(
            "//td[contains(@class,'day-en') and contains(@class,'day-active') and @data-date]"
        );

        if (days != null)
        {
            foreach (var td in days)
            {
                var date = td.GetAttributeValue("data-date", "");
                if (seenDates.Add(date))
                    newlyFound.Add(date);
            }
        }

        return newlyFound;
    }

    private static async Task MonitorForNewDatesAsync(HttpClient http, string profileUrl)
    {
        while (true)
        {
            Console.WriteLine($"\n🔄 Checking at {DateTime.Now:HH:mm:ss}");
            try
            {
                var html = await http.GetStringAsync(profileUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var calendars = doc.DocumentNode
                    .SelectNodes("//div[contains(@class,'online-appointment-calendars')]");
                if (calendars == null || calendars.Count == 0)
                {
                    Console.WriteLine("❌ No calendars found; saving debug HTML");
                   
                }
                else
                {
                    int runningTotal = 0;
                    foreach (var cal in calendars)
                    {
                        var days = cal.SelectNodes(
                            ".//td[contains(@class,'day-en') and contains(@class,'day-active') and @data-date]"
                        );
                        int count = days?.Count ?? 0;
                        Console.WriteLine($"📆 Calendar: {count} active days");
                        runningTotal += count;

                        if (days != null)
                        {
                            foreach (var td in days)
                            {
                                var date = td.GetAttributeValue("data-date", "");
                                if (seenDates.Add(date))
                                {
                                    Console.WriteLine($"📢 New date: {date}");
                                    if (chatId.HasValue)
                                    {
                                        await botClient.SendTextMessageAsync(
                                            chatId.Value,
                                            $"📅 New date available: {date}"
                                        );
                                    }
                                }
                            }
                        }
                    }

                    Console.WriteLine($"📊 Total available days now: {runningTotal}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error during check: {ex.Message}");
            }

            // For quick demo, you can shorten this to TimeSpan.FromSeconds(5)
            await Task.Delay(TimeSpan.FromMinutes(15));
        }
    }

    // ─── Telegram Handlers ───
    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Type != UpdateType.Message || update.Message!.Text == null) return;

        var msg = update.Message;
        Console.WriteLine($"📩 Message from {msg.Chat.Id}: {msg.Text}");

        if (msg.Text.Trim().Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            chatId = msg.Chat.Id;
            await bot.SendTextMessageAsync(
                chatId.Value,
                "✅ You’re registered! I’ll notify you about new dates."
            );
        }
        else
        {
            await bot.SendTextMessageAsync(
                msg.Chat.Id,
                "🤖 Please send /start to receive appointment updates."
            );
        }
    }

    static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        Console.WriteLine($"❌ Bot error: {ex.Message}");
        return Task.CompletedTask;
    }
}

