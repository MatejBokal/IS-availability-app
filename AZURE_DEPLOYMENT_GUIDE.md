# Azure App Service Deployment Guide

This guide will help you deploy your AvailabilityCollector application to Azure App Service.

## Prerequisites

1. ✅ **Azure SQL Database** - Already set up and configured
2. **Azure Account** with an active subscription
3. **Azure CLI** (optional but recommended) - [Install here](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
4. **.NET SDK 10** - Already installed

---

## Method 1: Deploy via Azure Portal (Recommended for First Time)

### Step 1: Create Azure App Service

1. Go to [Azure Portal](https://portal.azure.com)
2. Click **Create a resource** → Search for **Web App**
3. Click **Create** on "Web App"
4. Fill in the **Basics** tab:
   - **Subscription:** Your subscription
   - **Resource Group:** Create new or use existing (e.g., `availability-app-rg`)
   - **Name:** `availabilityapp-api` (must be globally unique)
   - **Publish:** Code
   - **Runtime stack:** .NET 10 (or .NET 8 if 10 not available)
   - **Operating System:** Linux (recommended) or Windows
   - **Region:** Same region as your SQL Database (recommended)
   - **App Service Plan:** 
     - Create new plan
     - **Plan name:** `availabilityapp-plan`
     - **Sku and size:** Basic B1 (or Free F1 for testing)
   - Click **Review + create** → **Create**

### Step 2: Configure Connection String

1. After deployment, go to your App Service
2. Navigate to **Configuration** → **Connection strings**
3. Click **+ New connection string**
4. Add:
   - **Name:** `DefaultConnection`
   - **Value:** `Server=tcp:availabilityapp-sql.database.windows.net,1433;Initial Catalog=availabilityapp-db;Persist Security Info=False;User ID=matej;Password=Admin123.;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`
   - **Type:** SQLAzure
5. Click **OK** → **Save**

### Step 3: Configure Application Settings (Optional but Recommended)

1. Still in **Configuration** → **Application settings**
2. Add/Update these settings:
   - `ASPNETCORE_ENVIRONMENT` = `Production`
   - `Jwt__Issuer` = `AvailabilityCollector` (or your domain)
   - `Jwt__Audience` = `AvailabilityCollector` (or your domain)
   - `Jwt__Key` = (Generate a secure random key - at least 32 characters)
3. Click **Save**

**Generate JWT Key:**
```powershell
# PowerShell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

### Step 4: Configure CORS (If Needed)

1. In **Configuration** → **CORS**
2. Add allowed origins:
   - `https://your-android-app-domain.com` (if you have one)
   - `*` (for testing - remove in production!)
3. Click **Save**

### Step 5: Deploy Your Code

#### Option A: Deploy from Visual Studio (Easiest)

1. Open your solution in Visual Studio
2. Right-click on `AvailabilityCollector` project
3. Select **Publish**
4. Choose **Azure** → **Azure App Service (Linux)** or **Azure App Service (Windows)**
5. Select your subscription and the App Service you created
6. Click **Finish** → **Publish**

#### Option B: Deploy using ZIP Deploy

1. **Publish locally:**
   ```powershell
   cd AvailabilityCollector
   dotnet publish -c Release -o ./publish
   ```

2. **Create ZIP file:**
   ```powershell
   Compress-Archive -Path ./publish/* -DestinationPath ./deploy.zip
   ```

3. **Deploy using Azure CLI:**
   ```powershell
   az webapp deployment source config-zip `
     --resource-group availability-app-rg `
     --name availabilityapp-api `
     --src ./deploy.zip
   ```

#### Option C: Deploy using GitHub Actions (Advanced)

See "Method 3: CI/CD with GitHub Actions" below.

### Step 6: Verify Deployment

1. Go to your App Service → **Overview**
2. Click on the **URL** (e.g., `https://availabilityapp-api.azurewebsites.net`)
3. You should see your application running
4. Test the API: `https://availabilityapp-api.azurewebsites.net/api/auth/login`

---

## Method 2: Deploy using Azure CLI

### Step 1: Login to Azure

```powershell
az login
```

### Step 2: Create Resource Group (if not exists)

```powershell
az group create --name availability-app-rg --location "West Europe"
```

### Step 3: Create App Service Plan

```powershell
az appservice plan create `
  --name availabilityapp-plan `
  --resource-group availability-app-rg `
  --sku B1 `
  --is-linux
```

### Step 4: Create Web App

```powershell
az webapp create `
  --resource-group availability-app-rg `
  --plan availabilityapp-plan `
  --name availabilityapp-api `
  --runtime "DOTNET|10.0"
```

### Step 5: Configure Connection String

```powershell
az webapp config connection-string set `
  --resource-group availability-app-rg `
  --name availabilityapp-api `
  --connection-string-type SQLAzure `
  --settings DefaultConnection="Server=tcp:availabilityapp-sql.database.windows.net,1433;Initial Catalog=availabilityapp-db;Persist Security Info=False;User ID=matej;Password=Admin123.;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

### Step 6: Set Application Settings

```powershell
az webapp config appsettings set `
  --resource-group availability-app-rg `
  --name availabilityapp-api `
  --settings ASPNETCORE_ENVIRONMENT="Production"
```

### Step 7: Deploy Application

```powershell
cd AvailabilityCollector
dotnet publish -c Release
cd bin/Release/net10.0/publish
Compress-Archive -Path * -DestinationPath deploy.zip

az webapp deployment source config-zip `
  --resource-group availability-app-rg `
  --name availabilityapp-api `
  --src deploy.zip
```

---

## Method 3: CI/CD with GitHub Actions (Recommended for Production)

### Step 1: Create GitHub Actions Workflow

Create `.github/workflows/azure-deploy.yml`:

```yaml
name: Deploy to Azure App Service

on:
  push:
    branches:
      - main
  workflow_dispatch:

env:
  AZURE_WEBAPP_NAME: availabilityapp-api
  DOTNET_VERSION: '10.0.x'

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore dependencies
        run: dotnet restore AvailabilityCollector/AvailabilityCollector.csproj

      - name: Build
        run: dotnet build AvailabilityCollector/AvailabilityCollector.csproj --configuration Release --no-restore

      - name: Publish
        run: dotnet publish AvailabilityCollector/AvailabilityCollector.csproj --configuration Release --no-build --output ./publish

      - name: Deploy to Azure Web App
        uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ env.AZURE_WEBAPP_NAME }}
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
          package: ./publish
