using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using HtmlAgilityPack;

class Program
{
    static async Task Main()
    {
        // Use the updated URL with current cid
        var url = "https://apnt.app/zavgorodnyaya_olga2/times?cid=caaptlaf1cb05i0kbkk8dk37gh";

        using var client = new HttpClient();
        // Mimic browser headers
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");

        // Fetch HTML with error handling
        HttpResponseMessage response;
        string html;
        try
        {
            response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            html = await response.Content.ReadAsStringAsync();

            // Save HTML for inspection
            File.WriteAllText("debug.html", html);
            Console.WriteLine("⚠️ HTML saved to debug.html for inspection");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🚫 Request failed: {ex.Message}");
            return;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Verify HTML structure
        Console.WriteLine("🔍 Verifying HTML structure...");
        Console.WriteLine($"Document title: {doc.DocumentNode.SelectSingleNode("//title")?.InnerText.Trim() ?? "N/A"}");
        Console.WriteLine($"Found time-slots container: {doc.DocumentNode.SelectSingleNode("//div[contains(@class,'times-container')]") != null}");

        // Target ONLY available slots - updated selector
        var nodes = doc.DocumentNode.SelectNodes(
            "//button[contains(concat(' ', @class, ' '), ' time-slot ') and contains(concat(' ', @class, ' '), ' available ')]");

        if (nodes == null || nodes.Count == 0)
        {
            Console.WriteLine("❌ No available slots found. Please check:");
            Console.WriteLine($"1. URL validity: {url}");
            Console.WriteLine($"2. HTML structure in debug.html");
            Console.WriteLine($"3. Current availability on the live site");

            // Check for common error patterns
            var errorNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'error')]");
            if (errorNode != null)
            {
                Console.WriteLine($"⚠️ Error message found: {errorNode.InnerText.Trim()}");
            }
            return;
        }

        Console.WriteLine("✅ Available slots:");
        foreach (var node in nodes)
        {
            Console.WriteLine($" - {node.InnerText.Trim()}");
        }
    }
}