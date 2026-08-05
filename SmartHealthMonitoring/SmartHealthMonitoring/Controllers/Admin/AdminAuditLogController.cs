using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Interfaces.Audit;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin
{
    [Authorize(Roles = "2")]
    public class AdminAuditLogController : Controller
    {
        private readonly IAuditLogService _auditLogService;

        public AdminAuditLogController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? actionType,
            string? entityName,
            int? actorUserId,
            DateTime? fromDate,
            DateTime? toDate,
            int page = 1,
            int pageSize = 15)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 5, 100);
            var vietnamUtcOffset = TimeSpan.FromHours(7);
            var normalizedFromDate = fromDate?.Date;
            var normalizedToDate = toDate?.Date;
            var today = SmartHealthMonitoring.Common.AppTime.Now.Add(vietnamUtcOffset).Date;
            
            var actors = await _auditLogService.GetActorOptionsAsync();

            if (normalizedFromDate.HasValue &&
                normalizedToDate.HasValue &&
                normalizedFromDate.Value > normalizedToDate.Value)
            {
                ModelState.AddModelError(nameof(fromDate), "T? ng�y kh�ng du?c l?n hon �?n ng�y.");
            }

            if (normalizedToDate.HasValue && normalizedToDate.Value > today)
            {
                ModelState.AddModelError(nameof(toDate), "�?n ng�y kh�ng du?c vu?t qu� ng�y hi?n t?i.");
            }

            if (!ModelState.IsValid)
            {
                var invalidModel = new AuditLogIndexViewModel
                {
                    ActionType = actionType,
                    EntityName = entityName,
                    ActorUserId = actorUserId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Actors = actors,
                    Logs = new PagedResult<AuditLogListItemViewModel>
                    {
                        Items = new List<AuditLogListItemViewModel>(),
                        TotalCount = 0,
                        Page = page,
                        PageSize = pageSize
                    }
                };

                return View(invalidModel);
            }

            var (items, totalRecords) = await _auditLogService.GetFilteredLogsAsync(
                actionType, entityName, actorUserId, fromDate, toDate, page, pageSize);

            var model = new AuditLogIndexViewModel
            {
                ActionType = actionType,
                EntityName = entityName,
                ActorUserId = actorUserId,
                FromDate = fromDate,
                ToDate = toDate,
                Actors = actors,
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
