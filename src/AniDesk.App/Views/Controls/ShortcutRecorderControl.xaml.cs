using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AniDesk.Core.Interop;

namespace AniDesk.App.Views.Controls;

public class KeyBadgeViewModel
{
    public string Text { get; set; } = string.Empty;
    public bool IsWinKey { get; set; }
    public bool IsShiftKey { get; set; }
    public bool IsSpecialSymbol => IsWinKey || IsShiftKey;
}

public partial class ShortcutRecorderControl : UserControl
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;
    private const int VK_MENU = 0x12; // Alt

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
    private readonly ObservableCollection<KeyBadgeViewModel> _liveBadges = new();
    private bool _isRecording;

    public ShortcutRecorderControl()
    {
        InitializeComponent();

        KeyBadgesItemsControl.ItemsSource = _keyBadges;
        LiveBadgesItemsControl.ItemsSource = _liveBadges;

        ParseAndRefreshBadges(HotkeyDisplay);

        PreviewKeyDown += OnControlPreviewKeyDown;
        PreviewKeyUp += OnControlPreviewKeyUp;
        LostFocus += (s, e) =>
        {
            if (_isRecording)
            {
                CancelRecording();
            }
        };
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
        StartRecording();
    }

    private void OnEditClicked(object sender, RoutedEventArgs e)
    {
        StartRecording();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        CancelRecording();
    }

    public void StartRecording()
    {
        _isRecording = true;
        DisplayContainer.Visibility = Visibility.Collapsed;
        RecordingContainer.Visibility = Visibility.Visible;
        _liveBadges.Clear();
        PromptText.Text = "Press shortcut keys... (Esc to cancel)";

        Focus();
        Keyboard.Focus(this);
    }

    public void CancelRecording()
    {
        _isRecording = false;
        _liveBadges.Clear();
        RecordingContainer.Visibility = Visibility.Collapsed;
        DisplayContainer.Visibility = Visibility.Visible;
    }

    private void OnControlPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_isRecording) return;
        UpdateLiveModifiersPreview();
        e.Handled = true;
    }

    private void OnControlPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isRecording) return;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // 1. Cancel on Escape
        if (key == Key.Escape)
        {
            CancelRecording();
            e.Handled = true;
            return;
        }

        // 2. Query actual physical modifier key states via Win32 GetAsyncKeyState
        bool isWin = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 ||
                     (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0 ||
                     (Keyboard.Modifiers & ModifierKeys.Windows) != 0 ||
                     key is Key.LWin or Key.RWin;

        bool isCtrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 ||
                      (Keyboard.Modifiers & ModifierKeys.Control) != 0 ||
                      key is Key.LeftCtrl or Key.RightCtrl;

        bool isAlt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0 ||
                     (Keyboard.Modifiers & ModifierKeys.Alt) != 0 ||
                     key is Key.LeftAlt or Key.RightAlt;

        bool isShift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0 ||
                       (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ||
                       key is Key.LeftShift or Key.RightShift;

        // 3. If only a modifier key is pressed, update live preview badges
        if (IsModifierKey(key))
        {
            UpdateLiveModifiersPreview(isWin, isCtrl, isAlt, isShift);
            e.Handled = true;
            return;
        }

        // 4. A non-modifier key is pressed! Validate that at least ONE modifier is held
        if (!isWin && !isCtrl && !isAlt && !isShift)
        {
            PromptText.Text = "Must include Win, Ctrl, Alt, or Shift!";
            e.Handled = true;
            return;
        }

        // 5. Convert to Win32 Virtual Key code
        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
        {
            e.Handled = true;
            return;
        }

        // 6. Build Win32 Modifier bitmask
        uint mod = NativeMethods.MOD_NOREPEAT;
        var parts = new List<string>();

        if (isWin)
        {
            mod |= NativeMethods.MOD_WIN;
            parts.Add("Win");
        }
        if (isCtrl)
        {
            mod |= NativeMethods.MOD_CONTROL;
            parts.Add("Ctrl");
        }
        if (isAlt)
        {
            mod |= NativeMethods.MOD_ALT;
            parts.Add("Alt");
        }
        if (isShift)
        {
            mod |= NativeMethods.MOD_SHIFT;
            parts.Add("Shift");
        }

        string keyName = FormatKeyName(key, vk);
        parts.Add(keyName);

        string finalDisplay = string.Join(" + ", parts);

        // 7. Commit new hotkey
        HotkeyDisplay = finalDisplay;
        Modifiers = mod;
        VirtualKey = (uint)vk;

        ParseAndRefreshBadges(finalDisplay);

        HotkeyRecorded?.Invoke(this, (mod, (uint)vk, finalDisplay));
        HotkeyRecordedCommand?.Execute((mod, (uint)vk, finalDisplay));

        CancelRecording();
        e.Handled = true;
    }

    private void UpdateLiveModifiersPreview(bool isWin = false, bool isCtrl = false, bool isAlt = false, bool isShift = false)
    {
        if (!isWin && !isCtrl && !isAlt && !isShift)
        {
            isWin = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
            isCtrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            isAlt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            isShift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        }

        _liveBadges.Clear();
        if (isWin) _liveBadges.Add(new KeyBadgeViewModel { Text = "⊞", IsWinKey = true });
        if (isCtrl) _liveBadges.Add(new KeyBadgeViewModel { Text = "Ctrl" });
        if (isAlt) _liveBadges.Add(new KeyBadgeViewModel { Text = "Alt" });
        if (isShift) _liveBadges.Add(new KeyBadgeViewModel { Text = "⇧", IsShiftKey = true });

        if (_liveBadges.Count > 0)
        {
            PromptText.Text = "+ press key";
        }
        else
        {
            PromptText.Text = "Press shortcut keys... (Esc to cancel)";
        }
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl
                   or Key.LeftAlt or Key.RightAlt
                   or Key.LeftShift or Key.RightShift
                   or Key.LWin or Key.RWin;
    }

    private static string FormatKeyName(Key key, int vk)
    {
        // Numbers 0-9
        if (key >= Key.D0 && key <= Key.D9)
            return ((int)key - (int)Key.D0).ToString();

        // Numpad 0-9
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return $"Num {((int)key - (int)Key.NumPad0)}";

        // Special common keys
        return key switch
        {
            Key.Space => "Space",
            Key.Back => "Backspace",
            Key.OemTilde => "~",
            Key.OemQuestion => "/",
            Key.OemPeriod => ".",
            Key.OemComma => ",",
            Key.OemMinus => "-",
            Key.OemPlus => "+",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemQuotes => "'",
            _ => key.ToString()
        };
    }
}
