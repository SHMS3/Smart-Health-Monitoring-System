namespace SmartHealthMonitoring.Interfaces;

public interface ISosNotificationService
{
    Task NotifyEmergencyContactsAsync(int alertId, CancellationToken cancellationToken = default);
}
