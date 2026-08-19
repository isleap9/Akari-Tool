using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using AkariTool.Core.Features.Common.Models;
using VirtualKey = Windows.System.VirtualKey;

namespace AkariTool.Views.Controls;

/// <summary>
/// Custom ComboBox for Power Plan selection with status indicators, [Active]
/// badges, and per-plan delete. Ported from Winhance's PowerPlanComboBox,
/// adapted to Akari's <see cref="PowerPlanComboBoxOption"/> model.
/// </summary>
public sealed partial class PowerPlanComboBox : UserControl
{
    private static readonly SolidColorBrush ExistsBrush = new(Color.FromArgb(255, 0, 200, 60));
    private static readonly SolidColorBrush NotExistsBrush = new(Color.FromArgb(255, 200, 40, 0));

    /// <summary>
    /// The dropdown items. Typed as <see cref="object"/> (like
    /// <c>ComboBox.ItemsSource</c>) on purpose: XAML metadata generation expands
    /// DependencyProperty value types, and expanding the init-only
    /// <see cref="PowerPlanComboBoxOption"/> record into per-property setters
    /// fails to compile (CS8852). Runtime value is always an
    /// <see cref="ObservableCollection{T}"/> of <see cref="PowerPlanComboBoxOption"/>.
    /// </summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(object),
            typeof(PowerPlanComboBox),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(
            nameof(SelectedIndex),
            typeof(int),
            typeof(PowerPlanComboBox),
            new PropertyMetadata(-1));

    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register(
            nameof(DeleteCommand),
            typeof(ICommand),
            typeof(PowerPlanComboBox),
            new PropertyMetadata(null));

    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public PowerPlanComboBox()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Commits the dropdown selection on close (Winhance timing) so arrow-key
    /// navigation through the open dropdown never activates plans mid-scroll.
    /// The commit propagates up the TwoWay SelectedIndex binding to the row's
    /// apply path.
    /// </summary>
    private void OnDropDownClosed(object sender, object e)
        => SelectedIndex = PowerPlanSelector.SelectedIndex;

    /// <summary>
    /// Handles the dropdown opened event to set up visual states for all items.
    /// </summary>
    private void OnDropDownOpened(object sender, object e)
    {
        // Defer so the container visual tree is realized.
        DispatcherQueue.TryEnqueue(UpdateAllItemVisualStates);
    }

    private void UpdateAllItemVisualStates()
    {
        if (ItemsSource is not ObservableCollection<PowerPlanComboBoxOption> options) return;

        for (int i = 0; i < options.Count; i++)
        {
            var container = PowerPlanSelector.ContainerFromIndex(i) as ComboBoxItem;
            if (container == null) continue;

            var option = options[i];
            var grid = FindChild<Grid>(container, "ItemRoot");
            if (grid == null) continue;

            SetupItemVisualState(grid, option);

            // Keyboard accessibility: Delete key on the focused item deletes the
            // plan (standard Windows list-deletion convention — the trash button
            // itself cannot be tab-reached inside an open ComboBox).
            container.Tag = option;
            container.KeyDown -= OnItemKeyDown;
            container.KeyDown += OnItemKeyDown;
        }
    }

    private void OnItemKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Delete
            && sender is ComboBoxItem container
            && container.Tag is PowerPlanComboBoxOption option
            && option.ExistsOnSystem
            && !option.IsActive)
        {
            RunDelete(option);
            e.Handled = true;
        }
    }

    private void SetupItemVisualState(Grid grid, PowerPlanComboBoxOption option)
    {
        var statusIndicator = FindChild<Ellipse>(grid, "StatusIndicator");
        var activeBadge = FindChild<TextBlock>(grid, "ActiveBadge");
        var deleteButton = FindChild<Button>(grid, "DeleteButton");

        if (statusIndicator != null)
        {
            statusIndicator.Fill = option.ExistsOnSystem ? ExistsBrush : NotExistsBrush;
            ToolTipService.SetToolTip(statusIndicator,
                option.ExistsOnSystem ? "Installed on system" : "Predefined plan (click to install)");
        }

        if (activeBadge != null)
            activeBadge.Visibility = option.IsActive ? Visibility.Visible : Visibility.Collapsed;

        if (deleteButton != null)
        {
            deleteButton.Visibility = (option.ExistsOnSystem && !option.IsActive)
                ? Visibility.Visible
                : Visibility.Collapsed;

            deleteButton.Tag = option;
            deleteButton.Click -= OnDeleteButtonClick;
            deleteButton.Click += OnDeleteButtonClick;
        }
    }

    private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is PowerPlanComboBoxOption option)
            RunDelete(option);
    }

    private void RunDelete(PowerPlanComboBoxOption option)
    {
        if (DeleteCommand is { } cmd && cmd.CanExecute(option))
            cmd.Execute(option);
    }

    private static T? FindChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild && typedChild.Name == childName)
                return typedChild;

            var result = FindChild<T>(child, childName);
            if (result != null)
                return result;
        }
        return null;
    }
}