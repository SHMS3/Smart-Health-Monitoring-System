# Task — Notification (NTF-01 & NTF-02)

| ID | Task | Trạng thái | Mô tả | Hook / Entry point | File chính |
|---|---|---|---|---|---|
| **NTF-01** | Email xác nhận đặt lịch + QR Check-in | ✅ Done | Gửi email template kèm mã QR Check-in ngay khi BOOK-08 (lễ tân duyệt đặt lịch) thành công | `ReceptionistController.ApproveBooking` → `SendBookingConfirmationCheckInAsync` | `ReceptionistController.cs`, `EmailTriggerService.cs`, `AppointmentBookingConfirmationTemplate.html` |
| **NTF-01** | Email QR khi bác sĩ tiếp nhận | ✅ Done | Gửi email template + QR Check-in khi bác sĩ tiếp nhận bệnh nhân trong hàng đợi thành công | `DoctorDashboardController.AcceptPatient` → `SendDoctorAcceptedCheckInAsync` | `DoctorDashboardController.cs`, `EmailTriggerService.cs`, `DoctorAcceptedCheckInTemplate.html` |
| **NTF-01** | Sinh mã & ảnh QR Check-in | ✅ Done | Tạo payload Check-in và PNG QR (QRCoder), nhúng ảnh inline (`cid:`) trong email | `IQrCheckInService` / `QrCheckInService` | `IQrCheckInService.cs`, `QrCheckInService.cs`, `EmailService.cs` |
| **NTF-02** | Background Worker nhắc 24h & 2h | ✅ Done | Job quét DB mỗi 5 phút, tìm lịch `Confirmed` còn ~24h hoặc ~2h (cửa sổ ±5 phút) | `AppointmentReminderWorker` (HostedService) | `Workers/AppointmentReminderWorker.cs`, `Program.cs` |
| **NTF-02** | Email nhắc lịch | ✅ Done | Gửi email template nhắc trước giờ khám (nhãn `24 giờ` / `2 giờ`) | `SendAppointmentReminderAsync` | `EmailTriggerService.cs`, `AppointmentReminderTemplate.html` |
| **NTF-02** | SMS nhắc lịch | ✅ Done | Gửi SMS kèm thông tin giờ khám qua `IOutboundSmsService` (Twilio) | Cùng vòng lặp worker | `AppointmentReminderWorker.cs`, `TwilioSmsService.cs` |
| **NTF-02** | Cờ chống gửi trùng | ✅ Done | Thêm `IsReminded24h`, `IsReminded2h`; set `true` sau khi gửi để không nhắc lại | Migration + update trong worker | `Models/Appointment.cs`, `Migrations/20260711232735_AddAppointmentReminderFlags.*` |

## Ghi chú nhanh

- **NTF-01 (BOOK-08):** sau khi `ApproveAppointmentBookingAsync` thành công → email xác nhận + QR (`SHMS-CHECKIN|A{id}|P…|D…|yyyyMMddHHmm`).
- **QR tiếp nhận:** payload dạng `SHMS-CHECKIN|W{waitingId}|P…|D…|Q{seq}|yyyyMMddHHmm`.
- **NTF-02:** chỉ áp dụng lịch `AppointmentStatus.Confirmed`; Email + SMS; không gửi trùng nhờ 2 cờ reminder.
- **NTF-03 (Waitlist):** đã tạm bỏ, không nằm trong phạm vi bảng này.
- Package mới: `QRCoder` 1.6.0.

## Checklist kiểm thử

| # | Kịch bản | Kỳ vọng |
|---|---|---|
| 1 | Lễ tân duyệt đặt lịch (Pending → Confirmed) | Bệnh nhân nhận email xác nhận + QR Check-in |
| 2 | Bác sĩ bấm Tiếp nhận trên WaitingList | Bệnh nhân nhận email + QR Check-in |
| 3 | Có lịch Confirmed còn ~24h, `IsReminded24h = false` | Worker gửi Email/SMS, set `IsReminded24h = true` |
| 4 | Có lịch Confirmed còn ~2h, `IsReminded2h = false` | Worker gửi Email/SMS, set `IsReminded2h = true` |
| 5 | Chạy lại worker sau khi đã nhắc | Không gửi trùng (cờ đã `true`) |

