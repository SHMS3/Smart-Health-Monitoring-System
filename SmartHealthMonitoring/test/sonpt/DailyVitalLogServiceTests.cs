using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Repositories;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.UnitTests;

public class DailyVitalLogServiceTests
{
    [Fact]
    public async Task GetPatientVitalsHistoryAsync_WhenPatientDoesNotExist_Throws()
    {
        await using var context = TestContextFactory.Create();
        var service = DailyVitalLogTestSetup.CreateService(context);

        await Assert.ThrowsAsync<Exception>(() => service.GetPatientVitalsHistoryAsync(
            userId: 999,
            fromDate: DateTime.Today,
            toDate: DateTime.Today));
    }

    [Fact]
    public async Task GetPatientVitalsHistoryAsync_WhenDateRangeIsInvalid_ThrowsArgumentException()
    {
        await using var context = TestContextFactory.Create();
        await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        var service = DailyVitalLogTestSetup.CreateService(context);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetPatientVitalsHistoryAsync(
            userId: 10,
            fromDate: DateTime.Today,
            toDate: DateTime.Today.AddDays(-1)));

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetPatientVitalsHistoryAsync(
            userId: 10,
            fromDate: DateTime.Today.AddDays(1),
            toDate: DateTime.Today.AddDays(1)));
    }

    [Fact]
    public async Task GetPatientVitalsHistoryAsync_MapsLogsAndCalculatesAlertLevel()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        context.DailyVitalLogs.AddRange(
            DailyVitalLogTestSetup.Log(1, patient.Id, DateTime.Today.AddHours(8), systolicBp: 120),
            DailyVitalLogTestSetup.Log(2, patient.Id, DateTime.Today.AddHours(9), systolicBp: 130),
            DailyVitalLogTestSetup.Log(3, patient.Id, DateTime.Today.AddHours(10), systolicBp: 145),
            DailyVitalLogTestSetup.Log(4, patient.Id, DateTime.Today.AddHours(11), systolicBp: 180, isDeleted: true));
        await context.SaveChangesAsync();
        var service = DailyVitalLogTestSetup.CreateService(context);

        var result = await service.GetPatientVitalsHistoryAsync(
            patient.UserId,
            DateTime.Today,
            DateTime.Today,
            pageIndex: 1,
            pageSize: 10);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal([3, 2, 1], result.Items.Select(item => item.Id));
        Assert.Equal(["Danger", "Warning", "Normal"], result.Items.Select(item => item.AlertLevel));
    }

    [Fact]
    public async Task CreateLogAsync_LocksPreviousLogsAndPersistsNewLog()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        context.PatientThresholds.Add(DailyVitalLogTestSetup.Threshold(patient.Id));
        context.DailyVitalLogs.Add(DailyVitalLogTestSetup.Log(1, patient.Id, DateTime.Today.AddHours(-2)));
        await context.SaveChangesAsync();
        var service = DailyVitalLogTestSetup.CreateService(context);
        var model = DailyVitalLogTestSetup.ValidModel(systolicBp: 145);

        await service.CreateLogAsync(patient.UserId, model);

        var logs = await context.DailyVitalLogs.OrderBy(log => log.Id).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.True(logs[0].IsUpdateLocked);
        Assert.Equal(patient.Id, logs[1].PatientId);
        Assert.Equal((short)145, logs[1].SystolicBp);
        Assert.False(logs[1].IsUpdateLocked);
        Assert.Equal((byte)0, logs[1].UpdateCount);
        Assert.Equal("Danger", model.AlertLevel);
    }

    [Fact]
    public async Task CreateLogAsync_WhenPatientDoesNotExist_Throws()
    {
        await using var context = TestContextFactory.Create();
        var service = DailyVitalLogTestSetup.CreateService(context);

        await Assert.ThrowsAsync<Exception>(() => service.CreateLogAsync(999, DailyVitalLogTestSetup.ValidModel()));
    }

    [Fact]
    public async Task GetDailyLogDetailsAsync_WhenLogDoesNotExist_ReturnsNull()
    {
        await using var context = TestContextFactory.Create();
        var service = DailyVitalLogTestSetup.CreateService(context);

        var result = await service.GetDailyLogDetailsAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDailyLogDetailsAsync_MapsThresholdAndUpdatePermissions()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        context.PatientThresholds.Add(DailyVitalLogTestSetup.Threshold(patient.Id, systolicWarning: 125, systolicDanger: 135));
        context.DailyVitalLogs.Add(DailyVitalLogTestSetup.Log(
            id: 1,
            patientId: patient.Id,
            loggedAt: DateTime.Today,
            systolicBp: 130,
            updateCount: 1));
        await context.SaveChangesAsync();
        var service = DailyVitalLogTestSetup.CreateService(context);

        var result = await service.GetDailyLogDetailsAsync(1);

        Assert.NotNull(result);
        Assert.Equal(125, result.SystolicBpWarning);
        Assert.Equal(135, result.SystolicBpDanger);
        Assert.Equal("Warning", result.AlertLevel);
        Assert.Equal((byte)1, result.UpdateCount);
        Assert.True(result.CanUpdate);
        Assert.Equal(1, result.RemainingUpdate);
    }

    [Fact]
    public async Task GetLogForUpdateAsync_ReturnsEditableValuesOrNull()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        var loggedAt = DateTime.Today.AddHours(9);
        context.DailyVitalLogs.Add(DailyVitalLogTestSetup.Log(1, patient.Id, loggedAt, systolicBp: 125));
        await context.SaveChangesAsync();
        var service = DailyVitalLogTestSetup.CreateService(context);

        var existing = await service.GetLogForUpdateAsync(1);
        var missing = await service.GetLogForUpdateAsync(999);

        Assert.NotNull(existing);
        Assert.Equal(1, existing.Id);
        Assert.Equal(loggedAt, existing.LoggedAt);
        Assert.Equal((short)125, existing.SystolicBp);
        Assert.Null(missing);
    }

    [Fact]
    public async Task UpdateLogAsync_WhenLogDoesNotExist_ReturnsFalse()
    {
        await using var context = TestContextFactory.Create();
        var service = DailyVitalLogTestSetup.CreateService(context);

        var updated = await service.UpdateLogAsync(999, DailyVitalLogTestSetup.ValidModel());

        Assert.False(updated);
    }

    [Fact]
    public async Task UpdateLogAsync_WhenLogIsLockedOrLimitReached_Throws()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        context.DailyVitalLogs.AddRange(
            DailyVitalLogTestSetup.Log(1, patient.Id, DateTime.Today, isUpdateLocked: true),
            DailyVitalLogTestSetup.Log(2, patient.Id, DateTime.Today.AddHours(1), updateCount: 2));
        await context.SaveChangesAsync();
        var service = DailyVitalLogTestSetup.CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateLogAsync(1, DailyVitalLogTestSetup.ValidModel()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateLogAsync(2, DailyVitalLogTestSetup.ValidModel()));
    }

    [Fact]
    public async Task UpdateLogAsync_UpdatesMeasurementsAndIncrementsCount()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        context.DailyVitalLogs.Add(DailyVitalLogTestSetup.Log(1, patient.Id, DateTime.Today, updateCount: 1));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = DailyVitalLogTestSetup.CreateService(context);
        var model = DailyVitalLogTestSetup.ValidModel(systolicBp: 135, diastolicBp: 85, heartRate: 95);
        model.ChestPainLevel = 1;
        model.HasExerciseAngina = true;

        var updated = await service.UpdateLogAsync(1, model);

        Assert.True(updated);
        var saved = await context.DailyVitalLogs.SingleAsync();
        Assert.Equal((short)135, saved.SystolicBp);
        Assert.Equal((short)85, saved.DiastolicBp);
        Assert.Equal((short)95, saved.HeartRate);
        Assert.Equal((byte)1, saved.ChestPainLevel);
        Assert.True(saved.HasExerciseAngina);
        Assert.Equal((byte)2, saved.UpdateCount);
    }

    [Fact]
    public async Task GetLogsByDateAsync_ReturnsOnlyNonDeletedLogsForRequestedDate()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        context.DailyVitalLogs.AddRange(
            DailyVitalLogTestSetup.Log(1, patient.Id, DateTime.Today.AddHours(8)),
            DailyVitalLogTestSetup.Log(2, patient.Id, DateTime.Today.AddDays(-1)),
            DailyVitalLogTestSetup.Log(3, patient.Id, DateTime.Today.AddHours(9), isDeleted: true));
        await context.SaveChangesAsync();
        var service = DailyVitalLogTestSetup.CreateService(context);

        var result = await service.GetLogsByDateAsync(patient.UserId, DateTime.Today);

        Assert.Equal([1], result.Select(log => log.Id));
        await Assert.ThrowsAsync<Exception>(() => service.GetLogsByDateAsync(999, DateTime.Today));
    }

    [Fact]
    public async Task GetPatientHealthTrendsAsync_ReturnsChronologicalValuesAndLabels()
    {
        await using var context = TestContextFactory.Create();
        var patient = await DailyVitalLogTestSetup.AddPatientAsync(context, patientId: 1, userId: 10);
        context.DailyVitalLogs.AddRange(
            DailyVitalLogTestSetup.Log(1, patient.Id, DateTime.Today.AddHours(12), systolicBp: 130, diastolicBp: 85, heartRate: 90),
            DailyVitalLogTestSetup.Log(2, patient.Id, DateTime.Today.AddHours(8), systolicBp: 120, diastolicBp: 75, heartRate: 80));
        await context.SaveChangesAsync();
        var service = DailyVitalLogTestSetup.CreateService(context);

        var result = await service.GetPatientHealthTrendsAsync(patient.UserId, days: 1);

        Assert.Equal(1, result.Days);
        Assert.Equal(["08:00", "12:00"], result.Labels);
        Assert.Equal([120, 130], result.SystolicBpValues);
        Assert.Equal([75, 85], result.DiastolicBpValues);
        Assert.Equal([80, 90], result.HeartRateValues);
        await Assert.ThrowsAsync<Exception>(() => service.GetPatientHealthTrendsAsync(999));
    }
}

