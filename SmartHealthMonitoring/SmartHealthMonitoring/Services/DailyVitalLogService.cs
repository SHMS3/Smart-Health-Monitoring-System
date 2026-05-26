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

            var pagedEntity = await _repository.GetAllDailyLogByPatientIdAsync(patient.Id, fromDate, toDate, pageIndex, pageSize);

            var viewModels = pagedEntity.Items.Select(entity => new DailyVitalLogViewModel
            {
                Id = entity.Id,
                LoggedAt = entity.LoggedAt,
                SystolicBp = entity.SystolicBp,
                DiastolicBp = entity.DiastolicBp,
                HeartRate = entity.HeartRate,
                ChestPainLevel = entity.ChestPainLevel,
                HasExerciseAngina = entity.HasExerciseAngina
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

            var entity = new DailyVitalLog
            {
                PatientId = patient.Id,
                LoggedAt = DateTime.Now,

                SystolicBp = model.SystolicBp,
                DiastolicBp = model.DiastolicBp,
                HeartRate = model.HeartRate,
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

            return new DailyVitalLogViewModel
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
            };
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

            entity.SystolicBp = model.SystolicBp;
            entity.DiastolicBp = model.DiastolicBp;
            entity.HeartRate = model.HeartRate;
            entity.ChestPainLevel = model.ChestPainLevel;
            entity.HasExerciseAngina = model.HasExerciseAngina;
            entity.UpdateCount++;

            await _repository.UpdateDailyLogAsync(entity);
            return true;
        }

        public async Task<IEnumerable<DailyVitalLog>> GetLogsByDateAsync(int patientId, DateTime date)
        {
            // Lấy log của đúng ngày đó
            var result = await _repository.GetAllDailyLogByPatientIdAsync(
                patientId, date, date, 1, 100); // Lấy nhiều hơn 4 để chắc chắn kiểm tra đủ
            return result.Items;
        }
    }
}