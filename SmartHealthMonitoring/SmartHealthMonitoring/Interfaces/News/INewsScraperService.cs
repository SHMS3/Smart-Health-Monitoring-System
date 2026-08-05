using System.Collections.Generic;
using System.Threading.Tasks;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces.News
{
    public interface INewsScraperService
    {
        Task<IEnumerable<HealthNewsArticle>> GetHealthNewsAsync();
    }
}
