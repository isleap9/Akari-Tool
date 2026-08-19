namespace AkariTool.ViewModels.Tweaks;

public interface ISettingRowViewModel
{
    bool IsVisible { get; set; }
    bool MatchesSearch(string query);
}
