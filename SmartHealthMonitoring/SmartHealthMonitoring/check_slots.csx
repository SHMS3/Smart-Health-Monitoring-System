using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;

var optionsBuilder = new DbContextOptionsBuilder<SmartHealthMonitoringContext>();
optionsBuilder.UseSqlServer("Server=localhost;Database=HeartCareDB_AI_Focus;Trusted_Connection=True;TrustServerCertificate=True");

using var context = new SmartHealthMonitoringContext(optionsBuilder.Options);
var slots = context.AppointmentSlots.OrderByDescending(s => s.SlotStart).Take(10).ToList();
foreach(var s in slots) {
    Console.WriteLine($"Id={s.Id}, DoctorId={s.DoctorId}, SlotStart={s.SlotStart}, Status={s.Status}");
}
