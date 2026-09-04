using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AniDesk.App.Views.Controls;

public partial class ShortcutRecorderControl : UserControl
{
    public static readonly DependencyProperty HotkeyDisplayProperty =
        DependencyProperty.Register(
            nameof(HotkeyDisplay),
            typeof(string),
            typeof(ShortcutRecorderControl),
            new FrameworkPropertyMetadata("Win + Shift + H", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyDisplayPropertyChanged));

    public static readonly DependencyProperty ModifiersProperty =
        DependencyProperty.Register(
            nameof(Modifiers),
            typeof(uint),
            typeof(ShortcutRecorderControl),
            new FrameworkPropertyMetadata(0u, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty VirtualKeyProperty =
        DependencyProperty.Register(
            nameof(VirtualKey),
            typeof(uint),
            typeof(ShortcutRecorderControl),
            new FrameworkPropertyMetadata(0u, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty HotkeyRecordedCommandProperty =
        DependencyProperty.Register(
            nameof(HotkeyRecordedCommand),
            typeof(ICommand),
            typeof(ShortcutRecorderControl));

    public string HotkeyDisplay
    {
        get => (string)GetValue(HotkeyDisplayProperty);
        set => SetValue(HotkeyDisplayProperty, value);
    }

    public uint Modifiers
    {
        get => (uint)GetValue(ModifiersProperty);
        set => SetValue(ModifiersProperty, value);
    }

    public uint VirtualKey
    {
        get => (uint)GetValue(VirtualKeyProperty);
        set => SetValue(VirtualKeyProperty, value);
    }

    public ICommand? HotkeyRecordedCommand
    {
        get => (ICommand?)GetValue(HotkeyRecordedCommandProperty);
        set => SetValue(HotkeyRecordedCommandProperty, value);
    }

    public event EventHandler<(uint Modifiers, uint VirtualKey, string Display)>? HotkeyRecorded;

    private readonly ObservableCollection<KeyBadgeViewModel> _keyBadges = new();

    public ShortcutRecorderControl()
    {
        InitializeComponent();

        KeyBadgesItemsControl.ItemsSource = _keyBadges;
        ParseAndRefreshBadges(HotkeyDisplay);
    }

    private static void OnHotkeyDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShortcutRecorderControl ctrl && e.NewValue is string newDisplay)
        {
            ctrl.ParseAndRefreshBadges(newDisplay);
        }
    }

    public void ParseAndRefreshBadges(string? display)
    {
        _keyBadges.Clear();
        if (string.IsNullOrWhiteSpace(display))
        {
            display = "Win + Shift + H";
        }

        var parts = display.Split('+').Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p));
        foreach (var part in parts)
        {
            if (string.Equals(part, "Win", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(part, "Windows", StringComparison.OrdinalIgnoreCase))
            {
                _keyBadges.Add(new KeyBadgeViewModel { Text = "⊞", IsWinKey = true });
            }
            else if (string.Equals(part, "Shift", StringComparison.OrdinalIgnoreCase))
            {
                _keyBadges.Add(new KeyBadgeViewModel { Text = "⇧", IsShiftKey = true });
            }
            else
            {
                _keyBadges.Add(new KeyBadgeViewModel { Text = part });
            }
        }
    }

    private void OnContainerClicked(object sender, MouseButtonEventArgs e)
    {
        OpenShortcutDialog();
    }

    private void OnEditClicked(object sender, RoutedEventArgs e)
    {
        OpenShortcutDialog();
    }

    public void OpenShortcutDialog()
    {
        var parentWindow = Window.GetWindow(this);
        var dialog = new ShortcutDialog(HotkeyDisplay, Modifiers, VirtualKey)
        {
            Owner = parentWindow
        };

        if (dialog.ShowDialog() == true)
        {
            HotkeyDisplay = dialog.RecordedDisplay;
            Modifiers = dialog.RecordedModifiers;
            VirtualKey = dialog.RecordedVirtualKey;

            ParseAndRefreshBadges(HotkeyDisplay);

            HotkeyRecorded?.Invoke(this, (Modifiers, VirtualKey, HotkeyDisplay));
            HotkeyRecordedCommand?.Execute((Modifiers, VirtualKey, HotkeyDisplay));
        }
    }
}
