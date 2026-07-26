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
            var actors = await _context.Users
                .AsNoTracking()
                .Where(x => x.Role == 1 || x.Role == 2)
                .OrderByDescending(x => x.Role)
                .ThenBy(x => x.FullName)
                .Select(x => new AuditLogActorOptionViewModel
                {
                    Id = x.Id,
                    FullName = x.FullName,
                    Email = x.Email,
                    Role = x.Role,
                    IsDeleted = x.IsDeleted
                })
                .ToListAsync();

            if (normalizedFromDate.HasValue &&
                normalizedToDate.HasValue &&
                normalizedFromDate.Value > normalizedToDate.Value)
            {
                ModelState.AddModelError(nameof(fromDate), "Từ ngày không được lớn hơn Đến ngày.");
            }

            if (normalizedToDate.HasValue && normalizedToDate.Value > today)
            {
                ModelState.AddModelError(nameof(toDate), "Đến ngày không được vượt quá ngày hiện tại.");
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

            var query = _context.AuditLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(actionType))
            {
                query = query.Where(x => x.Action == actionType);
            }

            if (!string.IsNullOrWhiteSpace(entityName))
            {
                query = query.Where(x => x.EntityName == entityName);
            }

            if (actorUserId.HasValue)
            {
                query = query.Where(x => x.ActorUserId == actorUserId.Value);
            }

            if (normalizedFromDate.HasValue)
            {
                var fromUtc = normalizedFromDate.Value.Subtract(vietnamUtcOffset);
                query = query.Where(x => x.CreatedAt >= fromUtc);
            }

            if (normalizedToDate.HasValue)
            {
                var toUtcExclusive = normalizedToDate.Value.AddDays(1).Subtract(vietnamUtcOffset);
                query = query.Where(x => x.CreatedAt < toUtcExclusive);
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
