using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Serilog;

namespace Pokebar.DesktopPet.Interop;

/// <summary>
/// Helper for extracting actual icon bitmaps from desktop shortcuts and files.
/// Used by IconOverlayWindow to display the real icon image when the pet carries it —
/// instead of the generic blue placeholder box.
/// </summary>
public static class DesktopIconBitmapHelper
{
    // ── Shell structures ──

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int    iIcon;
        public uint   dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON      = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;  // 32×32
    private const uint SHGFI_SMALLICON = 0x000000001;  // 16×16 (unused)

    // ── P/Invoke ──

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    // ── Public API ──

    /// <summary>
    /// Get a frozen WPF <see cref="BitmapSource"/> for a desktop icon identified by its
    /// display name (the label shown under the icon on the desktop).
    /// Searches the user desktop folder and the public/common desktop folder.
    /// Returns <c>null</c> if the file cannot be found or the icon cannot be extracted.
    /// </summary>
    public static BitmapSource? GetDesktopIconBitmap(string iconName)
    {
        if (string.IsNullOrEmpty(iconName)) return null;

        var filePath = FindDesktopFile(iconName);
        if (filePath == null)
        {
            Log.Debug("DesktopIconBitmapHelper: No desktop file found for icon '{Name}'", iconName);
            return null;
        }

        return ExtractIconFromPath(filePath);
    }

    /// <summary>
    /// Extract a WPF <see cref="BitmapSource"/> from an arbitrary file path
    /// using <c>SHGetFileInfo</c> (respects shell icon overrides and overlay icons).
    /// </summary>
    public static BitmapSource? ExtractIconFromPath(string filePath)
    {
        var shfi = new SHFILEINFO();
        var result = SHGetFileInfo(filePath, 0, ref shfi,
            (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_LARGEICON);

        if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
        {
            Log.Debug("DesktopIconBitmapHelper: SHGetFileInfo returned no icon for '{Path}'", filePath);
            return null;
        }

        try
        {
            var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                shfi.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DesktopIconBitmapHelper: CreateBitmapSourceFromHIcon failed for '{Path}'", filePath);
            return null;
        }
        finally
        {
            DestroyIcon(shfi.hIcon);
        }
    }

    // ── Private helpers ──

    /// <summary>
    /// Search both the user desktop and the public desktop for a file whose
    /// display name (name without extension) matches <paramref name="iconName"/>.
    /// Falls back to a full-name match in case extensions are visible.
    /// </summary>
    private static string? FindDesktopFile(string iconName)
    {
        var desktopPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        };

        foreach (var desktopPath in desktopPaths)
        {
            if (!Directory.Exists(desktopPath)) continue;

            try
            {
                foreach (var file in Directory.EnumerateFiles(desktopPath))
                {
                    // Primary match: name without extension (Windows hides ".lnk", ".exe", etc.)
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
                    if (string.Equals(nameWithoutExt, iconName, StringComparison.OrdinalIgnoreCase))
                        return file;

                    // Secondary match: name with extension (e.g. "notes.txt" shown as-is)
                    var nameWithExt = Path.GetFileName(file);
                    if (string.Equals(nameWithExt, iconName, StringComparison.OrdinalIgnoreCase))
                        return file;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "DesktopIconBitmapHelper: Error enumerating '{Path}'", desktopPath);
            }
        }

        return null;
    }
}
