using System;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Context
{
    public static class SeedData
    {
        public static void Initialize(ApplicationDbContext context)
        {
            context.Database.Migrate();

            if (context.Users.Any())
                return;

            // =========================
            // ROLES
            // =========================
            var adminRole = new Role
            {
                RoleName = "Admin"
            };

            var doctorRole = new Role
            {
                RoleName = "Doctor"
            };

            var operatorRole = new Role
            {
                RoleName = "Operator"
            };

            var patientRole = new Role
            {
                RoleName = "Patient"
            };

            context.Roles.AddRange(adminRole, doctorRole, operatorRole, patientRole);
            context.SaveChanges();

            // =========================
            // USERS (8 accounts)
            // Password chung: 123456
            // =========================
            string defaultPassword = BCrypt.Net.BCrypt.HashPassword("123456");

            var users = new List<User>
            {
                // ADMIN
                new User
                {
                    Username = "admin",
                    Email = "admin@smarthealth.com",
                    PasswordHash = defaultPassword,
                    RoleId = adminRole.RoleId,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },

                // OPERATOR
                new User
                {
                    Username = "operator1",
                    Email = "operator1@smarthealth.com",
                    PasswordHash = defaultPassword,
                    RoleId = operatorRole.RoleId,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },

                // DOCTORS
                new User
                {
                    Username = "doctor1",
                    Email = "doctor1@smarthealth.com",
                    PasswordHash = defaultPassword,
                    RoleId = doctorRole.RoleId,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },

                new User
                {
                    Username = "doctor2",
                    Email = "doctor2@smarthealth.com",
                    PasswordHash = defaultPassword,
                    RoleId = doctorRole.RoleId,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },

                new User
                {
                    Username = "doctor3",
                    Email = "doctor3@smarthealth.com",
                    PasswordHash = defaultPassword,
                    RoleId = doctorRole.RoleId,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },

                // PATIENT USERS
                new User
                {
                    Username = "patient1",
                    Email = "patient1@smarthealth.com",
                    PasswordHash = defaultPassword,
                    RoleId = patientRole.RoleId,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },

                new User
                {
                    Username = "patient2",
                    Email = "patient2@smarthealth.com",
                    PasswordHash = defaultPassword,
                    RoleId = patientRole.RoleId,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },

                new User
                {
                    Username = "patient3",
                    Email = "patient3@smarthealth.com",
                    PasswordHash = defaultPassword,
                    RoleId = patientRole.RoleId,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                }
            };

            context.Users.AddRange(users);
            context.SaveChanges();

            // =========================
            // DOCTORS
            // Chỉ chuyên khoa tim mạch
            // =========================
            var doctors = new List<Doctor>
            {
                new Doctor
                {
                    UserId = users[2].UserId,
                    FullName = "Dr. Nguyen Van Minh",
                    Specialty = "Cardiology",
                    LicenseNumber = "CARD-001",
                    CreatedAt = DateTime.Now
                },

                new Doctor
                {
                    UserId = users[3].UserId,
                    FullName = "Dr. Tran Thi Lan",
                    Specialty = "Cardiology",
                    LicenseNumber = "CARD-002",
                    CreatedAt = DateTime.Now
                },

                new Doctor
                {
                    UserId = users[4].UserId,
                    FullName = "Dr. Le Hoang Phuc",
                    Specialty = "Cardiology",
                    LicenseNumber = "CARD-003",
                    CreatedAt = DateTime.Now
                }
            };

            context.Doctors.AddRange(doctors);
            context.SaveChanges();

            // =========================
            // PATIENTS
            // =========================
            var patients = new List<Patient>
            {
                new Patient
                {
                    UserId = users[5].UserId,
                    FullName = "Pham Tuan Kiet",
                    DateOfBirth = new DateOnly(1998, 5, 12),
                    Gender = "Male",
                    BloodType = "A+",
                    PhoneNumber = "0901000001",
                    Address = "Ha Noi",
                    EmergencyContact = "0909999991",
                    IsDeleted = false,
                    CreatedAt = DateTime.Now
                },

                new Patient
                {
                    UserId = users[6].UserId,
                    FullName = "Nguyen Thi Hoa",
                    DateOfBirth = new DateOnly(1985, 8, 20),
                    Gender = "Female",
                    BloodType = "B+",
                    PhoneNumber = "0901000002",
                    Address = "Hai Phong",
                    EmergencyContact = "0909999992",
                    IsDeleted = false,
                    CreatedAt = DateTime.Now
                },

                new Patient
                {
                    UserId = users[7].UserId,
                    FullName = "Tran Quoc Bao",
                    DateOfBirth = new DateOnly(1975, 2, 10),
                    Gender = "Male",
                    BloodType = "O+",
                    PhoneNumber = "0901000003",
                    Address = "Da Nang",
                    EmergencyContact = "0909999993",
                    IsDeleted = false,
                    CreatedAt = DateTime.Now
                }
            };

            context.Patients.AddRange(patients);
            context.SaveChanges();

            // =========================
            // METRIC TYPES
            // =========================
            var metricTypes = new List<MetricType>
            {
                new MetricType
                {
                    Code = "HEART_RATE",
                    Name = "Heart Rate",
                    Unit = "bpm"
                },

                new MetricType
                {
                    Code = "BLOOD_PRESSURE_SYS",
                    Name = "Systolic Blood Pressure",
                    Unit = "mmHg"
                },

                new MetricType
                {
                    Code = "BLOOD_PRESSURE_DIA",
                    Name = "Diastolic Blood Pressure",
                    Unit = "mmHg"
                },

                new MetricType
                {
                    Code = "SPO2",
                    Name = "Blood Oxygen",
                    Unit = "%"
                }
            };

            context.MetricTypes.AddRange(metricTypes);
            context.SaveChanges();

            // =========================
            // HEALTH METRICS
            // =========================
            var metrics = new List<HealthMetric>
            {
                new HealthMetric
                {
                    PatientId = patients[0].PatientId,
                    MetricTypeId = metricTypes[0].MetricTypeId,
                    Value = 78,
                    Notes = "Normal",
                    MeasuredAt = DateTime.Now.AddHours(-5),
                    Source = "Smart Watch"
                },

                new HealthMetric
                {
                    PatientId = patients[1].PatientId,
                    MetricTypeId = metricTypes[1].MetricTypeId,
                    Value = 145,
                    Notes = "High blood pressure",
                    MeasuredAt = DateTime.Now.AddHours(-3),
                    Source = "Clinic Device"
                },

                new HealthMetric
                {
                    PatientId = patients[2].PatientId,
                    MetricTypeId = metricTypes[0].MetricTypeId,
                    Value = 110,
                    Notes = "Elevated heart rate",
                    MeasuredAt = DateTime.Now.AddHours(-1),
                    Source = "Wearable Device"
                }
            };

            context.HealthMetrics.AddRange(metrics);
            context.SaveChanges();
        }
    }
}