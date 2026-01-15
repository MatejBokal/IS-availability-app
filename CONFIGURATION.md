# Configuration Guide

This document describes the current configuration for both the .NET API and Android app.

## .NET API Configuration

### Database Connection

The application is currently configured to use **Azure SQL Database**.

**Connection String Location:** `AvailabilityCollector/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:availabilityapp-sql.database.windows.net,1433;Initial Catalog=availabilityapp-db;Persist Security Info=False;User ID=matej;Password=Admin123.;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

### API Endpoints

The API runs on:
- **HTTP:** `http://localhost:5180`
- **HTTPS:** `https://localhost:7248`

Configuration: `AvailabilityCollector/Properties/launchSettings.json`

### Switching Back to Local Database

If you need to use the local Docker database instead:

1. Update `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AvailabilityCollectorDB;User Id=sa;Password=VerySecret!;TrustServerCertificate=true"
  }
}
```

2. Ensure Docker SQL Server is running:
```powershell
docker start availability-sqlserver
```

## Android App Configuration

### API Base URL

The Android app connects to the .NET API (not directly to the database). The API endpoint is configured in:

**Files:**
- `Android/app/src/main/java/Android/availibityCollector/data/api/VolleyClient.kt`
- `Android/app/src/main/java/Android/availibityCollector/data/api/RetrofitClient.kt`

**Current Configuration:**
- **Base URL:** `http://10.0.2.2:5180/`
- **Port:** `5180` (matches .NET API)

### URL Explanation

- `10.0.2.2` - Special IP address that maps to `localhost` on your development machine when using Android Emulator
- For **physical devices**, replace with your computer's local IP address (e.g., `http://192.168.1.100:5180/`)

### Finding Your Computer's IP Address

**Windows:**
```powershell
ipconfig
# Look for "IPv4 Address" under your active network adapter
```

**macOS/Linux:**
```bash
ifconfig
# or
ip addr show
```

### Changing the API Endpoint

#### For Android Emulator (Current Setup)
```kotlin
private const val BASE_URL = "http://10.0.2.2:5180/"
```

#### For Physical Device
```kotlin
private const val BASE_URL = "http://YOUR_COMPUTER_IP:5180/"
```

#### For Production/Deployed API
```kotlin
private const val BASE_URL = "https://your-api-domain.azurewebsites.net/"
```

**Important:** Update both `RetrofitClient.kt` and `VolleyClient.kt` if you're using both clients.

## Network Requirements

### Local Development

1. **.NET API** must be running locally
2. **Android Emulator** uses `10.0.2.2` to access `localhost`
3. **Physical Device** needs:
   - Android device and computer on the same Wi-Fi network
   - Windows Firewall configured to allow connections on port 5180
   - Use computer's local IP address instead of `localhost`

### Azure Deployment (Future)

When deploying to Azure:
1. Update Android app's `BASE_URL` to point to Azure App Service URL
2. Configure connection string in Azure App Service (not in `appsettings.json`)
3. Ensure CORS is properly configured for your domain

## Security Notes

⚠️ **Important:** 
- Connection strings with passwords are currently in `appsettings.json`
- For production, use Azure App Service Configuration or Azure Key Vault
- Never commit production connection strings to source control
- Consider using environment variables for sensitive configuration

## Testing the Configuration

### Test .NET API Database Connection

```powershell
# Test Azure SQL connection
.\test-azure-sql.ps1 -ConnectionString "your-connection-string"
```

### Test Android App Connection

1. Start the .NET API:
   ```powershell
   cd AvailabilityCollector
   dotnet run
   ```

2. Run the Android app
3. Check logs in Android Studio's Logcat for connection errors

### Common Issues

**"Cannot connect to API" (Android Emulator)**
- Ensure .NET API is running on port 5180
- Verify you're using `10.0.2.2` (not `localhost`)

**"Cannot connect to API" (Physical Device)**
- Verify computer and device are on same Wi-Fi network
- Check Windows Firewall allows port 5180
- Use computer's local IP (not `localhost` or `127.0.0.1`)

**"Database connection failed" (.NET API)**
- Verify Azure SQL firewall rules allow your IP
- Check connection string is correct
- Ensure database server is not paused (for basic tiers)
