# Environment Configuration Guide

## 🔄 **How Environment Detection Works**

The app automatically detects the environment using this priority:

1. **Environment Variable** (highest priority)
2. **Hardware Detection** (ARM = Production, x86/x64 = Development)  
3. **OS Detection** (Linux = Production, Windows = Development)

## 🖥️ **Manual Environment Control**

### Development Mode
```powershell
# Windows
dotnet run
# OR explicitly
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

### Production Mode  
```powershell
# Windows
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run
```

```bash
# Linux/Raspberry Pi
export ASPNETCORE_ENVIRONMENT=Production
dotnet run
```

## 🥧 **Raspberry Pi Deployment**

The app will **automatically** use Production mode on Raspberry Pi because:
- ARM processor detected → Production
- Linux OS detected → Production

## 🔑 **Configuration Keys by Environment**

| Environment | Config File | Ingest Key |
|-------------|-------------|------------|
| Development | `appsettings.json` | `dev-testing-key-123` |
| Production | `appsettings.Production.json` | `K8n2mP9vQ4xR7sT1uY5wE3iO6pA0lZ9c` |

## 🚀 **Deployment Checklist**

1. ✅ Copy app to Raspberry Pi
2. ✅ App auto-detects Production environment  
3. ✅ Uses Production ingest key automatically
4. ✅ Points to your desktop IP relay server
5. ✅ Ready to transmit to your web app!