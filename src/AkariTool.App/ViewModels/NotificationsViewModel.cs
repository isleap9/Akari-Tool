using System.Collections.Generic;
using WinUI.Framework.Mvvm;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;
using AkariTool.Tabs.Notifications;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Notifications page — ported to the declarative SettingDefinition model
/// (Track A Phase 4). Builds its sections from <see cref="NotificationsOptimizations.Build"/>.
///
/// ⚠ The Security Notifications group contains Windows Defender *notification*
/// toggles (`notifications-windows-security`). These only touch notification
/// registry keys — they do NOT disable, arm, or otherwise alter Defender
/// protection.
/// </summary>
public sealed partial class NotificationsViewModel : SettingPageViewModel
{
    public NotificationsViewModel(
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs,
        ISettingDependencyResolver? dependencyResolver = null)
        : base(stateReader, executor, dialogs, dependencyResolver: dependencyResolver)
    {
        Title = "Notifications";
        Subtitle = "Notification behavior and system alerts";
    }

    public override string NavTag => "Notifications";
    public override string NavLabel => "Notifications";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => NotificationsOptimizations.Build();
}
