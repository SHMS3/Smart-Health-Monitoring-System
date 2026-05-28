using Microsoft.AspNetCore.Authorization; 
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "1")] // Chỉ cho phép Bác sĩ (Role = 1) truy cập
    public class DoctorDashboardController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;

        public DoctorDashboardController(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10) // Thêm tham số phân trang
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                // 1. Dựng Query cơ sở (Chưa thực thi xuống Database)
                var query = _context.Patients
                    .Include(p => p.User)
                    .Where(p => !p.IsDeleted && !p.User.IsDeleted && p.User.Role == 0);

                // 2. Đếm tổng số bệnh nhân thỏa mãn điều kiện (Hit DB lần 1)
                int totalRecords = await query.CountAsync();

                // 3. Thực hiện phân trang và map dữ liệu sang ViewModel (Hit DB lần 2)
                var items = await query
                    .OrderByDescending(p => p.User.CreatedAt) // Sắp xếp bệnh nhân mới lên đầu
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new PatientListViewModel
                    {
                        PatientId = p.Id,
                        FullName = p.User.FullName,
                        Age = today.Year - p.DateOfBirth.Year - (today.DayOfYear < p.DateOfBirth.DayOfYear ? 1 : 0),
                        SexDisplay = p.Sex == 1 ? "Nam" : "Nữ",
                        Phone = p.Phone ?? "N/A"
                    })
                    .ToListAsync();

                // 4. Đóng gói kết quả vào class PagedResult của bạn
                var result = new PagedResult<PatientListViewModel>
                {
                    Items = items,
                    TotalCount = totalRecords,
                    Page = page,
                    PageSize = pageSize
                };

                return View(result);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi tải danh sách bệnh nhân: " + ex.Message;

                // Trường hợp lỗi, trả về một PagedResult trống để giao diện không bị crash
                return View(new PagedResult<PatientListViewModel>());
            }
        }
    }
}