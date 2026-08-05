using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces.Email;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Services.Email
{
    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly SmartHealthMonitoringContext _context;

        public EmailNotificationService(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<EmailHistoryIndexViewModel> GetFilteredAsync(
            int? currentDoctorId,
            bool isDoctorRole,
            byte? status,
            string? emailType,
            DateTime? fromDate,
            DateTime? toDate,
            string? keyword,
            int? patientId,
            string? sender,
            int page,
            int pageSize)
        {
            var today = DateTime.Today;
            fromDate ??= today;
            toDate ??= today;
            page = Math.Max(page, 1);

            IQueryable<EmailNotification> accessibleEmails = _context.EmailNotifications.AsNoTracking();

            if (isDoctorRole && currentDoctorId.HasValue)
            {
                accessibleEmails = accessibleEmails.Where(e =>
                    e.SentByDoctorId == null ||
                    e.SentByDoctorId == currentDoctorId.Value);
            }

            var query = accessibleEmails
                .Include(e => e.Patient).ThenInclude(p => p.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(e => (e.Patient != null && e.Patient.User != null && e.Patient.User.FullName.Contains(keyword)) ||
                                         e.ToEmail.Contains(keyword));
            }

            if (status.HasValue)
                query = query.Where(e => e.Status == status.Value);

            if (patientId.HasValue)
                query = query.Where(e => e.PatientId == patientId.Value);

            if (!string.IsNullOrWhiteSpace(sender))
            {
                if (sender == "system")
                {
                    query = query.Where(e => e.SentByDoctorId == null);
                }
                else if (sender.StartsWith("doctor:", StringComparison.OrdinalIgnoreCase) &&
                         int.TryParse(sender.Substring("doctor:".Length), out var senderDoctorId))
                {
                    query = query.Where(e => e.SentByDoctorId == senderDoctorId);
                }
            }

            if (fromDate.HasValue)
                query = query.Where(e => e.CreatedAt >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(e => e.CreatedAt < toDate.Value.Date.AddDays(1));

            if (!string.IsNullOrEmpty(emailType))
            {
                if (emailType == "M?i t�i kh�m")
                    query = query.Where(e => e.Subject.Contains("T�i Kh�m") || e.Subject.Contains("T�i kh�m") || e.Subject.Contains("t�i kh�m"));
                else if (emailType == "C?nh b�o s?c kh?e")
                    query = query.Where(e => e.Subject.Contains("C?NH B�O") || e.Subject.Contains("C?nh b�o") || e.Subject.Contains("c?nh b�o"));
                else if (emailType == "Nh?c ghi ch? s?")
                    query = query.Where(e => e.Status == 3);
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            var emailsList = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var doctorIds = emailsList
                .Where(e => e.SentByDoctorId.HasValue)
                .Select(e => e.SentByDoctorId!.Value)
                .Distinct()
                .ToList();

            var doctorNames = await _context.Doctors
                .Include(d => d.User)
                .Where(d => doctorIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.User?.FullName ?? "B�c si");

            var dtos = emailsList.Select(e => new EmailHistoryDto
            {
                Id = e.Id,
                PatientName = e.Patient?.User?.FullName ?? "Kh�ng r�",
                ToEmail = e.ToEmail,
                Subject = e.Subject,
                Status = e.Status,
                StatusDisplay = GetStatusDisplay(e.Status),
                CreatedAt = e.CreatedAt,
                SentAt = e.SentAt,
                ErrorMessage = GetErrorDisplay(e.ErrorMessage),
                Body = e.Body,
                AlertId = e.AlertId > 0 ? e.AlertId : null,
                EmailType = e.Status == 3 ? "Nh?c ghi ch? s?" : GetEmailType(e.Subject),
                SenderName = e.SentByDoctorId.HasValue
                    ? (doctorNames.TryGetValue(e.SentByDoctorId.Value, out var name) ? name : "B�c si")
                    : "H? th?ng t? d?ng"
            }).ToList();

            var since7Days = DateTime.Now.AddDays(-7);
            var statsAll = await accessibleEmails
                .Where(e => e.CreatedAt >= since7Days)
                .ToListAsync();

            var stats = new EmailStats
            {
                TotalLast7Days = statsAll.Count,
                Succeeded = statsAll.Count(e => e.Status == 1),
                Failed = statsAll.Count(e => e.Status == 2),
                ByAI = statsAll.Count(e => e.SentByDoctorId == null),
                ByDoctor = statsAll.Count(e => e.SentByDoctorId != null)
            };

            var patientOptions = await accessibleEmails
                .Select(e => new
                {
                    e.PatientId,
                    PatientName = e.Patient.User.FullName ?? e.ToEmail
                })
                .Distinct()
                .OrderBy(e => e.PatientName)
                .ToListAsync();

            var senderDoctorIds = await accessibleEmails
                .Where(e => e.SentByDoctorId.HasValue)
                .Select(e => e.SentByDoctorId!.Value)
                .Distinct()
                .ToListAsync();

            var senderDoctors = await _context.Doctors
                .Include(d => d.User)
                .Where(d => senderDoctorIds.Contains(d.Id))
                .OrderBy(d => d.User!.FullName)
                .Select(d => new
                {
                    d.Id,
                    DoctorName = d.User != null ? d.User.FullName : "B�c si"
                })
                .ToListAsync();

            var hasSystemSender = await accessibleEmails
                .AnyAsync(e => e.SentByDoctorId == null);

            var viewModel = new EmailHistoryIndexViewModel
            {
                Emails = dtos,
                FilterStatus = status,
                FilterEmailType = emailType,
                FromDate = fromDate,
                ToDate = toDate,
                FilterKeyword = keyword,
                FilterPatientId = patientId,
                FilterSender = sender,
                PatientOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "B?nh nh�n" }
                },
                SenderOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "Ngu?i g?i" }
                },
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                Stats = stats
            };

            viewModel.PatientOptions.AddRange(patientOptions.Select(p => new SelectListItem
            {
                Value = p.PatientId.ToString(),
                Text = p.PatientName
            }));

            if (hasSystemSender)
            {
                viewModel.SenderOptions.Add(new SelectListItem
                {
                    Value = "system",
                    Text = "H? th?ng t? d?ng"
                });
            }

            viewModel.SenderOptions.AddRange(senderDoctors.Select(d => new SelectListItem
            {
                Value = $"doctor:{d.Id}",
                Text = d.DoctorName
            }));

            return viewModel;
        }

        private static string GetStatusDisplay(byte status) => status switch
        {
            0 => "Ch? g?i",
            1 => "Th�nh c�ng",
            2 => "Th?t b?i",
            3 => "Th�ng b�o n?i b?",
            _ => "Kh�ng x�c d?nh"
        };

        private static string? GetErrorDisplay(string? errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                return null;

            return errorMessage.Contains("Daily user sending limit exceeded", StringComparison.OrdinalIgnoreCase)
                || errorMessage.Contains("5.4.5", StringComparison.OrdinalIgnoreCase)
                    ? "T�i kho?n Gmail d� d?t gi?i h?n g?i email trong ng�y. Vui l�ng ch? Google kh�i ph?c h?n m?c ho?c d?i t�i kho?n g?i email."
                    : "Kh�ng th? g?i email. Vui l�ng ki?m tra c?u h�nh g?i email ho?c th? l?i sau.";
        }

        private static string GetEmailType(string subject)
        {
            if (string.IsNullOrWhiteSpace(subject)) return "Kh�c";

            if (subject.Contains("T�i Kh�m", StringComparison.OrdinalIgnoreCase) ||
                subject.Contains("t�i kh�m", StringComparison.OrdinalIgnoreCase))
                return "M?i t�i kh�m";

            if (subject.Contains("C?NH B�O", StringComparison.OrdinalIgnoreCase) ||
                subject.Contains("c?nh b�o", StringComparison.OrdinalIgnoreCase))
                return "C?nh b�o s?c kh?e";

            return "Kh�c";
        }
    }
}
