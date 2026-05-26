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

            // 3. Tạo Claims cho cookie
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("FullName", user.FullName),
                new Claim(ClaimTypes.Role, user.Role == 1 ? "Doctor" : "Patient")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddMinutes(30)
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

                // 4. Tự động đăng nhập sau khi đăng ký
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Email),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("FullName", user.FullName),
                    new Claim(ClaimTypes.Role, "Patient")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24) });

                return RedirectToAction("Index", "Patient");
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
            // Trỏ RedirectUri về GoogleResponse để tránh trùng lặp với CallbackPath của Google Middleware
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
            // 1. Đọc thông tin từ cookie do Google Middleware vừa tạo ra
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
                return RedirectToAction(nameof(Login));

            var googleEmail = result.Principal?.FindFirstValue(ClaimTypes.Email);
            var googleName  = result.Principal?.FindFirstValue(ClaimTypes.Name) ?? googleEmail ?? "Người dùng";

            if (string.IsNullOrEmpty(googleEmail))
                return RedirectToAction(nameof(Login));

            // 2. Tìm hoặc tạo user
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == googleEmail && !u.IsDeleted);

            if (user == null)
            {
                // Tạo user mới (Patient)
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    user = new User
                    {
                        FullName = googleName,
                        Email = googleEmail,
                        PasswordHash = string.Empty, // Không có mật khẩu vì login bằng Google
                        Role = 0, // Patient
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    var patient = new Patient
                    {
                        UserId = user.Id,
                        DateOfBirth = new DateOnly(2000, 1, 1), // Mặc định, có thể cập nhật sau
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

            // 3. Tạo cookie session
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("FullName", user.FullName),
                new Claim(ClaimTypes.Role, user.Role == 1 ? "Doctor" : "Patient")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24) });

            // 4. Redirect
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != "/")
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
        // HELPER: Redirect theo Role
        // ==========================================
        private IActionResult RedirectByRole(byte? role = null)
        {
            if (role == 1 || (role == null && User.IsInRole("Doctor")))
            {
                return RedirectToAction("Index", "DoctorDashboard");
            }
            else if (role == 0 || (role == null && User.IsInRole("Patient")))
            {
                return RedirectToAction("Index", "Patient");
            }

            return RedirectToAction("Index", "Home");
        }
    } 
}
