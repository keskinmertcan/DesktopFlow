using System.Diagnostics;
using DesktopFlow.App.Localization;
using DesktopFlow.Core.Models;

namespace DesktopFlow.App.ViewModels;

internal static class TextMaps
{
    public static string State(ILocalizer text, MonitorRunState state)
    {
        return state switch
        {
            MonitorRunState.Off => text["Status_Off"],
            MonitorRunState.Paused => text["Status_Paused"],
            MonitorRunState.Watching => text["Status_Watching"],
            MonitorRunState.Error => text["Status_Error"],
            _ => throw new UnreachableException(state.ToString()),
        };
    }

    public static string Skip(ILocalizer text, SkipReason reason)
    {
        return reason switch
        {
            SkipReason.Directory => text["Skip_Directory"],
            SkipReason.Shortcut => text["Skip_Shortcut"],
            SkipReason.HiddenOrSystem => text["Skip_HiddenOrSystem"],
            SkipReason.DesktopIni => text["Skip_DesktopIni"],
            SkipReason.Excluded => text["Skip_Excluded"],
            SkipReason.ReparsePoint => text["Skip_ReparsePoint"],
            SkipReason.RuleSkip => text["Skip_RuleSkip"],
            SkipReason.PartialDownload => text["Skip_PartialDownload"],
            SkipReason.CloudPlaceholder => text["Skip_CloudPlaceholder"],
            SkipReason.AlreadyInDestination => text["Skip_AlreadyInDestination"],
            SkipReason.DestinationEscapesRoot => text["Skip_DestinationEscapesRoot"],
            _ => throw new UnreachableException(reason.ToString()),
        };
    }

    public static string Kind(ILocalizer text, ActivityKind kind)
    {
        return kind switch
        {
            ActivityKind.Move => text["Kind_Move"],
            ActivityKind.Undo => text["Kind_Undo"],
            ActivityKind.Skip => text["Kind_Skip"],
            ActivityKind.Recovery => text["Kind_Recovery"],
            _ => throw new UnreachableException(kind.ToString()),
        };
    }

    public static string Status(ILocalizer text, ActivityStatus status)
    {
        return status switch
        {
            ActivityStatus.Pending => text["Result_Pending"],
            ActivityStatus.Succeeded => text["Result_Succeeded"],
            ActivityStatus.Failed => text["Result_Failed"],
            ActivityStatus.Skipped => text["Result_Skipped"],
            _ => throw new UnreachableException(status.ToString()),
        };
    }

    public static string Scope(ILocalizer text, MonitorScope scope)
    {
        return scope switch
        {
            MonitorScope.Desktop => text["Scope_Desktop"],
            MonitorScope.Downloads => text["Scope_Downloads"],
            _ => throw new UnreachableException(scope.ToString()),
        };
    }
}