---

# Task — Admin Calendar / Booking Analytics (ADM)

| ID | Task | Trạng thái | Mô tả | Hook / Entry point | File chính |
|---|---|---|---|---|---|
| **ADM-01** | Calendar Dashboard (Admin View) | ✅ Done | Giao diện lịch (kiểu Google Calendar) tổng quan tất cả bác sĩ trong tuần; lọc bác sĩ; xem Tuần / Ngày / Danh sách; click sự kiện xem chi tiết | `AdminCalendarController.Index` / `Events` | `AdminCalendarController.cs`, `Views/AdminCalendar/Index.cshtml`, `AdminCalendarViewModels.cs`, `_AdminLayout.cshtml` |
| **ADM-02** | Báo cáo No-show & Tỷ lệ huỷ | ✅ Done | Biểu đồ tròn/cột thống kê % BN bỏ lịch / huỷ lịch theo từng bác sĩ; lọc khoảng ngày; bảng chi tiết | `AdminCalendarController.NoShowReport` | `AdminCalendarController.cs`, `Views/AdminCalendar/NoShowReport.cshtml`, `AdminCalendarViewModels.cs`, `_AdminLayout.cshtml` |
| **ADM-03** | Heatmap Giờ Cao Điểm | ✅ Done | Bảng heatmap màu đậm/nhạt theo số lượng đặt lịch theo khung giờ × ngày trong tuần; lọc khoảng ngày; độ phân giải 30p/1h | `AdminCalendarController.Heatmap` | `AdminCalendarController.cs`, `Views/AdminCalendar/Heatmap.cshtml`, `AdminCalendarViewModels.cs`, `_AdminLayout.cshtml` |

## Checklist kiểm thử ADM-01

| # | Kịch bản | Kỳ vọng |
|---|---|---|
| 1 | Admin mở `/AdminCalendar` | Thấy lịch tuần (T2–CN), sidebar danh sách bác sĩ, thống kê tuần |
| 2 | Có lịch hẹn trong tuần | Event hiện trên lưới theo giờ, màu theo bác sĩ |
| 3 | Bỏ chọn / chọn bác sĩ trên sidebar | Lịch lọc đúng bác sĩ |
| 4 | Chuyển Tuần / Ngày / Danh sách | View đổi, events load lại qua `GET /AdminCalendar/Events` |
| 5 | Click một event | Modal hiện bác sĩ, BN, giờ, trạng thái, ghi chú |
| 6 | Có slot Blocked | Hiện event xám “Blocked” trên lịch |

## Checklist kiểm thử ADM-02

| # | Kịch bản | Kỳ vọng |
|---|---|---|
| 1 | Admin mở `/AdminCalendar/NoShowReport` | Thấy KPI + biểu đồ doughnut + bar theo bác sĩ |
| 2 | Có lịch NoShow / Cancelled trong khoảng | % No-show và % Huỷ hiển thị đúng theo bác sĩ |
| 3 | Đổi khoảng ngày (7/30/tháng / custom) | Dữ liệu và chart cập nhật theo filter |
| 4 | Hover chart | Tooltip hiện số lượng / % |
| 5 | Xem bảng chi tiết | Cột No-show, BN huỷ, BS huỷ, % có badge màu theo mức độ |

## Checklist kiểm thử ADM-03

| # | Kịch bản | Kỳ vọng |
|---|---|---|
| 1 | Admin mở `/AdminCalendar/Heatmap` | Thấy bảng heatmap T2–CN × khung giờ 07:00–19:00 |
| 2 | Có lịch đặt trong khoảng | Ô có số lượng, màu đậm hơn khi nhiều hơn |
| 3 | Ô max | Có viền đỏ (peak); KPI hiện giờ/ngày cao điểm |
| 4 | Đổi 30 phút / 1 giờ | Lưới thay đổi độ phân giải |
| 5 | Lọc 7 ngày / 4 tuần / tháng | Dữ liệu heatmap cập nhật theo khoảng |
