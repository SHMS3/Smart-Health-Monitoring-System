# Unit tests for `namntp`

This folder contains the isolated xUnit test project for backend code currently
owned by `namntp27@gmail.com` according to Git blame on branch `namntp`.

## Covered areas

- Audit logging: service, MVC action filter, admin controller, SignalR hub, and
  audit calls integrated into account, patient, doctor, chat, clinical exam,
  threshold, and warning-alert flows.
- Email: template rendering, SMTP abstraction, notification history,
  notification controller, trigger orchestration, and admin template/settings
  controllers.
- Patient features: emergency contacts, patient UI settings, their models and
  view models.
- Background work: daily vital-log reminders and high-risk AI prediction email
  triggering.
- Supporting result, settings, validation, and view-model behavior.

The tests use EF Core InMemory, Moq, and unique temporary directories. They do
not connect to SQL Server, SMTP, SignalR clients, or the production
`App_Data`/email-template directories.

Generated migrations, the generated DbContext snapshot, Razor/CSS/JavaScript,
and startup wiring in `Program.cs` are not unit-test targets.

## Run

From the repository root:

```powershell
dotnet test SmartHealthMonitoring\SmartHealthMonitoring.sln --no-restore
```

Collect coverage:

```powershell
dotnet test SmartHealthMonitoring\SmartHealthMonitoring.sln --no-restore `
  --collect "XPlat Code Coverage" `
  --results-directory ".tmp\namntp-unit-coverage"
```
