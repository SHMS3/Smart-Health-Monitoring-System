using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace SmartHealthMonitoring.Repositories
{
    public class UserRepository
    {
        private readonly SmartHealthMonitoringContext _context;
        public UserRepository(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        //public async Task<PagedResult<UserViewModel>> GetAllUserAsync(string? search, int page, int pageSize, string? status)
        //{
        //    var query = _context.Users.Include(u => u.Role).AsQueryable();

        //    if (!string.IsNullOrWhiteSpace(status))
        //    {
        //        if (status == "active")
        //        {
        //            query = query.Where(x => x.IsActive == true);
        //        }
        //        else if (status == "locked")
        //        {
        //            query = query.Where(x => x.IsActive == false);
        //        }
        //    }

        //    if (!string.IsNullOrWhiteSpace(search))
        //    {
        //        string s = search.Trim().ToLower();

        //        query = query.Where(u =>
        //            u.Email.ToLower().Contains(s) ||
        //            u.Username.ToLower().Contains(s));
        //    }

        //    int total = await query.CountAsync();

        //    var items = await query
        //        .OrderBy(u => u.UserId)
        //        .Skip((page - 1) * pageSize)
        //        .Take(pageSize)
        //        .Select(u => new UserViewModel
        //        {
        //            UserId = u.UserId,
        //            RoleId = u.RoleId,
        //            RoleName = u.Role.RoleName,
        //            Username = u.Username,
        //            Email = u.Email,
        //            IsActive = u.IsActive,
        //            CreatedAt = u.CreatedAt
        //        })
        //        .ToListAsync();

        //    return new PagedResult<UserViewModel>
        //    {
        //        Items = items,
        //        TotalCount = total,
        //        Page = page,
        //        PageSize = pageSize
        //    };
        //}

        //public void UpdateAccount(Guid id, UserViewModel userVM)
        //{
        //    var user = _context.Users.Find(id);
        //    if (user == null)
        //        throw new Exception("User not found");

        //    user.RoleId = userVM.RoleId;

        //    _context.Update(user);
        //    _context.SaveChanges();
        //}

        //public void LockAccount(Guid id, string status)
        //{
        //    var user = _context.Users.Find(id);
           
        //    if(user == null)
        //    {
        //        throw new Exception("User not found");
        //    }
        //    user.IsActive = status == "active";

        //    _context.Update(user);
        //    _context.SaveChanges();
        //}

        //public async Task<List<Role>> GetAllRoles() => await _context.Roles.ToListAsync();
        //public async Task<UserViewModel?> GetUserVmById(Guid id)
        //{
        //    var user = await _context.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.UserId == id);

        //    if (user == null)
        //        return null;

        //    return new UserViewModel
        //    {
        //        UserId = user.UserId,
        //        Username = user.Username,
        //        Email = user.Email,
        //        RoleId = user.RoleId,
        //        RoleName = user.Role.RoleName,
        //    };

        //}
    }
}
