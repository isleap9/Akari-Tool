// Dummy page required so WPF-UI NavigationViewItem.OnClick() fires its
// navigation pipeline (ItemInvoked / SelectionChanged). We intercept
// in ItemInvoked before any real navigation occurs.
namespace AkariTool
{
    internal class NavDummyPage : System.Windows.Controls.Page { }
}
