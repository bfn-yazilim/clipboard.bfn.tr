namespace ClipboardManager.Interop;

/// <summary>
/// "Ctrl+Shift", "Ctrl+Alt", "Win" gibi metinsel modifiyer ifadelerini
/// Win32 RegisterHotKey sabitlerine cevirir. Ayarlari UI uzerinden saklarken
/// metin olarak tutuyoruz; bu sinif metin->bayrak donusumunu yapar.
/// </summary>
public static class HotkeyParser
{
    public static uint ParseModifiers(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        uint mods = 0;
        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            switch (p.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    mods |= NativeMethods.MOD_CONTROL; break;
                case "ALT":
                    mods |= NativeMethods.MOD_ALT; break;
                case "SHIFT":
                    mods |= NativeMethods.MOD_SHIFT; break;
                case "WIN":
                case "WINDOWS":
                    mods |= NativeMethods.MOD_WIN; break;
            }
        }
        return mods;
    }

    /// <summary>Tek karakter (harir/rakam) ya da "[F1]" gibi ifadeden virtual-key kodu uretir.</summary>
    public static uint ParseVirtualKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return 0;
        key = key.Trim();

        if (key.StartsWith('[') && key.EndsWith(']'))
        {
            // Ornek: [F5], [Tab]
            return 0; // ileride genisletilebilir
        }

        if (key.Length == 1)
        {
            return (uint)char.ToUpper(key[0]); // A-Z, 0-9 virtual-key kodu ASCII ile aynidir
        }

        return 0;
    }
}