internal static class DailyVitalLogTestSetup
{
    public static DailyVitalLogService CreateService(SmartHealthMonitoringContext context)
    {
        return new DailyVitalLogService(
            new DailyVitalLogRepository(context),
            new PatientRepository(context));
    }

    public static async Task<Patient> AddPatientAsync(
        SmartHealthMonitoringContext context,
        int patientId,
        int userId)
    {
        var user = EntityFactory.User(userId, role: 0);
        var patient = EntityFactory.Patient(patientId, user);
        context.Users.Add(user);
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        return patient;
    }

    public static DailyVitalLogViewModel ValidModel(
        short systolicBp = 120,
        short diastolicBp = 75,
        short heartRate = 80)
    {
        return new DailyVitalLogViewModel
        {
            SystolicBp = systolicBp,
            DiastolicBp = diastolicBp,
            HeartRate = heartRate,
            ChestPainLevel = 0,
            HasExerciseAngina = false
        };
    }

    public static DailyVitalLog Log(
        int id,
        int patientId,
        DateTime loggedAt,
        short systolicBp = 120,
        short diastolicBp = 75,
        short heartRate = 80,
        byte chestPainLevel = 0,
        bool hasExerciseAngina = false,
        bool isDeleted = false,
        byte updateCount = 0,
        bool isUpdateLocked = false)
    {
        return new DailyVitalLog
        {
            Id = id,
            PatientId = patientId,
            LoggedAt = loggedAt,
            SystolicBp = systolicBp,
            DiastolicBp = diastolicBp,
            HeartRate = heartRate,
            ChestPainLevel = chestPainLevel,
            HasExerciseAngina = hasExerciseAngina,
            IsDeleted = isDeleted,
            UpdateCount = updateCount,
            IsUpdateLocked = isUpdateLocked
        };
    }

    public static PatientThreshold Threshold(
        int patientId,
        short systolicWarning = 130,
        short systolicDanger = 140)
    {
        return new PatientThreshold
        {
            PatientId = patientId,
            SystolicBpWarning = systolicWarning,
            SystolicBpDanger = systolicDanger,
            DiastolicBpWarning = 80,
            DiastolicBpDanger = 90,
            HeartRateWarningMin = 60,
            HeartRateDangerMin = 50,
            HeartRateWarningMax = 100,
            HeartRateDangerMax = 120
        };
    }
}
