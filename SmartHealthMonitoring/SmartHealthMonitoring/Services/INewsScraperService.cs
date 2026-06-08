using System.Collections.Generic;
using System.Threading.Tasks;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services
{
    public interface INewsScraperService
    {
        Task<IEnumerable<HealthNewsArticle>> GetHealthNewsAsync();
    }
}
