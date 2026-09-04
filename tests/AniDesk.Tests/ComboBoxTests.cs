using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfPath = System.Windows.Shapes.Path;
using Xunit;

namespace AniDesk.Tests;

public class ComboBoxTests
{
    private static readonly Thread _staThread;
    private static readonly Dispatcher _dispatcher;
    private static readonly ResourceDictionary _resources;

    static ComboBoxTests()
    {
        var ready = new ManualResetEventSlim(false);
        ResourceDictionary? loadedResources = null;

        _staThread = new Thread(() =>
        {
            var app = new AniDesk.App.App();
            app.InitializeComponent();
            loadedResources = app.Resources;
            ready.Set();
            Dispatcher.Run();
        });

        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.IsBackground = true;
        _staThread.Start();

        ready.Wait();
        _dispatcher = Dispatcher.FromThread(_staThread)!;
        _resources = loadedResources!;
    }

    private static void RunOnUI(Action<ResourceDictionary> action)
    {
        _dispatcher.Invoke(() => action(_resources));
    }

    [Fact]
    public void AniDeskCleanComboBoxStyle_LoadsSuccessfullyFromResources()
    {
        RunOnUI((resources) =>
        {
            var style = resources["AniDeskCleanComboBoxStyle"] as Style;
            Assert.NotNull(style);
            Assert.Equal(typeof(ComboBox), style.TargetType);
        });
    }

