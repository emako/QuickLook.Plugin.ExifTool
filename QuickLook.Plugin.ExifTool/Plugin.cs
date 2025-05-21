// Copyright © 2017-2025 QL-Win Contributors
//
// This file is part of QuickLook program.
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using QuickLook.Common.Helpers;
using QuickLook.Common.Plugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace QuickLook.Plugin.ExifTool;

public class Plugin : IViewer
{
    public enum ImageMethod
    {
        // exiftool(-k).exe test.indt -thumbnailimage -b -m > test.jpg
        ThumbnailImage,

        // exiftool(-k).exe test.indd -listItem 0 -pageimage -b -m > test.jpg
        PageImage,
    }

    private static readonly HashSet<string> hashSet =
    [
        ".indd", // Adobe InDesign Document file
        ".indt", // Adobe InDesign Template file
    ];

    private static readonly HashSet<string> WellKnownImageExtensions = hashSet;

    private ImagePanel? _ip;

    public int Priority => 0;

    public void Init()
    {
    }

    public bool CanHandle(string path)
    {
        return WellKnownImageExtensions.Contains(Path.GetExtension(path.ToLower()));
    }

    public void Prepare(string path, ContextObject context)
    {
        context.SetPreferredSizeFit(new Size { Width = 800, Height = 600 }, 0.9d);
    }

    public void View(string path, ContextObject context)
    {
        _ip = new ImagePanel
        {
            ContextObject = context,
        };

        _ = Task.Run(() =>
        {
            byte[] imageData = [];
            string ext = Path.GetExtension(path.ToLower());
            ImageMethod method = ext switch
            {
                ".indd" => ImageMethod.PageImage,
                ".indt" or _ => ImageMethod.ThumbnailImage,
            };

            imageData = ViewImage(path, method);

            if (!imageData.Any())
            {
                // Image is empty
                context.IsBusy = false;
                return;
            }

            BitmapImage bitmap = new();
            using MemoryStream stream = new(imageData);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            if (_ip is null) return;

            _ip.Dispatcher.Invoke(() =>
            {
                _ip.Source = bitmap;
                _ip.DoZoomToFit();
            });

            // In Adobe InDesign:
            // The default internal unit is points, where 1 inch = 72 points.
            // Therefore, during export, preview, or thumbnail generation,
            // the default pixel density is typically 72 DPI.
            double dpiScaleX = bitmap.DpiX / 96d;
            double dpiScaleY = bitmap.DpiY / 96d;

            context.Title = $"{(int)(bitmap.Width * dpiScaleX)}×{(int)(bitmap.Height * dpiScaleY)}: {Path.GetFileName(path)}";
            context.IsBusy = false;
        });

        context.ViewerContent = _ip;
        context.Title = $"{Path.GetFileName(path)}";
    }

    public void Cleanup()
    {
        GC.SuppressFinalize(this);

        _ip = null;
    }

    public static byte[] ViewImage(string path, ImageMethod method)
    {
        const string version = "13.29";
        string processBit = Environment.Is64BitProcess ? "64" : "32";

        string exifTool = @$".\QuickLook.Plugin\QuickLook.Plugin.ExifTool\exiftool-{version}_{processBit}\exiftool(-k).exe";

        if (!File.Exists(exifTool))
        {
            exifTool = Path.Combine(SettingHelper.LocalDataPath, @$"QuickLook.Plugin\QuickLook.Plugin.ExifTool\exiftool-{version}_{processBit}\exiftool(-k).exe");
        }

        string args = method switch
        {
            ImageMethod.PageImage => $"\"{path}\" -listItem 0 -pageimage -b -m",
            ImageMethod.ThumbnailImage or _ => $"\"{path}\" -thumbnailimage -b -m",
        };

        using Process process = new()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = exifTool,
                Arguments = args,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();

        process.StandardInput.BaseStream.Write([(byte)'\n'], 0, 1); // Deal with -k mode
        process.StandardInput.BaseStream.Flush();
        process.StandardInput.BaseStream.Close();

        using MemoryStream ms = new();
        byte[] buffer = new byte[4096];
        int bytesRead;
        while ((bytesRead = process.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            ms.Write(buffer, 0, bytesRead);
        }

        ms.Seek(0, SeekOrigin.Begin);

        try
        {
            // TODO
        }
        catch (Exception e)
        {
            ProcessHelper.WriteLog(e.ToString());
        }

        return ms.ToArray();
    }
}
