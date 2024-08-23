using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace HelloDotNet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookController(
        IMemoryCache memoryCache,
        IDistributedCache redisCache,
        IHttpClientFactory httpClientFactory) : ControllerBase
    {
        private readonly IMemoryCache _memoryCache = memoryCache;
        private readonly IDistributedCache _redisCache = redisCache;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBookById(string id)
        {
            var cacheKey = $"book_{id}";
            var dataOrigin = "none";

            if (_memoryCache.TryGetValue(cacheKey, out string bookContent))
            {
                dataOrigin = "memory";
            }

            if (bookContent == null)
            {
                var redisContent = await _redisCache.GetStringAsync(cacheKey);
                if (redisContent != null)
                {
                    _memoryCache.Set(cacheKey, redisContent, TimeSpan.FromMinutes(5));
                    bookContent = redisContent;
                    dataOrigin = "redis_cache";
                }
            }

            if (bookContent == null)
            {
                var client = _httpClientFactory.CreateClient();
                var apiUrl = $"https://get.taaghche.com/v2/book/{id}";
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    bookContent = await response.Content.ReadAsStringAsync();
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromSeconds(300));

                    await _redisCache.SetStringAsync(cacheKey, bookContent);

                    _memoryCache.Set(cacheKey, bookContent, cacheEntryOptions);

                    dataOrigin = "upstream";
                }
            }

            Response.Headers.Add("data-origin", dataOrigin);
            return Ok(bookContent);
        }
    }
}