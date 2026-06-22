using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;
using Microsoft.Extensions.Logging;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Interfaces;

namespace SmartHealthMonitoring.Services
{
    public class NewsScraperService : INewsScraperService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NewsScraperService> _logger;
        private static readonly Regex ImageRegex = new Regex(@"<img[^>]+src=""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HtmlTagRegex = new Regex(@"<[^>]*>", RegexOptions.Compiled);

        public NewsScraperService(IHttpClientFactory httpClientFactory, ILogger<NewsScraperService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IEnumerable<HealthNewsArticle>> GetHealthNewsAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                // Configure User-Agent to avoid potential blocking from VNExpress
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                var responseString = await client.GetStringAsync("https://vnexpress.net/rss/suc-khoe.rss");
                if (string.IsNullOrWhiteSpace(responseString))
                {
                    _logger.LogWarning("VNExpress Health RSS returned an empty response.");
                    return Array.Empty<HealthNewsArticle>();
                }

                var xDoc = XDocument.Parse(responseString);
                var items = xDoc.Descendants("item");

                var articles = new List<HealthNewsArticle>();

                foreach (var item in items)
                {
                    var title = item.Element("title")?.Value ?? string.Empty;
                    var link = item.Element("link")?.Value ?? string.Empty;
                    var rawDescription = item.Element("description")?.Value ?? string.Empty;
                    var pubDateStr = item.Element("pubDate")?.Value ?? string.Empty;

                    // Extract image URL from description HTML using Regex
                    string imageUrl = string.Empty;
                    var imgMatch = ImageRegex.Match(rawDescription);
                    if (imgMatch.Success)
                    {
                        imageUrl = imgMatch.Groups[1].Value;
                    }

                    // Remove HTML tags and decode entities for clean description text
                    string cleanDescription = HtmlTagRegex.Replace(rawDescription, string.Empty);
                    cleanDescription = System.Net.WebUtility.HtmlDecode(cleanDescription).Trim();

                    // Parse PubDate
                    DateTime? pubDate = null;
                    if (DateTime.TryParse(pubDateStr, out var parsedDate))
                    {
                        pubDate = parsedDate;
                    }
                    else
                    {
                        _logger.LogWarning($"Could not parse pubDate: {pubDateStr}");
                    }

                    articles.Add(new HealthNewsArticle
                    {
                        Title = title,
                        Link = link,
                        Description = cleanDescription,
                        ImageUrl = imageUrl,
                        PubDate = pubDate
                    });
                }

                return articles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching or parsing VNExpress health news RSS.");
                return Array.Empty<HealthNewsArticle>();
            }
        }
    }
}
