using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
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

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectByRole();
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
                return View(model);
            }
            
            bool isPasswordValid = false;
            if (user.PasswordHash.StartsWith("$2a$") || user.PasswordHash.StartsWith("$2b$") || user.PasswordHash.StartsWith("$2y$"))
            {
                isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);
            }
            else
            {
                isPasswordValid = (model.Password == user.PasswordHash);
            }

            if (!isPasswordValid)
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
                return View(model);
            }

            // 3. KIỂM TRA TRẠNG THÁI KHÓA (Kiểm tra sau khi đã nhập đúng mật khẩu)
            if (user.IsDeleted)
            {
                string reason = string.IsNullOrWhiteSpace(user.LockReason)
                    ? "Vui lòng liên hệ Admin để biết thêm chi tiết."
                    : user.LockReason;

                ModelState.AddModelError(string.Empty, $"Tài khoản của bạn đã bị khóa! Lý do: {reason}");
                return View(model);
            }

            // 4. Tạo Claims cho cookie
            Console.WriteLine($"[DEBUG LOGIN] Email={user.Email}, UserId={user.Id}, Role={user.Role}");
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
                    : DateTimeOffset.UtcNow.AddMinutes(60)
            };

            // 5. Đăng nhập (ghi cookie)
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // 6. Bật ca trực tự động nếu là Bác sĩ (role = 1)
            if (user.Role == 1)
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id && !d.IsDeleted);
                if (doctor != null)
                {
                    doctor.IsOnShift = true;
                    await _context.SaveChangesAsync();
                }
            }

            // 7. Redirect
            Console.WriteLine($"[DEBUG LOGIN] returnUrl='{returnUrl}'");
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != "/")
            {
                Console.WriteLine($"[DEBUG LOGIN] Redirecting to returnUrl: {returnUrl}");
                return Redirect(returnUrl);
            }

            Console.WriteLine($"[DEBUG LOGIN] Calling RedirectByRole({user.Role})");
            return RedirectByRole(user.Role);
        }


        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectByRole();
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            bool emailExists = await _context.Users
                .AnyAsync(u => u.Email == model.Email && !u.IsDeleted);

            if (emailExists)
            {
                ModelState.AddModelError("Email", "Email này đã được sử dụng. Vui lòng dùng email khác.");
                return View(model);
            }

            if (model.DateOfBirth > DateOnly.FromDateTime(DateTime.Now))
            {
                ModelState.AddModelError("DateOfBirth", "Ngày sinh không được lớn hơn ngày hiện tại.");
                return View(model);
            }

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                return await strategy.ExecuteAsync(async () =>
                {
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
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi trong quá trình đăng ký. Vui lòng thử lại.");
                return View(model);
            }
        }


        [HttpGet]
        public IActionResult GoogleLogin(string? returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(GoogleResponse), "Auth", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }


        [HttpGet("/Auth/GoogleResponse")]
        [AllowAnonymous]
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
                var googleStrategy = _context.Database.CreateExecutionStrategy();
                try
                {
                    user = await googleStrategy.ExecuteAsync(async () =>
                    {
                        await using var transaction = await _context.Database.BeginTransactionAsync();
                        try
                        {
                            var newUser = new User
                            {
                                FullName = googleName,
                                Email = googleEmail,
                                PasswordHash = string.Empty,
                                Role = 0,
                                IsDeleted = false,
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.Users.Add(newUser);
                            await _context.SaveChangesAsync();

                            var patient = new Patient
                            {
                                UserId = newUser.Id,
                                DateOfBirth = new DateOnly(2000, 1, 1),
                                Sex = 0,
                                IsDeleted = false
                            };
                            _context.Patients.Add(patient);
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                            return newUser;
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    });
                }
                catch
                {
                    return RedirectToAction(nameof(Login));
                }
            }

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


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Tắt ca trực khi Bác sĩ đăng xuất
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var roleString = User.FindFirstValue(ClaimTypes.Role);
            if (!string.IsNullOrEmpty(userIdString) && roleString == "1" && int.TryParse(userIdString, out int userId))
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
                if (doctor != null)
                {
                    doctor.IsOnShift = false;
                    await _context.SaveChangesAsync();
                }
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }


        private IActionResult RedirectByRole(byte? role = null)
        {
            if (role == null)
            {
                var roleClaim = User.FindFirstValue(ClaimTypes.Role);
                Console.WriteLine($"[DEBUG RedirectByRole] roleClaim from cookie = '{roleClaim}'");
                if (byte.TryParse(roleClaim, out byte parsedRole))
                {
                    role = parsedRole;
                }
            }

            Console.WriteLine($"[DEBUG RedirectByRole] Final role = {role}");
            var result = role switch
            {
                3 => RedirectToAction("Index", "Receptionist"),
                2 => RedirectToAction("Index", "AdminDashboard"),
                1 => RedirectToAction("Index", "DoctorDashboard"), 
                _ => RedirectToAction("Index", "Home") 
            };
            Console.WriteLine($"[DEBUG RedirectByRole] Redirecting to: {(result as RedirectToActionResult)?.ControllerName}/{(result as RedirectToActionResult)?.ActionName}");
            return result;
        }
    }
}