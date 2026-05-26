using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using System.Security.Claims;

namespace SmartHealthMonitoring.Controllers
{
    public class AuthController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;

        public AuthController(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET: /Auth/Login
        // ==========================================
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Nếu đã đăng nhập rồi thì redirect theo Role
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectByRole();
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // ==========================================
        // POST: /Auth/Login
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 1. Tìm user theo email
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == model.Email && !u.IsDeleted);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
                return View(model);
            }

            // 2. Kiểm tra mật khẩu bằng BCrypt (Hỗ trợ dữ liệu seed chưa hash)
            bool isPasswordValid = false;
            if (user.PasswordHash.StartsWith("$2a$") || user.PasswordHash.StartsWith("$2b$") || user.PasswordHash.StartsWith("$2y$"))
            {
                isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);
            }
            else
            {
                // Fallback cho dữ liệu seed
                isPasswordValid = (model.Password == user.PasswordHash);
            }

            if (!isPasswordValid)
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
                return View(model);
            }

            // 3. Tạo Claims cho cookie (ĐÃ FIX: Dùng user.Role.ToString() để map với Authorize(Roles="..."))
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("FullName", user.FullName),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddMinutes(60) // Tăng từ 1 phút lên 60 phút để không bị văng sớm
            };

            // 4. Đăng nhập (ghi cookie)
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // 5. Redirect
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != "/")
            {
                return Redirect(returnUrl);
            }

            return RedirectByRole(user.Role);
        }

        // ==========================================
        // GET: /Auth/Register
        // ==========================================
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectByRole();
            }

            return View();
        }

        // ==========================================
        // POST: /Auth/Register
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 1. Kiểm tra email đã tồn tại chưa
            bool emailExists = await _context.Users
                .AnyAsync(u => u.Email == model.Email && !u.IsDeleted);

            if (emailExists)
            {
                ModelState.AddModelError("Email", "Email này đã được sử dụng. Vui lòng dùng email khác.");
                return View(model);
            }

            // 2. Hash mật khẩu
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

            // 3. Tạo User + Patient trong transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    PasswordHash = passwordHash,
                    Role = 0, // Patient
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var patient = new Patient
                {
                    UserId = user.Id,
                    DateOfBirth = model.DateOfBirth,
                    Sex = model.Sex,
                    Phone = model.Phone,
                    IsDeleted = false
                };

                _context.Patients.Add(patient);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // 4. Tự động đăng nhập sau khi đăng ký (ĐÃ FIX: Dùng Role "0")
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Email),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("FullName", user.FullName),
                    new Claim(ClaimTypes.Role, "0")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24) });

                return RedirectByRole(0);
            }
            catch
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi trong quá trình đăng ký. Vui lòng thử lại.");
                return View(model);
            }
        }

        // ==========================================
        // GET: /Auth/GoogleLogin
        // ==========================================
        [HttpGet]
        public IActionResult GoogleLogin(string? returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(GoogleResponse), "Auth", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        // ==========================================
        // GET: /Auth/GoogleResponse
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GoogleResponse(string? returnUrl = null)
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
                return RedirectToAction(nameof(Login));

            var googleEmail = result.Principal?.FindFirstValue(ClaimTypes.Email);
            var googleName = result.Principal?.FindFirstValue(ClaimTypes.Name) ?? googleEmail ?? "Người dùng";

            if (string.IsNullOrEmpty(googleEmail))
                return RedirectToAction(nameof(Login));

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == googleEmail && !u.IsDeleted);

            if (user == null)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    user = new User
                    {
                        FullName = googleName,
                        Email = googleEmail,
                        PasswordHash = string.Empty,
                        Role = 0,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    var patient = new Patient
                    {
                        UserId = user.Id,
                        DateOfBirth = new DateOnly(2000, 1, 1),
                        Sex = 0,
                        IsDeleted = false
                    };
                    _context.Patients.Add(patient);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    return RedirectToAction(nameof(Login));
                }
            }

            // 3. Tạo cookie session (ĐÃ FIX: Dùng user.Role.ToString())
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("FullName", user.FullName),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24) });

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectByRole(user.Role);
        }

        // ==========================================
        // POST: /Auth/Logout
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // GET: /Auth/AccessDenied
        // ==========================================
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ==========================================
        // HELPER: Redirect theo Role (ĐÃ FIX)
        // ==========================================
        private IActionResult RedirectByRole(byte? role = null)
        {
            // Nếu role chưa được truyền vào (VD: gọi từ hàm [HttpGet] Login), lấy role từ Cookie hiện tại
            if (role == null)
            {
                var roleClaim = User.FindFirstValue(ClaimTypes.Role);
                if (byte.TryParse(roleClaim, out byte parsedRole))
                {
                    role = parsedRole;
                }
            }

            // Bẻ lái dựa trên giá trị Role
            return role switch
            {
                2 => RedirectToAction("Index", "AdminDashboard"), // Admin
                1 => RedirectToAction("Index", "DoctorDashboard"), // Bác sĩ
                _ => RedirectToAction("Index", "Home") // Bệnh nhân (0) hoặc các trường hợp khác
            };
        }
    }
}