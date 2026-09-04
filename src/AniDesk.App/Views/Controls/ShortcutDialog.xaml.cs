using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using AniDesk.Core.Interop;

namespace AniDesk.App.Views.Controls;

public partial class ShortcutDialog : Window
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;
    private const int VK_MENU = 0x12; // Alt

    public string RecordedDisplay { get; private set; } = string.Empty;
    public uint RecordedModifiers { get; private set; }
    public uint RecordedVirtualKey { get; private set; }

    private readonly ObservableCollection<KeyBadgeViewModel> _badges = new();
    private bool _hasCompleteShortcut;

    public ShortcutDialog(string currentDisplay = "", uint currentModifiers = 0, uint currentVk = 0)
    {
        InitializeComponent();

        RecordedDisplay = currentDisplay;
        RecordedModifiers = currentModifiers;
        RecordedVirtualKey = currentVk;

        KeyBadgesItemsControl.ItemsSource = _badges;

        Loaded += (s, e) =>
        {
            Focus();
            Keyboard.Focus(this);
            ResetToPrompt();
        };

        PreviewKeyDown += OnWindowPreviewKeyDown;
        PreviewKeyUp += OnWindowPreviewKeyUp;
    }

    private void ResetToPrompt()
    {
        _hasCompleteShortcut = false;
        _badges.Clear();
        NoKeyText.Visibility = Visibility.Visible;
        KeyBadgesItemsControl.Visibility = Visibility.Collapsed;
        RecordingBorder.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#222B3D")!;
        SaveButton.IsEnabled = false;
    }

    private void OnWindowPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (_hasCompleteShortcut) return;

        // If all modifiers released and no full shortcut recorded yet, show "No key detected yet"
        bool anyModifier = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 ||
                           (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0 ||
                           (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 ||
                           (GetAsyncKeyState(VK_MENU) & 0x8000) != 0 ||
                           (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

        if (!anyModifier)
        {
            ResetToPrompt();
        }
        else
        {
            UpdateLiveModifiers();
        }

        e.Handled = true;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Escape cancels and closes dialog
        if (key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
            return;
        }

        // Enter confirms if a complete shortcut has been recorded
        if (key == Key.Enter && _hasCompleteShortcut)
        {
            DialogResult = true;
            Close();
            e.Handled = true;
            return;
        }

        // Detect real-time modifiers via Win32 GetAsyncKeyState
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

        // If only a modifier key is pressed, show live held badges
        if (IsModifierKey(key))
        {
            _hasCompleteShortcut = false;
            UpdateLiveModifiers(isWin, isCtrl, isAlt, isShift);
            e.Handled = true;
            return;
        }

        // A non-modifier key is pressed! Validate that at least ONE modifier is held
        if (!isWin && !isCtrl && !isAlt && !isShift)
        {
            NoKeyText.Text = "Must include Win, Ctrl, Alt, or Shift!";
            NoKeyText.Visibility = Visibility.Visible;
            KeyBadgesItemsControl.Visibility = Visibility.Collapsed;
            e.Handled = true;
            return;
        }

        // Convert key to Win32 Virtual Key code
        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
        {
            e.Handled = true;
            return;
        }

        // Build modifier flags and display string
        uint mod = NativeMethods.MOD_NOREPEAT;
        var parts = new List<string>();
        _badges.Clear();

        if (isWin)
        {
            mod |= NativeMethods.MOD_WIN;
            parts.Add("Win");
            _badges.Add(new KeyBadgeViewModel { Text = "⊞", IsWinKey = true });
        }
        if (isCtrl)
        {
            mod |= NativeMethods.MOD_CONTROL;
            parts.Add("Ctrl");
            _badges.Add(new KeyBadgeViewModel { Text = "Ctrl" });
        }
        if (isAlt)
        {
            mod |= NativeMethods.MOD_ALT;
            parts.Add("Alt");
            _badges.Add(new KeyBadgeViewModel { Text = "Alt" });
        }
        if (isShift)
        {
            mod |= NativeMethods.MOD_SHIFT;
            parts.Add("Shift");
            _badges.Add(new KeyBadgeViewModel { Text = "⇧", IsShiftKey = true });
        }

        string keyName = FormatKeyName(key, vk);
        parts.Add(keyName);
        _badges.Add(new KeyBadgeViewModel { Text = keyName });

        RecordedDisplay = string.Join(" + ", parts);
        RecordedModifiers = mod;
        RecordedVirtualKey = (uint)vk;

        _hasCompleteShortcut = true;
        NoKeyText.Visibility = Visibility.Collapsed;
        KeyBadgesItemsControl.Visibility = Visibility.Visible;
        RecordingBorder.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#CA8A04")!;
        SaveButton.IsEnabled = true;

        e.Handled = true;
    }

    private void UpdateLiveModifiers(bool isWin = false, bool isCtrl = false, bool isAlt = false, bool isShift = false)
    {
        if (!isWin && !isCtrl && !isAlt && !isShift)
        {
            isWin = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
            isCtrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            isAlt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            isShift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        }

        _badges.Clear();
        if (isWin) _badges.Add(new KeyBadgeViewModel { Text = "⊞", IsWinKey = true });
        if (isCtrl) _badges.Add(new KeyBadgeViewModel { Text = "Ctrl" });
        if (isAlt) _badges.Add(new KeyBadgeViewModel { Text = "Alt" });
        if (isShift) _badges.Add(new KeyBadgeViewModel { Text = "⇧", IsShiftKey = true });

        if (_badges.Count > 0)
        {
            NoKeyText.Visibility = Visibility.Collapsed;
            KeyBadgesItemsControl.Visibility = Visibility.Visible;
            RecordingBorder.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#CA8A04")!;
        }
        else
        {
            ResetToPrompt();
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
        if (key >= Key.D0 && key <= Key.D9)
            return ((int)key - (int)Key.D0).ToString();

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return $"Num {((int)key - (int)Key.NumPad0)}";

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

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        // Reset to default: Win + Shift + H
        RecordedModifiers = NativeMethods.MOD_WIN | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT;
        RecordedVirtualKey = 0x48; // 'H'
        RecordedDisplay = "Win + Shift + H";

        DialogResult = true;
        Close();
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (_hasCompleteShortcut)
        {
            DialogResult = true;
            Close();
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
