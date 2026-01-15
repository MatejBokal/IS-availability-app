# Azure Deployment Guide

This guide explains how to migrate your database to Azure SQL Database and deploy the AvailabilityCollector application to Azure.

## Prerequisites

1. **Azure Account** with an active subscription
2. **Azure SQL Database** instance created (or create one using the steps below)
3. **.NET SDK 10** installed locally
4. **Azure CLI** (optional, but recommended)

## Step 1: Get Your Azure SQL Connection String

### Option A: Using Azure Portal

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to your SQL Database resource
3. Click on **Connection strings** in the left menu
4. Copy the **ADO.NET** connection string
5. Replace `{your_username}` and `{your_password}` with your actual credentials

The connection string should look like:
```
Server=tcp:yourserver.database.windows.net,1433;Initial Catalog=yourdatabase;Persist Security Info=False;User ID=youruser;Password=yourpassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

### Option B: If You Haven't Created the Database Yet

1. Go to Azure Portal → Create a resource → SQL Database
2. Fill in:
   - **Database name**: `AvailabilityCollectorDB` (or your preferred name)
   - **Server**: Create new or select existing
   - **Compute + storage**: Choose your pricing tier
   - **Authentication**: SQL authentication
3. Configure firewall rules:
   - **Allow Azure services and resources to access this server**: Yes
   - Add your current IP address (important for running migrations)
4. After creation, go to Connection strings and copy the ADO.NET connection string

## Step 2: Test the Connection

Before running migrations, test that you can connect to your Azure SQL Database:

```powershell
# Option 1: Using the test script
.\test-azure-sql.ps1 -ConnectionString "your-connection-string-here"

# Option 2: Using environment variable (recommended for security)
$env:AZURE_SQL_CONNECTION_STRING = "your-connection-string-here"
.\test-azure-sql.ps1
```

The test script will:
- Verify connectivity to Azure SQL
- Show the SQL Server version
- Display the current database name
- Check if migrations have already been applied

**Important**: If the connection fails, check:
- Your IP address is whitelisted in Azure SQL firewall rules
- The connection string is correct (especially username and password)
- The server name is correct (format: `yourserver.database.windows.net`)

## Step 3: Run Migrations

Once the connection test succeeds, run the migrations:

```powershell
# Option 1: Using the migration script
.\migrate-azure-sql.ps1 -ConnectionString "your-connection-string-here"

# Option 2: Using environment variable
$env:AZURE_SQL_CONNECTION_STRING = "your-connection-string-here"
.\migrate-azure-sql.ps1
```

The migration script will:
- Check/install EF Core tools if needed
- Temporarily update `appsettings.json` with your Azure connection string
- Run `dotnet ef database update` to apply all migrations
- Restore the original `appsettings.json` after completion

### Manual Migration (Alternative)

If you prefer to run migrations manually:

1. Update `AvailabilityCollector/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=tcp:yourserver.database.windows.net,1433;Initial Catalog=yourdatabase;User ID=youruser;Password=yourpassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
     }
   }
   ```

2. Run the migration:
   ```powershell
   cd AvailabilityCollector
   dotnet ef database update
   ```

3. **Important**: Restore your local connection string if you want to continue using Docker locally.

## Step 4: Verify Migrations

After running migrations, verify that all tables were created:

```powershell
# The test script will show migration count
.\test-azure-sql.ps1 -ConnectionString "your-connection-string-here"
```

You can also use Azure Portal:
1. Go to your SQL Database
2. Click on **Query editor** (preview)
3. Run: `SELECT * FROM INFORMATION_SCHEMA.TABLES ORDER BY TABLE_NAME`
4. You should see tables like:
   - `__EFMigrationsHistory`
   - `AspNetUsers`
   - `AspNetRoles`
   - `AvailabilityMonths`
   - `Positions`
   - And other application tables

## Step 5: Configure Azure App Service (When Deploying)

When deploying to Azure App Service, configure the connection string:

1. Go to your App Service in Azure Portal
2. Navigate to **Configuration** → **Connection strings**
3. Add a new connection string:
   - **Name**: `DefaultConnection`
   - **Value**: Your Azure SQL connection string
   - **Type**: SQLAzure
4. Click **Save**

**Important**: 
- Never commit your production connection string to source control
- Use Azure Key Vault or App Service configuration for production
- The connection string in `appsettings.json` should only be for local development

## Connection String Format Reference

### Azure SQL Database Format
```
Server=tcp:{server}.database.windows.net,1433;Initial Catalog={database};Persist Security Info=False;User ID={username};Password={password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

### Local Docker Format (for reference)
```
Server=localhost;Database=AvailabilityCollectorDB;User Id=sa;Password=VerySecret!;TrustServerCertificate=true
```

Key differences:
- Azure uses `Server=tcp:...` format
- Azure requires `Encrypt=True` and `TrustServerCertificate=False`
- Azure uses `Initial Catalog` instead of `Database`
- Local uses `TrustServerCertificate=true` (for Docker)

## Security Best Practices

1. **Firewall Rules**: Only allow necessary IP addresses
2. **Strong Passwords**: Use complex passwords for SQL authentication
3. **Managed Identity** (Advanced): Consider using Azure AD authentication instead of SQL auth
4. **Connection Strings**: Store in Azure Key Vault or App Service Configuration, never in code
5. **SSL/TLS**: Always use `Encrypt=True` for Azure SQL

## Troubleshooting

### "Cannot open server" error
- Check firewall rules in Azure Portal
- Ensure "Allow Azure services" is enabled
- Add your current IP address

### "Login failed" error
- Verify username and password are correct
- Check if the SQL user exists
- Ensure the user has proper permissions

### Migration fails with timeout
- Increase `Connection Timeout` in connection string (default 30 seconds)
- Check if database server is not paused (for basic tiers)
- Verify network connectivity

### "TrustServerCertificate" warnings
- For Azure SQL, use `TrustServerCertificate=False` and `Encrypt=True`
- For local Docker, use `TrustServerCertificate=true`

## Next Steps

After successfully migrating the database:
1. Test your application locally with the Azure connection string
2. Deploy your application to Azure App Service
3. Configure the connection string in App Service settings
4. Test the deployed application
5. Consider setting up automated backups for your database
