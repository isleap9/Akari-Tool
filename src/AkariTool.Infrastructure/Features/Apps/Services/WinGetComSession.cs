using System;
using Microsoft.Management.Deployment;
using WindowsPackageManager.Interop;

namespace AkariTool.Infrastructure.Features.Apps.Services;

/// <summary>
/// Owns the WinGet COM state (factory, package manager, lock, flags).
/// Shared singleton — injected into services that need COM access.
/// Winhance WinGetComSession 1:1: StandardFactory + ALLOW_LOWER_TRUST_REGISTRATION
/// is the only approach that works for unpackaged apps running as admin with
/// self-contained AppSdk (the ElevatedFactory path hangs there — winget-cli#4377).
/// </summary>
public sealed class WinGetComSession(AkariTool.Infrastructure.Features.Common.Interfaces.IAkariLogService logService)
{
    private readonly object _factoryLock = new();
    private volatile bool _isInitialized;
    private volatile bool _comInitTimedOut;

    public PackageManager? PackageManager { get; private set; }
    public WindowsPackageManagerFactory? Factory { get; private set; }

    public bool ComInitTimedOut
    {
        get => _comInitTimedOut;
        set => _comInitTimedOut = value;
    }

    public bool EnsureComInitialized()
    {
        if (_isInitialized && PackageManager != null)
            return true;

        if (_comInitTimedOut)
            return false;

        lock (_factoryLock)
        {
            if (_isInitialized && PackageManager != null)
                return true;

            if (_comInitTimedOut)
                return false;

            try
            {
                logService.Log(AkariTool.Core.Features.Common.Enums.LogLevel.Info,
                    "Initializing WinGet COM API via StandardFactory");
                var factory = new WindowsPackageManagerStandardFactory(
                    ClsidContext.Prod,
                    allowLowerTrustRegistration: true);
                PackageManager = factory.CreatePackageManager();
                Factory = factory;
                _isInitialized = true;
                logService.Log(AkariTool.Core.Features.Common.Enums.LogLevel.Info,
                    "WinGet COM API initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                logService.Log(AkariTool.Core.Features.Common.Enums.LogLevel.Error,
                    $"Failed to initialize WinGet COM API: {ex.Message}");
                _isInitialized = false;
                PackageManager = null;
                Factory = null;
                return false;
            }
        }
    }

    public void ResetFactory()
    {
        lock (_factoryLock)
        {
            _isInitialized = false;
            _comInitTimedOut = false;
            PackageManager = null;
            Factory = null;
        }
    }
}
