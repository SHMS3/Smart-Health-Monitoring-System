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

        //        query = query.Where(u =>
        //    }


        //    var items = await query
        //        {
        //            UserId = u.UserId,
        //            RoleId = u.RoleId,
        //            RoleName = u.Role.RoleName,
        //            Username = u.Username,
        //            Email = u.Email,
        //            IsActive = u.IsActive,
        //            CreatedAt = u.CreatedAt
        //        })

        //    return new PagedResult<UserViewModel>
        //    {
        //        Items = items,
        //        TotalCount = total,
        //        Page = page,
        //        PageSize = pageSize
        //    };
        //}

        //{
        //    var user = _context.Users.Find(id);
        //    if (user == null)
        //        throw new Exception("User not found");


        //    _context.Update(user);
        //    _context.SaveChanges();
        //}

        //{
        //    var user = _context.Users.Find(id);
           
        //    if(user == null)
        //    {
        //        throw new Exception("User not found");
        //    }

        //    _context.Update(user);
        //    _context.SaveChanges();
        //}

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
