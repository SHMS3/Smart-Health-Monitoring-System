using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin
{
    [Authorize(Roles = "2")]
    public class AdminAuditLogController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;

        public AdminAuditLogController(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? actionType,
            string? entityName,
            string? keyword,
            DateTime? fromDate,
            DateTime? toDate,
            int page = 1,
            int pageSize = 15)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 5, 100);

            var query = _context.AuditLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(actionType))
            {
                query = query.Where(x => x.Action == actionType);
            }

            if (!string.IsNullOrWhiteSpace(entityName))
            {
                query = query.Where(x => x.EntityName == entityName);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(x => x.CreatedAt >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var nextDate = toDate.Value.Date.AddDays(1);
                query = query.Where(x => x.CreatedAt < nextDate);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var term = keyword.Trim();
                query = query.Where(x =>
                    x.ActorName.Contains(term) ||
                    x.ActorEmail.Contains(term) ||
                    (x.TargetName != null && x.TargetName.Contains(term)) ||
                    (x.EntityId != null && x.EntityId.Contains(term)) ||
                    x.Description.Contains(term));
            }

            var totalRecords = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AuditLogListItemViewModel
                {
                    Id = x.Id,
                    ActorName = x.ActorName,
                    ActorEmail = x.ActorEmail,
                    Action = x.Action,
                    EntityName = x.EntityName,
                    EntityId = x.EntityId,
                    TargetName = x.TargetName,
                    Description = x.Description,
                    IpAddress = x.IpAddress,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            var model = new AuditLogIndexViewModel
            {
                ActionType = actionType,
                EntityName = entityName,
                Keyword = keyword,
                FromDate = fromDate,
                ToDate = toDate,
                Logs = new PagedResult<AuditLogListItemViewModel>
                {
                    Items = items,
                    TotalCount = totalRecords,
                    Page = page,
                    PageSize = pageSize
                }
            };

            return View(model);
        }
    }
}
