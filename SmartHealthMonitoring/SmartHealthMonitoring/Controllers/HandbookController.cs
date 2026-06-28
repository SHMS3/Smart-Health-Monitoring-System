using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SmartHealthMonitoring.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartHealthMonitoring.Interfaces;

namespace SmartHealthMonitoring.Controllers
{
    public class HandbookController : Controller
    {
        private readonly INewsScraperService _newsScraperService;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "VnExpressHealthNews";

        public HandbookController(INewsScraperService newsScraperService, IMemoryCache cache)
        {
            _newsScraperService = newsScraperService;
            _cache = cache;
        }

        public async Task<IActionResult> Index()
        {
            if (!_cache.TryGetValue(CacheKey, out IEnumerable<HealthNewsArticle>? articles))
            {
                // Cache Miss - scrape the news
                articles = await _newsScraperService.GetHealthNewsAsync();

                // Store in Cache with absolute expiration of 30 minutes
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

                _cache.Set(CacheKey, articles, cacheEntryOptions);
            }

            return View(articles);
        }
    }
}
