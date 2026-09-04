using System;
using System.IO;
using System.Threading;
using Xunit;
using AniDesk.App.ViewModels;
using AniDesk.App.Views.Controls;
using AniDesk.Core.Interop;
using AniDesk.Core.Services;

namespace AniDesk.Tests;

public class ShortcutRecorderTests
{
    [Fact]
    public void ShortcutRecorder_ParseAndRefreshBadges_RunsOnStaThread()
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                var ctrl = new ShortcutRecorderControl();
                ctrl.ParseAndRefreshBadges("Win + Shift + V");
                Assert.NotNull(ctrl);
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(ex);
    }

    [Fact]
    public void SettingsViewModel_UpdateCustomHotkey_PersistsAndUpdatesService()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AniDesk_Test_" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalStorageService(tempDir);
            var safety = new ContentSafetyService(storage);
            var cache = new ImageCacheService();

            var vm = new SettingsViewModel(storage, safety, cache, null, null);

            uint testMod = NativeMethods.MOD_WIN | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;
            uint testVk = 0x56; // 'V'
            string testDisplay = "Win + Alt + V";

            vm.UpdateCustomHotkey(testMod, testVk, testDisplay);

            Assert.Equal(testDisplay, vm.PanicHotkeyDisplay);

            var saved = storage.LoadSettings();
            Assert.Equal(testDisplay, saved.PanicHotkeyDisplay);
            Assert.Equal(testMod, saved.PanicModifiers);
            Assert.Equal(testVk, saved.PanicKey);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }
}
