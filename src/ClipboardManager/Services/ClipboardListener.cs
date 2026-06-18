using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ClipboardManager.Interop;
using ClipboardManager.Models;

namespace ClipboardManager.Services;

/// <summary>
/// Pano dinleyici. Win32 AddClipboardFormatListener API'sini kullanir.
/// Arka planda kopyalanan duz metin, zengin metin (RTF) ve goruntuleri yakalar.
///
/// Onemli not: Pano okuma islemi STA thread'inde (UI thread) yapilmali; bu yuzden
/// OnClipboardUpdate UI thread'inden cagrilmalidir.
/// </summary>
public class ClipboardListener : IClipboardListener
{
    private IntPtr _hwnd;
    private bool _registered;
    private bool _suppressNext; // Kendi atadigimiz icerigi tekrar yakalamamak icin

    public bool IsListening => _registered;

    public event EventHandler<ClipboardCapturedEventArgs>? ClipboardChanged;

    public void Start(IntPtr hwnd)
    {
        _hwnd = hwnd;
        if (_registered) return;
        _registered = NativeMethods.AddClipboardFormatListener(hwnd);
    }

    /// <summary>Yaklasan bir panoyu bilincli olarak yok say (programatik set sonrasi).</summary>
    public void SuppressNext() => _suppressNext = true;

    public void OnClipboardUpdate()
    {
        if (_suppressNext)
        {
            _suppressNext = false;
            return;
        }

        try
        {
            var args = ReadClipboard();
            if (args != null)
                ClipboardChanged?.Invoke(this, args);
        }
        catch
        {
            // Pano kilitli olabilir (baska uygulama tutuyorsa); gecistir.
        }
    }

    private static ClipboardCapturedEventArgs? ReadClipboard()
    {
        // Oncelik sirasi: Image > RTF/RichText > PlainText
        if (Clipboard.ContainsImage())
        {
            var src = Clipboard.GetImage();
            if (src == null) return null;

            var bytes = EncodePng(src);
            return new ClipboardCapturedEventArgs
            {
                Kind = ClipboardItemKind.Image,
                Image = src,
                ImageBytes = bytes
            };
        }

        string? rtf = null;
        if (Clipboard.ContainsData(DataFormats.Rtf))
        {
            rtf = Clipboard.GetData(DataFormats.Rtf) as string;
        }

        if (Clipboard.ContainsText())
        {
            var text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text)) return null;
            return new ClipboardCapturedEventArgs
            {
                Kind = string.IsNullOrEmpty(rtf) ? ClipboardItemKind.PlainText : ClipboardItemKind.RichText,
                Text = text,
                RichText = rtf
            };
        }

        return null;
    }

    /// <summary>BitmapSource'u PNG baytlarina cevirir (dosyaya kayit icin).</summary>
    public static byte[] EncodePng(BitmapSource source)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    public void Dispose()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            NativeMethods.RemoveClipboardFormatListener(_hwnd);
            _registered = false;
        }
    }
}
