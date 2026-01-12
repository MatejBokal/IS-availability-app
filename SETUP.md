# AvailabilityCollector – Local Setup Guide

This guide explains how to set up and run the AvailabilityCollector project locally.
Follow all steps **exactly** to ensure the app works the same way on every machine.

---

# 🚀 Quick Setup (Recommended)

Run the automated setup script:

```powershell
.\setup.ps1
```

This will automatically:
- Check prerequisites (Docker, .NET SDK)
- Start SQL Server in Docker
- Restore dependencies
- Apply all EF migrations

---

# ✅ 1. Requirements

Make sure you have:

- .NET SDK 10
- Docker Desktop
- Azure Data Studio (recommended) or another SQL tool
- Git

---

# ⭐ 2. Clone the Repository

```bash
git clone https://github.com/MatejBokal/IS-availability-app
cd AvailabilityCollector
```

---

# ⭐ 3. Start SQL Server in Docker

The app uses Microsoft SQL Server running in a Docker container.

### 🟦 Windows / Linux (x64):
```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=VerySecret!" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

### 🟧 macOS (M1/M2/M3 – ARM):
```bash
docker run --cap-add SYS_PTRACE -e "ACCEPT_EULA=1" -e "MSSQL_SA_PASSWORD=VerySecret!" -p 1433:1433 --name azuresqledge -d mcr.microsoft.com/azure-sql-edge
```

**IMPORTANT:**  
Use the SAME password as in the instructions:
```
VerySecret!
```

---


# ⭐ 4. Restore Dependencies

```bash
dotnet restore
```

---

# ⭐ 5. Apply Entity Framework Migrations

The project uses TWO DbContexts:

- **AppContextDb** → domain tables (Worker, Matrica, Status, etc.)
- **AppIdentityContext** → Identity tables (AspNetUsers, Roles, etc.)

Run BOTH:

### 1) Create Identity tables:
```bash
dotnet ef database update -c AppIdentityContext
```

### 2) Create domain tables:
```bash
dotnet ef database update -c AppContextDb
```

---

# ⭐ 6. Run the App

```bash
dotnet run
```
---


# ⭐ 7. Administrator Account (IMPORTANT)

An administrator account has **already been created**.

You can log in using:

**Email:** example@example.si  
**Password:** Example123.

This user already has the **Administrator** role assigned.

### ✔ You can use this account to:
- Access **Workers** (the only protected section so far)
- Test login & authorization
- Access all CRUD pages

You can still register your own account if you want.

---