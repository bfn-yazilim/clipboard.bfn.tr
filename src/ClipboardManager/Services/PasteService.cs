using System.IO;
using System.Windows;
using ClipboardManager.Interop;
using ClipboardManager.Models;

namespace ClipboardManager.Services;

/// <summary>
/// Ogeyi panoya koyar, hedef pencereye odak verir ve SendInput ile CTRL+V tus
/// dizisini gondererek otomatik yapistirma yapar.
///
/// Pano yazma STA'da yapilmali; ayrica SendInput sonrasi kisa bir gecikme (Thread.Sleep)
/// pencerenin odagi almasi icin kritiktir - aksi halde CTRL+V kaybolur.
/// </summary>
public class PasteService : IPasteService
{
    private readonly IClipboardListener _listener;

    public PasteService(IClipboardListener listener)
    {
        _listener = listener;
    }

    public void PasteItem(ClipboardItem item, IntPtr targetWindow)
    {
        // 1) Ogeyi panoya yaz
        PutOnClipboard(item);

        // Kendi yazdigimiz icerigin dinleyici tarafindan tekrar yakalanmasini engelle
        if (_listener is ClipboardListener cl)
            cl.SuppressNext();

        // 2) Hedef pencereyi on plana getir
        if (targetWindow == IntPtr.Zero)
            targetWindow = NativeMethods.GetForegroundWindow();

        FocusWindow(targetWindow);

        // 3) Pencerenin odak almasi icin kisa sure bekle
        System.Threading.Thread.Sleep(120);

        // 4) Programatik CTRL+V gonder
        SendCtrlV();
    }

    private static void PutOnClipboard(ClipboardItem item)
    {
        // Temizle
        Clipboard.Clear();

        var data = new DataObject();

        switch (item.Kind)
        {
            case ClipboardItemKind.PlainText:
                if (!string.IsNullOrEmpty(item.Content))
                    data.SetText(item.Content, TextDataFormat.UnicodeText);
                break;

            case ClipboardItemKind.RichText:
                if (!string.IsNullOrEmpty(item.RichContent))
                    data.SetData(DataFormats.Rtf, item.RichContent);
                if (!string.IsNullOrEmpty(item.Content))
                    data.SetText(item.Content, TextDataFormat.UnicodeText);
                break;

            case ClipboardItemKind.Image:
                if (!string.IsNullOrEmpty(item.ImageFilePath) && File.Exists(item.ImageFilePath))
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(item.ImageFilePath);
                    bmp.EndInit();
                    bmp.Freeze();
                    data.SetImage(bmp);
                }
                break;
        }

        Clipboard.SetDataObject(data, true);
    }

    /// <summary>
    /// Hedef pencereyi on plana getirir. SetForegroundWindow tek basina sik calismaz;
    /// bu yuzden thread input'larini gecici olarak birlestiririz (AttachThreadInput).
    /// </summary>
    internal static void FocusWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;

        var foreThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
        var targetThread = NativeMethods.GetWindowThreadProcessId(hWnd, out _);
        var currentThread = NativeMethods.GetCurrentThreadId();

        if (foreThread != targetThread)
        {
            NativeMethods.AttachThreadInput(foreThread, currentThread, true);
            NativeMethods.AttachThreadInput(targetThread, currentThread, true);
            NativeMethods.SetForegroundWindow(hWnd);
            NativeMethods.BringWindowToTop(hWnd);
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            NativeMethods.AttachThreadInput(foreThread, currentThread, false);
            NativeMethods.AttachThreadInput(targetThread, currentThread, false);
        }
        else
        {
            NativeMethods.SetForegroundWindow(hWnd);
        }
    }

    /// <summary>SendInput ile CTRL tusunu basili tutarak V gonderir.</summary>
    internal static void SendCtrlV()
    {
        var inputs = new NativeMethods.INPUT[4];

        // CTRL down
        inputs[0].type = NativeMethods.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = NativeMethods.VK_CONTROL;
        inputs[0].u.ki.dwFlags = 0;

        // V down
        inputs[1].type = NativeMethods.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = NativeMethods.VK_V;
        inputs[1].u.ki.dwFlags = 0;

        // V up
        inputs[2].type = NativeMethods.INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = NativeMethods.VK_V;
        inputs[2].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        // CTRL up
        inputs[3].type = NativeMethods.INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = NativeMethods.VK_CONTROL;
        inputs[3].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        var size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>();
        NativeMethods.SendInput((uint)inputs.Length, inputs, size);
    }
}
