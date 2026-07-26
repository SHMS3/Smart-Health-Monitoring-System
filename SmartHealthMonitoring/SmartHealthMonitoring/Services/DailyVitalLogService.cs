using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Repositories;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Services
{
    public class DailyVitalLogService
    {
        private readonly DailyVitalLogRepository _repository;
        private readonly PatientRepository _patientRepository;

        public DailyVitalLogService(DailyVitalLogRepository repository, PatientRepository patientRepository)
        {
            _repository = repository;
            _patientRepository = patientRepository;
        }

        public async Task<PagedResult<DailyVitalLogViewModel>> GetPatientVitalsHistoryAsync( int userId, DateTime? fromDate, DateTime? toDate, int pageIndex = 1, int pageSize = 10)
        {
            var patient = await _patientRepository.GetByUserIdAsync(userId);

            if (patient == null)
                throw new Exception("Không tìm thấy hồ sơ bệnh nhân");

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            {
                throw new ArgumentException("Khoảng thời gian không hợp lệ: 'Từ ngày' không được lớn hơn 'Đến ngày'.");
            }

            var today = DateTime.Now.Date;
            if ((fromDate.HasValue && fromDate.Value.Date > today) ||
                (toDate.HasValue && toDate.Value.Date > today))
            {
                throw new ArgumentException("Không thể tìm kiếm dữ liệu ở tương lai.");
            }

            var threshold = await _repository.GetPatientThresholdAsync(patient.Id);

            var pagedEntity = await _repository.GetAllDailyLogByPatientIdAsync(patient.Id, fromDate, toDate, pageIndex, pageSize);

            var viewModels = pagedEntity.Items.Select(entity =>
            {
                var vm = new DailyVitalLogViewModel
                {
                    Id = entity.Id,
                    LoggedAt = entity.LoggedAt,
                    SystolicBp = entity.SystolicBp,
                    DiastolicBp = entity.DiastolicBp,
                    HeartRate = entity.HeartRate,
                    ChestPainLevel = entity.ChestPainLevel,
                    HasExerciseAngina = entity.HasExerciseAngina
                };

                // GÁN MÀU SẮC ĐỘNG THEO CẤU HÌNH
                vm.AlertLevel = EvaluateAlertLevel(vm, threshold);
                return vm;

            }).ToList();

            return new PagedResult<DailyVitalLogViewModel>
            {
                Items = viewModels,
                TotalCount = pagedEntity.TotalCount,
                Page = pagedEntity.Page,
                PageSize = pagedEntity.PageSize
            };
        }

        public async Task CreateLogAsync(int userId, DailyVitalLogViewModel model)
        {
            var patient = await _patientRepository.GetByUserIdAsync(userId);

            if (patient == null)
                throw new Exception("Không tìm thấy hồ sơ bệnh nhân");

            await _repository.LockPreviousLogsAsync(patient.Id);

            // 1. Lấy ngưỡng hiện tại của bệnh nhân
            var threshold = await _repository.GetPatientThresholdAsync(patient.Id);

            // 2. Tự động tính toán mức độ nguy hiểm dựa trên ngưỡng vừa lấy
            model.AlertLevel = EvaluateAlertLevel(model, threshold);

            var entity = new DailyVitalLog
            {
                PatientId = patient.Id,
                LoggedAt = DateTime.Now,

                SystolicBp = model.SystolicBp!.Value,
                DiastolicBp = model.DiastolicBp!.Value,
                HeartRate = model.HeartRate!.Value,
                ChestPainLevel = model.ChestPainLevel,
                HasExerciseAngina = model.HasExerciseAngina,

                UpdateCount = 0,
                IsUpdateLocked = false,
                IsDeleted = false
            };

            await _repository.CreateDailyLogAsync(entity);
        }

        public async Task<DailyVitalLogViewModel?> GetDailyLogDetailsAsync(int id)
        {
            var entity = await _repository.GetDailyLogByIdAsync(id);
            if (entity == null) return null;

            // Load ngưỡng riêng của bệnh nhân để tính màu sắc đúng
            var threshold = await _repository.GetPatientThresholdAsync(entity.PatientId);

            var vm = new DailyVitalLogViewModel
            {
                Id = entity.Id,
                LoggedAt = entity.LoggedAt,
                SystolicBp = entity.SystolicBp,
                DiastolicBp = entity.DiastolicBp,
                HeartRate = entity.HeartRate,
                ChestPainLevel = entity.ChestPainLevel,
                HasExerciseAngina = entity.HasExerciseAngina,
                UpdateCount = entity.UpdateCount,
                IsUpdateLocked = entity.IsUpdateLocked,
                CanUpdate = !entity.IsUpdateLocked && entity.UpdateCount < 2,

                // Gán ngưỡng vào VM để View hiển thị đúng
                SystolicBpWarning  = threshold?.SystolicBpWarning  ?? 130,
                SystolicBpDanger   = threshold?.SystolicBpDanger   ?? 140,
                DiastolicBpWarning = threshold?.DiastolicBpWarning ?? 80,
                DiastolicBpDanger  = threshold?.DiastolicBpDanger  ?? 90,
                HeartRateWarningMin = threshold?.HeartRateWarningMin ?? 60,
                HeartRateDangerMin  = threshold?.HeartRateDangerMin  ?? 50,
                HeartRateWarningMax = threshold?.HeartRateWarningMax ?? 100,
                HeartRateDangerMax  = threshold?.HeartRateDangerMax  ?? 120,
            };

            // Tính AlertLevel theo ngưỡng của bệnh nhân (có fallback về default nếu chưa cài)
            vm.AlertLevel = EvaluateAlertLevel(vm, threshold);

            return vm;
        }

        public async Task<DailyVitalLogViewModel?> GetLogForUpdateAsync(int id)
        {
            var entity = await _repository.GetDailyLogByIdAsync(id);
            if (entity == null) return null;

            return new DailyVitalLogViewModel
            {
                Id = entity.Id,
                LoggedAt = entity.LoggedAt, // Cần lấy ngày cũ lên để UI biết
                SystolicBp = entity.SystolicBp,
                DiastolicBp = entity.DiastolicBp,
                HeartRate = entity.HeartRate,
                ChestPainLevel = entity.ChestPainLevel,
                HasExerciseAngina = entity.HasExerciseAngina
            };
        }

        public async Task<bool> UpdateLogAsync(int id, DailyVitalLogViewModel model)
        {
            var entity = await _repository.GetDailyLogByIdAsync(id);
            if (entity == null) return false;

            if (entity.IsUpdateLocked)
            {
                throw new InvalidOperationException(
                    "Đã có bản ghi mới hơn. Hồ sơ này đã bị khóa chỉnh sửa.");
            }

            if (entity.UpdateCount >= 2)
            {
                throw new InvalidOperationException(
                    "Đã dùng hết 2 lượt chỉnh sửa.");
            }

            entity.SystolicBp = model.SystolicBp!.Value;
            entity.DiastolicBp = model.DiastolicBp!.Value;
            entity.HeartRate = model.HeartRate!.Value;
            entity.ChestPainLevel = model.ChestPainLevel;
            entity.HasExerciseAngina = model.HasExerciseAngina;
            entity.UpdateCount++;

            await _repository.UpdateDailyLogAsync(entity);
            return true;
        }

        public async Task<IEnumerable<DailyVitalLog>> GetLogsByDateAsync(int userId, DateTime date)
        {
            var patient = await _patientRepository.GetByUserIdAsync(userId);

            if (patient == null)
                throw new Exception("Không tìm thấy hồ sơ bệnh nhân");

            var result = await _repository.GetAllDailyLogByPatientIdAsync(
                patient.Id, date, date, 1, 100);

            return result.Items;
        }

        public async Task<PersonalHealthTrackerViewModel> GetPatientHealthTrendsAsync(int userId, int days = 7)
        {
            var patient = await _patientRepository.GetByUserIdAsync(userId);
            if (patient == null)
                throw new Exception("Không tìm thấy hồ sơ bệnh nhân");

            DateTime startDate;
            if (days == 1)
            {
                startDate = DateTime.Today; 
            }
            else
            {
                startDate = DateTime.Today.AddDays(-days + 1); 
            }

            var pagedLogs = await _repository.GetAllDailyLogByPatientIdAsync(patient.Id, startDate, DateTime.Now, 1, 1000);
            
            var logs = pagedLogs.Items.OrderBy(x => x.LoggedAt).ToList();

            var vm = new PersonalHealthTrackerViewModel { Days = days };

            foreach (var log in logs)
            {
                // Nếu là 1 ngày, hiển thị Giờ:Phút. Nếu là 7/30 ngày, hiển thị Ngày/Tháng Giờ:Phút
                string labelFormat = days == 1 ? "HH:mm" : "dd/MM HH:mm";
                vm.Labels.Add(log.LoggedAt.ToString(labelFormat));
                vm.SystolicBpValues.Add(log.SystolicBp);
                vm.DiastolicBpValues.Add(log.DiastolicBp);
                vm.HeartRateValues.Add(log.HeartRate);
            }

            return vm;
        }
            
        private string EvaluateAlertLevel(DailyVitalLogViewModel model, PatientThreshold? threshold)
        {
            // Nếu bác sĩ chưa cấu hình, dùng một instance mới (nó sẽ tự lấy giá trị mặc định đã set trong Model)
            if (threshold == null)
            {
                threshold = new PatientThreshold();
            }

            // 1. CẤP ĐỘ ĐỎ (Danger)
            if (model.SystolicBp >= threshold.SystolicBpDanger ||
                model.DiastolicBp >= threshold.DiastolicBpDanger ||
                model.HeartRate <= threshold.HeartRateDangerMin ||
                model.HeartRate >= threshold.HeartRateDangerMax ||
                model.ChestPainLevel >= 2 ||
                model.HasExerciseAngina)
            {
                return "Danger";
            }

            // 2. CẤP ĐỘ VÀNG (Warning)
            if (model.SystolicBp >= threshold.SystolicBpWarning ||
                model.DiastolicBp >= threshold.DiastolicBpWarning ||
                model.HeartRate <= threshold.HeartRateWarningMin ||
                model.HeartRate >= threshold.HeartRateWarningMax ||
                model.ChestPainLevel == 1)
            {
                return "Warning";
            }

            // 3. CẤP ĐỘ XANH (Normal)
            return "Normal";
        }
    }
}