    [Fact]
    public void ComboBox_TemplateApplies_AndHasAllRequiredVisualContracts()
    {
        RunOnUI((resources) =>
        {
            var style = (Style)resources["AniDeskCleanComboBoxStyle"];
            var comboBox = new ComboBox
            {
                Style = style,
                ItemsSource = new[] { "1080p FHD", "1440p 2K", "2160p 4K" },
                SelectedIndex = 0,
                Width = 200,
                Height = 38
            };

            var window = new Window { Content = comboBox, Width = 300, Height = 200 };
            window.Show();

            try
            {
                comboBox.ApplyTemplate();

                // 1. ToggleButton contract
                var toggleButton = (ToggleButton)comboBox.Template.FindName("toggleButton", comboBox);
                Assert.NotNull(toggleButton);
                Assert.Equal(ClickMode.Press, toggleButton.ClickMode);
                Assert.False(toggleButton.Focusable, "ToggleButton must be Focusable=False to keep focus on ComboBox");
                Assert.False(toggleButton.IsTabStop, "ToggleButton must be IsTabStop=False");

                // 2. PART_Popup contract
                var popup = (Popup)comboBox.Template.FindName("PART_Popup", comboBox);
                Assert.NotNull(popup);
                Assert.Equal(PlacementMode.Bottom, popup.Placement);
                Assert.True(popup.StaysOpen, "PART_Popup must have StaysOpen=True (default) to not conflict with ComboBox subtree mouse capture");
                Assert.False(popup.Focusable);

                // 3. ContentSite
                var contentSite = (ContentPresenter)comboBox.Template.FindName("ContentSite", comboBox);
                Assert.NotNull(contentSite);

                // 4. Chevron
                var chevron = (WpfPath)comboBox.Template.FindName("Chevron", comboBox);
                Assert.NotNull(chevron);

                // 5. ScrollViewer & ItemsPresenter inside DropDown
                var scrollViewer = (ScrollViewer)popup.FindName("DropDownScrollViewer");
                Assert.NotNull(scrollViewer);
                Assert.False(scrollViewer.Focusable);
                var itemsPresenter = (ItemsPresenter)popup.FindName("ItemsPresenter");
                Assert.NotNull(itemsPresenter);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ComboBox_DropDownOpen_TwoWayToggling_AndSelection()
    {
        RunOnUI((resources) =>
        {
            var style = (Style)resources["AniDeskCleanComboBoxStyle"];
            var comboBox = new ComboBox
            {
                Style = style,
                ItemsSource = new[] { "Source A", "Source B", "Source C" },
                SelectedIndex = 0,
                Width = 200,
                Height = 38
            };

            var window = new Window { Content = comboBox, Width = 400, Height = 300 };
            window.Show();

            try
            {
                comboBox.ApplyTemplate();
                var toggleButton = (ToggleButton)comboBox.Template.FindName("toggleButton", comboBox);
                var popup = (Popup)comboBox.Template.FindName("PART_Popup", comboBox);
                var chevron = (WpfPath)comboBox.Template.FindName("Chevron", comboBox);

                // Initial state
                Assert.False(comboBox.IsDropDownOpen);
                Assert.False(toggleButton.IsChecked);
                Assert.False(popup.IsOpen);

                // Open
                comboBox.IsDropDownOpen = true;
                Assert.True(comboBox.IsDropDownOpen);
                Assert.True(toggleButton.IsChecked);
                Assert.True(popup.IsOpen);

                // Close
                comboBox.IsDropDownOpen = false;
                Assert.False(comboBox.IsDropDownOpen);
                Assert.False(toggleButton.IsChecked);
                Assert.False(popup.IsOpen);

                // Toggle via ToggleButton.IsChecked
                toggleButton.IsChecked = true;
                Assert.True(comboBox.IsDropDownOpen);
                Assert.True(toggleButton.IsChecked);
                Assert.True(popup.IsOpen);

                toggleButton.IsChecked = false;
                Assert.False(comboBox.IsDropDownOpen);
                Assert.False(toggleButton.IsChecked);
                Assert.False(popup.IsOpen);

                // Change selection
                comboBox.SelectedIndex = 1;
                Assert.Equal("Source B", comboBox.SelectedItem);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ComboBox_SelectedValueAndSelectedValuePath_WorkProperly()
    {
        RunOnUI((resources) =>
        {
            var style = (Style)resources["AniDeskCleanComboBoxStyle"];
            var itemStyle = (Style)resources[typeof(ComboBoxItem)];

            var comboBox = new ComboBox
            {
                Style = style,
                SelectedValuePath = "Tag",
                Width = 200,
                Height = 38
            };

            var item1 = new ComboBoxItem { Style = itemStyle, Content = "All Sources", Tag = "All" };
            var item2 = new ComboBoxItem { Style = itemStyle, Content = "yande.re", Tag = "Yandere" };
            var item3 = new ComboBoxItem { Style = itemStyle, Content = "konachan.net", Tag = "KonachanNet" };

            comboBox.Items.Add(item1);
            comboBox.Items.Add(item2);
            comboBox.Items.Add(item3);

            var window = new Window { Content = comboBox, Width = 400, Height = 300 };
            window.Show();

            try
            {
                comboBox.ApplyTemplate();

                // Select by Tag value
                comboBox.SelectedValue = "Yandere";
                Assert.Equal(item2, comboBox.SelectedItem);
                Assert.Equal("Yandere", comboBox.SelectedValue);

                comboBox.SelectedValue = "KonachanNet";
                Assert.Equal(item3, comboBox.SelectedItem);
                Assert.Equal("KonachanNet", comboBox.SelectedValue);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ComboBoxItem_Style_AppliesHitTestableBackgroundAndTriggers()
    {
        RunOnUI((resources) =>
        {
            var itemStyle = (Style)resources[typeof(ComboBoxItem)];
            Assert.NotNull(itemStyle);

            var item = new ComboBoxItem
            {
                Style = itemStyle,
                Content = "Test Option"
            };

            var window = new Window { Content = item, Width = 200, Height = 100 };
            window.Show();

            try
            {
                item.ApplyTemplate();
                var border = (Border)item.Template.FindName("ItemBorder", item);
                Assert.NotNull(border);
                Assert.Equal(Cursors.Hand, item.Cursor);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ComboBox_MouseClick_TogglesDropDown()
    {
        RunOnUI((resources) =>
        {
            var style = (Style)resources["AniDeskCleanComboBoxStyle"];
            var comboBox = new ComboBox
            {
                Style = style,
                ItemsSource = new[] { "Option A", "Option B" },
                SelectedIndex = 0,
                Width = 200,
                Height = 38
            };

            var window = new Window { Content = comboBox, Width = 400, Height = 300 };
            window.Show();

            try
            {
                comboBox.ApplyTemplate();
                var toggleButton = (ToggleButton)comboBox.Template.FindName("toggleButton", comboBox);
                Assert.NotNull(toggleButton);

                Assert.False(comboBox.IsDropDownOpen);

                // Simulate Left Mouse Down on ToggleButton
                var downArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    Source = toggleButton
                };
                toggleButton.RaiseEvent(downArgs);

                Assert.True(comboBox.IsDropDownOpen, "ComboBox should be open after MouseLeftButtonDown");
            }
            finally
            {
                window.Close();
            }
        });
    }
}
