````markdown
# 🏥 Smart Health Monitoring System (SHMS)

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-MVC-5C2D91?style=for-the-badge)
![SQL Server](https://img.shields.io/badge/SQL%20Server-Database-CC2927?style=for-the-badge&logo=microsoftsqlserver)
![Docker](https://img.shields.io/badge/Docker-Containerized-2496ED?style=for-the-badge&logo=docker)
![License](https://img.shields.io/badge/License-Educational-green?style=for-the-badge)

Smart Health Monitoring System (SHMS) is a comprehensive healthcare platform built with **ASP.NET Core MVC** and **.NET 8**. The system digitalizes healthcare workflows while providing an integrated environment for **Patients**, **Doctors**, **Receptionists**, and **Administrators**.

It combines modern web technologies, artificial intelligence, and real-time communication to improve healthcare services, patient monitoring, and hospital management.

🌐 **Live Demo:** *Coming Soon*

---

# ✨ Why This Project Stands Out

- 👥 **Multi-role platform** with dedicated interfaces for Patients, Doctors, Receptionists, and Administrators.
- 🤖 **AI-powered health monitoring** using an ANFIS model running with **ONNX Runtime**.
- 🩺 **End-to-end appointment workflow** from booking to clinical examination and post-treatment follow-up.
- 💬 **Real-time communication** powered by SignalR WebSocket.
- ⚙️ **Background automation** for appointment slot generation, reminders, and expired booking cleanup.
- 🔗 **Production-ready integrations** including Google OAuth2, Gmail SMTP, Twilio Verify, Google Gemini AI, MinIO, and OCR.
- 🐳 **Dockerized deployment** with Azure SQL Database support.

---

# 🚀 Core Features

| Feature | Description |
|---------|-------------|
| 🩺 Appointment Booking | Search doctors by specialty, gender, date, and available time slots. |
| 📋 Electronic Medical Records | Clinical examinations, prescriptions, laboratory results, and patient history. |
| 📊 Vital Sign Monitoring | Blood pressure, heart rate, SpO₂, blood glucose, and trend visualization. |
| 🤖 AI Risk Prediction | Cardiovascular risk prediction using ANFIS (ONNX Runtime). |
| 💬 Telemedicine Chat | Real-time doctor-patient messaging with SignalR. |
| 🤖 AI Chatbot | Healthcare assistant powered by Google Gemini API. |
| 🔐 Authentication | Cookie Authentication, Google OAuth2, and SMS OTP (Twilio Verify). |
| 📧 Email Notification | Dynamic email templates and automatic appointment reminders. |
| 📰 Health News | News management and external news crawling. |
| 🏥 Role-based Dashboard | Separate dashboards for Admin, Doctor, Receptionist, and Patient. |
| 📱 QR Check-in | QR code check-in for appointments. |
| 🗂️ Audit Log | Real-time system activity logging. |
| 📁 OCR & File Storage | Medical document scanning with Tesseract OCR and MinIO storage. |
| 🆘 Emergency Contacts | Emergency contact management for patients. |
| ⚙️ System Administration | User management, threshold configuration, email templates, and system settings. |

---

# 🛠 Tech Stack

| Layer | Technology |
|-------|------------|
| Backend | C#, .NET 8, ASP.NET Core MVC |
| Frontend | Razor (.cshtml), HTML, CSS, JavaScript |
| ORM | Entity Framework Core 8 |
| Database | Microsoft SQL Server / Azure SQL Database |
| Real-time | ASP.NET Core SignalR |
| AI / ML | Google Gemini API, ONNX Runtime (ANFIS Model) |
| Authentication | Cookie Authentication, Google OAuth2, Twilio Verify |
| Storage | MinIO Object Storage |
| Email | MailKit (Gmail SMTP) |
| OCR | Tesseract OCR |
| QR Code | QRCoder |
| Dependency Injection | Scrutor |
| DevOps | Docker, Docker Compose, VPS (Linux) |

---

# ⚙️ Run Locally

## Prerequisites

- .NET 8 SDK
- SQL Server (Local or Azure SQL)
- MinIO (Optional)

## 1. Clone Repository

```bash
git clone https://github.com/SHMS3/Smart-Health-Monitoring-System.git
cd SmartHealthMonitoring
````

---

## 2. Configure Environment Variables

Create a `.env` file in the project root.

```env
# Database
DB_CONNECTION=Server=localhost;Database=HeartCareDB_AI_Focus;Trusted_Connection=True;TrustServerCertificate=True

# Google OAuth2
Authentication__Google__ClientId=your_google_client_id
Authentication__Google__ClientSecret=your_google_client_secret

# Email
EmailSettings__SenderEmail=your_email@gmail.com
EmailSettings__Password=your_app_password

# Gemini AI
GeminiApiKey=your_gemini_api_key

# Twilio OTP
TwilioSettings__AccountSid=your_twilio_account_sid
TwilioSettings__AuthToken=your_twilio_auth_token
TwilioSettings__VerifyServiceSid=your_twilio_verify_service_sid

# MinIO
MINIO_ENDPOINT=localhost:9000
MINIO_ACCESS_KEY=your_minio_access_key
MINIO_SECRET_KEY=your_minio_secret_key
MINIO_SECURE=false
```

---

## 3. Apply Database Migration

```bash
dotnet ef database update
```

---

## 4. Run Application

```bash
dotnet run
```

Open:

```
https://localhost:7xxx
```

---

# 🐳 Run with Docker

```bash
docker compose up --build
```

Application URL

```
http://localhost:5001
```

The application connects to SQL Server using the **DB_CONNECTION** environment variable, allowing deployment with Azure SQL Database or an external SQL Server without requiring a SQL Server container.

---

# 🚀 Deployment

The application is deployed using Docker on a Linux VPS.

| Component | Technology                      |
| --------- | ------------------------------- |
| Runtime   | Docker (.NET 8 ASP.NET Runtime) |
| Database  | Azure SQL Database              |
| Storage   | MinIO Object Storage            |
| Timezone  | Asia/Ho_Chi_Minh                |

---

# 👥 User Roles

| Role             | Permissions                                                                |
| ---------------- | -------------------------------------------------------------------------- |
| 👤 Patient       | Book appointments, monitor health, chat with doctors, view medical records |
| 👨‍⚕️ Doctor     | Manage appointments, clinical examinations, telemedicine chat, schedules   |
| 🏥 Receptionist  | Patient check-in, waiting queue management, appointment support            |
| ⚙️ Administrator | Full system management, dashboards, statistics, configurations             |

---

# 👨‍💻 Team

Developed as part of the **SWP391** course.

| Name                    | GitHub |
| ------------------------| ------ |
| Khương Đức Anh          | GitHub |
| Phạm Thế Sơn            | GitHub |
| Nguyễn Đức Dũng         | GitHub |
| Nguyễn Quang Vinh       | GitHub |
| Nguyễn Thành Phương Nam | GitHub |

---

# 📞 Contact

🌐 **Demo:** https://shms.plo-learning.com/
📧 **Email:** [thesonpham28@gmail.com](mailto:thesonpham28@gmail.com)
🐙 **GitHub:** https://github.com/SHMS3/Smart-Health-Monitoring-System