```

### Step 2: Get Publish Profile

1. Go to Azure Portal → Your App Service
2. Click **Get publish profile**
3. Download the `.PublishSettings` file
4. Copy its contents

### Step 3: Add GitHub Secret

1. Go to your GitHub repository
2. **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Name: `AZURE_WEBAPP_PUBLISH_PROFILE`
5. Value: Paste the entire contents of the `.PublishSettings` file
6. Click **Add secret**

### Step 4: Push to GitHub

Push your code to the `main` branch, and GitHub Actions will automatically deploy!

---

## Post-Deployment Configuration

### 1. Update Android App API URL

Update the Android app to point to your Azure deployment:

**File:** `Android/app/src/main/java/Android/availibityCollector/data/api/RetrofitClient.kt`

```kotlin
private const val BASE_URL = "https://availabilityapp-api.azurewebsites.net/"
```

**File:** `Android/app/src/main/java/Android/availibityCollector/data/api/VolleyClient.kt`

```kotlin
private const val BASE_URL = "https://availabilityapp-api.azurewebsites.net/api/"
```

### 2. Enable HTTPS Only

1. Go to App Service → **Configuration** → **General settings**
2. Enable **HTTPS Only**
3. Click **Save**

### 3. Set up Custom Domain (Optional)

1. Go to App Service → **Custom domains**
2. Follow the instructions to add your domain
3. Configure SSL certificate

### 4. Enable Application Insights (Recommended)

1. Go to App Service → **Application Insights**
2. Click **Turn on Application Insights**
3. Create new resource or use existing
4. Click **Apply**

---

## Troubleshooting

### Application Won't Start

1. Check **Log stream** in App Service
2. Check **App Service logs** → **Logging**
3. Enable **Application logging** and **Detailed error messages**

### Database Connection Errors

1. Verify connection string in **Configuration** → **Connection strings**
2. Check Azure SQL firewall rules allow App Service IP
3. Enable "Allow Azure services" in SQL Server firewall

### CORS Errors (Android App)

1. Verify CORS is configured in App Service
2. Check Android app is using correct API URL
3. Ensure HTTPS is enabled if using HTTPS in Android app

### View Application Logs

**Option 1: Log Stream (Real-time)**
1. Go to App Service → **Log stream**
2. View real-time logs

**Option 2: Download Logs**
```powershell
az webapp log download `
  --resource-group availability-app-rg `
  --name availabilityapp-api `
  --log-file app-logs.zip
```

**Option 3: Kudu Console**
1. Go to `https://availabilityapp-api.scm.azurewebsites.net`
2. Navigate to **Debug console** → **CMD**
3. View logs in `LogFiles/Application`

---

## Security Best Practices

1. ✅ **Use App Service Configuration** for connection strings (not appsettings.json)
2. ✅ **Generate secure JWT key** (32+ characters, random)
3. ✅ **Enable HTTPS Only**
4. ✅ **Configure CORS properly** (don't use `*` in production)
5. ✅ **Use Application Insights** for monitoring
6. ✅ **Enable authentication** if needed (Azure AD, etc.)
7. ✅ **Set up backup** for App Service
8. ✅ **Configure auto-scaling** based on demand

---

## Cost Optimization

- **Development/Testing:** Use Free (F1) or Basic B1 tier
- **Production:** Start with Basic B1, scale up as needed
- **SQL Database:** Use Basic tier for development, scale up for production
- **Monitor costs** in Azure Cost Management

---

## Next Steps

After deployment:
1. ✅ Update Android app API URLs
2. ✅ Test all API endpoints
3. ✅ Verify database connectivity
4. ✅ Set up monitoring (Application Insights)
5. ✅ Configure backups
6. ✅ Set up staging slot for testing deployments

---

## Quick Reference

**App Service URL Format:**
```
https://[app-name].azurewebsites.net
```

**API Endpoints:**
- Auth: `https://[app-name].azurewebsites.net/api/auth/login`
- Workers: `https://[app-name].azurewebsites.net/api/workers`
- Availability: `https://[app-name].azurewebsites.net/api/availability`

**Useful Azure CLI Commands:**
```powershell
# List all web apps
az webapp list

# View app service logs
az webapp log tail --name availabilityapp-api --resource-group availability-app-rg

# Restart app service
az webapp restart --name availabilityapp-api --resource-group availability-app-rg

# View app service status
az webapp show --name availabilityapp-api --resource-group availability-app-rg
```
