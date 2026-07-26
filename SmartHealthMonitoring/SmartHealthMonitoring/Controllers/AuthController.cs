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
using Microsoft.Extensions.Caching.Memory;
using SmartHealthMonitoring.Interfaces;

namespace SmartHealthMonitoring.Controllers
{
    public class AuthController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService;

        public AuthController(SmartHealthMonitoringContext context, IMemoryCache cache, IEmailService emailService)
        {
            _context = context;
            _cache = cache;
            _emailService = emailService;
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
                                CreatedAt = SmartHealthMonitoring.Common.AppTime.Now
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
                3 => RedirectToAction("Patients", "Receptionist"),
                2 => RedirectToAction("Index", "AdminDashboard"),
                1 => RedirectToAction("Index", "DoctorDashboard"), 
                _ => RedirectToAction("Index", "Home") 
            };
            Console.WriteLine($"[DEBUG RedirectByRole] Redirecting to: {(result as RedirectToActionResult)?.ControllerName}/{(result as RedirectToActionResult)?.ActionName}");
            return result;
        }
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null || user.IsDeleted)
            {
                ModelState.AddModelError("Email", "Email không tồn tại hoặc tài khoản đã bị khóa.");
                return View(model);
            }

            // Generate 6 digit OTP
            var random = new Random();
            string otp = random.Next(100000, 999999).ToString();

            // Store in Cache for 3 minutes
            _cache.Set($"ResetOTP_{model.Email}", otp, TimeSpan.FromMinutes(3));

            // Send Email
            var replacements = new Dictionary<string, string>
            {
                { "{{OTP_CODE}}", otp }
            };
            string htmlContent = _emailService.GetHtmlContentFromFile("forgot_password.html", replacements);
            await _emailService.SendEmailAsync(model.Email, "Mã xác thực khôi phục mật khẩu - Smart Health Monitoring", htmlContent);

            return RedirectToAction("VerifyResetOtp", new { email = model.Email });
        }

        [HttpGet]
        public IActionResult VerifyResetOtp(string email)
        {
            if (string.IsNullOrEmpty(email)) return RedirectToAction("ForgotPassword");
            return View(new VerifyResetOtpViewModel { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyResetOtp(VerifyResetOtpViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            if (_cache.TryGetValue($"ResetOTP_{model.Email}", out string? storedOtp))
            {
                if (storedOtp == model.Otp)
                {
                    // OTP valid
                    _cache.Remove($"ResetOTP_{model.Email}");
                    // Cấp một token tạm thời để ResetPassword
                    string resetToken = Guid.NewGuid().ToString();
                    _cache.Set($"ResetToken_{model.Email}", resetToken, TimeSpan.FromMinutes(10));
                    
                    return RedirectToAction("ResetPassword", new { email = model.Email, token = resetToken });
                }
            }
            
            ModelState.AddModelError("Otp", "Mã OTP không chính xác hoặc đã hết hạn.");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendResetOtp(string email)
        {
            if (string.IsNullOrEmpty(email))
                return Json(new { success = false, message = "Email không hợp lệ" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
            if (user == null)
                return Json(new { success = false, message = "Email không tồn tại" });

            var random = new Random();
            string otp = random.Next(100000, 999999).ToString();
            _cache.Set($"ResetOTP_{email}", otp, TimeSpan.FromMinutes(3));

            var replacements = new Dictionary<string, string> { { "{{OTP_CODE}}", otp } };
            string htmlContent = _emailService.GetHtmlContentFromFile("forgot_password.html", replacements);
            await _emailService.SendEmailAsync(email, "Mã xác thực khôi phục mật khẩu (Cấp lại)", htmlContent);

            return Json(new { success = true, message = "Đã gửi lại mã OTP thành công!" });
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
                return RedirectToAction("Login");
                
            if (!_cache.TryGetValue($"ResetToken_{email}", out string? storedToken) || storedToken != token)
            {
                TempData["Error"] = "Phiên khôi phục mật khẩu đã hết hạn hoặc không hợp lệ. Vui lòng thực hiện lại.";
                return RedirectToAction("ForgotPassword");
            }

            return View(new ResetPasswordViewModel { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, string token)
        {
            if (!ModelState.IsValid) return View(model);

            if (!_cache.TryGetValue($"ResetToken_{model.Email}", out string? storedToken) || storedToken != token)
            {
                TempData["Error"] = "Phiên khôi phục mật khẩu đã hết hạn. Vui lòng thực hiện lại.";
                return RedirectToAction("ForgotPassword");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user != null)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
                await _context.SaveChangesAsync();
                _cache.Remove($"ResetToken_{model.Email}");
                
                TempData["Success"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login");
            }

            ModelState.AddModelError("", "Đã có lỗi xảy ra.");
            return View(model);
        }
    }
}