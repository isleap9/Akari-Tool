using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Optimize.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AkariTool.Infrastructure.Tests.Features.Optimize;

public class WindowsUpdatePolicyHandlerTests
{
    private const string UxHklm = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

    private static WindowsUpdatePolicyHandler MakeHandler(
        IWindowsRegistryService registryService,
        IFileSystemService fileSystemService)
    {
        var logService = Substitute.For<IAkariLogService>();
        var processExecutor = Substitute.For<IProcessExecutor>();
        var powerShellRunner = Substitute.For<IPowerShellRunner>();

        return new WindowsUpdatePolicyHandler(
            logService,
            registryService,
            processExecutor,
            powerShellRunner,
            fileSystemService);
    }

    private static async Task<int> DiscoverIndexAsync(WindowsUpdatePolicyHandler handler)
    {
        var disc = await handler.DiscoverSpecialSettingsAsync(new[]
        {
            new SettingDefinition { Id = "updates-policy-mode", Name = "Policy", Description = "Policy mode" }
        });

        return (int)disc["updates-policy-mode"]["CurrentPolicyIndex"]!;
    }

    [Fact]
    public async Task GetCurrentUpdatePolicyIndex_CriticalDllsRenamed_Returns3()
    {
        var registryService = Substitute.For<IWindowsRegistryService>();
        var fileSystemService = Substitute.For<IFileSystemService>();

        fileSystemService.FileExists(@"C:\Windows\System32\WaaSMedicSvc.dll").Returns(false);
        fileSystemService.FileExists(@"C:\Windows\System32\WaaSMedicSvc_BAK.dll").Returns(true);

        var handler = MakeHandler(registryService, fileSystemService);

        (await DiscoverIndexAsync(handler)).Should().Be(3);
    }

    [Fact]
    public async Task GetCurrentUpdatePolicyIndex_UpdatesPaused_Returns2()
    {
        var registryService = Substitute.For<IWindowsRegistryService>();
        var fileSystemService = Substitute.For<IFileSystemService>();

        fileSystemService.FileExists(Arg.Any<string>()).Returns(false);
        registryService.GetValue(UxHklm, "PauseUpdatesStartTime").Returns("2025-01-01T00:00:00Z");

        var handler = MakeHandler(registryService, fileSystemService);

        (await DiscoverIndexAsync(handler)).Should().Be(2);
    }

    [Fact]
    public async Task GetCurrentUpdatePolicyIndex_SecurityOnlyDefer_Returns1()
    {
        var registryService = Substitute.For<IWindowsRegistryService>();
        var fileSystemService = Substitute.For<IFileSystemService>();

        fileSystemService.FileExists(Arg.Any<string>()).Returns(false);
        registryService.GetValue(Arg.Any<string>(), Arg.Any<string>()).Returns((object?)null);
        registryService.GetValue(UxHklm, "DeferFeatureUpdates").Returns(1);

        var handler = MakeHandler(registryService, fileSystemService);

        (await DiscoverIndexAsync(handler)).Should().Be(1);
    }

    [Fact]
    public async Task GetCurrentUpdatePolicyIndex_NormalMode_Returns0()
    {
        var registryService = Substitute.For<IWindowsRegistryService>();
        var fileSystemService = Substitute.For<IFileSystemService>();

        fileSystemService.FileExists(Arg.Any<string>()).Returns(false);
        registryService.GetValue(Arg.Any<string>(), Arg.Any<string>()).Returns((object?)null);

        var handler = MakeHandler(registryService, fileSystemService);

        (await DiscoverIndexAsync(handler)).Should().Be(0);
    }
}
