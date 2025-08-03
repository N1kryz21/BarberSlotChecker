using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

class Program
{
    static void Main(string[] args)
    {
        // 1) Set up headless Chrome
        var options = new ChromeOptions();
        options.AddArgument("--headless");  // Run Chrome without GUI
        options.AddArgument("--disable-gpu");
        using var driver = new ChromeDriver(options);

        // 2) Go to the page
        string url = "https://apnt.app/zavgorodnyaya_olga2/times?cid=7p7id3jc9ikqvbndupekbmlibi";
        driver.Navigate().GoToUrl(url);

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        wait.Until(d => d.FindElement(By.TagName("body")));

        // 3) Give time for JavaScript to render the calendar
        System.Threading.Thread.Sleep(3000); // or wait for some element

        // 4) Grab all visible text
        string allText = driver.FindElement(By.TagName("body")).Text;

        // 5) Extract all date-time patterns from text
        var regex = new Regex(@"\b\d{2}\.\d{2}\.\d{4}\s+\d{2}:\d{2}\b");
        var matches = regex.Matches(allText);

        var dates = new HashSet<string>();
        foreach (Match m in matches)
            dates.Add(m.Value);

        // 6) Display results
        Console.WriteLine("📅 Available visible dates:");
        foreach (var date in dates)
            Console.WriteLine(" - " + date);

        driver.Quit();
    }
}
