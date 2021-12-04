﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Xml;

namespace dotnetCampus.OfficeDocumentZipper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenOfficeFile_OnClick(object sender, RoutedEventArgs e)
        {
            if (!CheckFileExists())
            {
                return;
            }

            var file = GetFile();

            Process.Start(new ProcessStartInfo("explorer")
            {
                ArgumentList =
                {
                    file
                }
            });
        }

        private void Unzip_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                UnzipFile();
            }
            catch (Exception exception)
            {
                Warn(exception.ToString());
            }
        }

        private void UnzipAndFormatFile_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                UnzipFile();

                var directory = OfficeFolder.Text;
                FormatXml(directory);
            }
            catch (Exception exception)
            {
                Warn(exception.ToString());
            }
        }

        private void UnzipFile()
        {
            if (!CheckFileExists())
            {
                return;
            }

            if (string.IsNullOrEmpty(OfficeFolder.Text))
            {
                Warn($"OfficeFolder can not be empty");
                return;
            }

            var file = GetFile();

            var directory = OfficeFolder.Text;

            if (Directory.Exists(directory))
            {
                try
                {
                    Directory.Delete(directory, true);
                }
                catch (Exception exception)
                {
                    Warn($"Delete {directory} {exception}");
                    return;
                }
            }

            Directory.CreateDirectory(directory);
            System.IO.Compression.ZipFile.ExtractToDirectory(file, directory, true);

            // 这个方法对嵌入excel表格的PPT文件进行处理。
            UnZipOleObjectFile();

            Warn("");
        }

        /// <summary>
        /// 这个方法对嵌入excel表格的PPT文件进行处理。
        /// 如果PPT文件中嵌入了Excel表格，则调用该方法对OleObj映射的xlsx文件进行解压缩。
        /// </summary>
        private void UnZipOleObjectFile()
        {
            // 获取OleObj的映射的xlsx文件。
            var oleObjDirectory = Path.Combine(OfficeFolder.Text, "ppt\\embeddings\\");
            if (Directory.Exists(oleObjDirectory))
            {
                var oleFileInfo = new DirectoryInfo(oleObjDirectory);
                foreach (var fileInfo in oleFileInfo.GetFiles())
                {
                    if (fileInfo.Extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        var directoryName = Path.Combine(fileInfo.DirectoryName, Path.GetFileNameWithoutExtension(fileInfo.Name));
                        Directory.CreateDirectory(directoryName);
                        System.IO.Compression.ZipFile.ExtractToDirectory(fileInfo.FullName, directoryName, true);
                    }
                }
            }
        }

        private static void FormatXml(string directory)
        {
            foreach (var xmlFile in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                if (!xmlFile.EndsWith(".xml.rels", StringComparison.OrdinalIgnoreCase) &&
                    !xmlFile.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var xmlString = File.ReadAllText(xmlFile);
                    XmlDocument document = new XmlDocument();
                    document.LoadXml(xmlString);

                    using var fileStream = new FileStream(xmlFile, FileMode.Create, FileAccess.Write);
                    fileStream.SetLength(0);

                    using var xmlWriter = XmlWriter.Create(fileStream, new XmlWriterSettings()
                    {
                        Indent = true
                    });
                    document.WriteTo(xmlWriter);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception);
                }
            }
        }

        private bool CheckFileExists()
        {
            var file = GetFile();

            if (string.IsNullOrEmpty(file))
            {
                Warn($"Office File can not be empty");
                return false;
            }

            if (!File.Exists(file))
            {
                Warn($"Office file {file} not found");
                return false;
            }
            Warn("");

            return true;
        }

        private bool CheckFolderExists()
        {
            if (string.IsNullOrEmpty(OfficeFolder.Text))
            {
                Warn($"OfficeFolder can not be empty");
                return false;
            }

            if (!Directory.Exists(OfficeFolder.Text))
            {
                Warn($"Office Folder {OfficeFolder.Text} not found");
                return false;
            }
            Warn("");

            return true;
        }

        private void Explorer_OnClick(object sender, RoutedEventArgs e)
        {
            if (!CheckFolderExists())
            {
                return;
            }

            Process.Start(new ProcessStartInfo("explorer")
            {
                ArgumentList =
                {
                    OfficeFolder.Text
                }
            });

            Warn("");
        }

        /// <summary>
        /// 压缩PPT文件。
        /// </summary>
        private void ZipFile()
        {
            if (!CheckFolderExists())
            {
                return;
            }

            var file = GetFile();

            if (string.IsNullOrEmpty(file))
            {
                Warn($"Office File can not be empty");
                return;
            }

            if (File.Exists(file))
            {
                file = CreateFileName(file);
            }

            var directory = OfficeFolder.Text;

            // 这个步骤是查找文件夹里是否存在oleObj元素映射的xlsx文件，并进行压缩处理。
            ZipOleObjectFile();

            System.IO.Compression.ZipFile.CreateFromDirectory(directory, file, CompressionLevel.NoCompression, false);

            // 还原ZipOleObjectFile方法删除的Excel文件解压出来的文件夹。
            UnZipOleObjectFile();

            OfficeFile.Text = file;
            OfficeFolder.Text = directory;

            Warn("");
        }

        private void Zip_OnClick(object sender, RoutedEventArgs e)
        {
            // 涉及IO操作，最好捕获一下异常。
            try
            {
                ZipFile();
            }
            catch (Exception exception)
            {
                Warn(exception.ToString());
            }
        }

        /// <summary>
        /// 这个方法对嵌入excel表格的PPT文件进行处理。
        ///  如果OleObj映射的xlsx文件被解压缩，则将解压缩的文件夹压缩为xlsx文件，并删除原有文件夹。
        /// </summary>
        private void ZipOleObjectFile()
        {
            // 获取OleObj的映射的xlsx文件。
            var oleObjDirectory = Path.Combine(OfficeFolder.Text, "ppt\\embeddings\\");
            if (Directory.Exists(oleObjDirectory))
            {
                var oleFileInfo = new DirectoryInfo(oleObjDirectory);
                var fileInfos = oleFileInfo.GetFiles();
                foreach (var directoryInfo in oleFileInfo.GetDirectories())
                {
                    // 如果xlsx文件去掉后缀后与文件夹同名，那我们将认为该文件夹是由xlsx文件解压缩而来的，此时需要替换原有的 xlsx 文件并删除 xlsx 对应的文件夹。
                    if (fileInfos.Any(fileInfo => Path.GetFileNameWithoutExtension(fileInfo.Name).Equals(directoryInfo.Name)
                        && fileInfo.Extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)))
                    {
                        var fileName = Path.ChangeExtension(directoryInfo.FullName, ".xlsx");
                        File.Delete(fileName);
                        System.IO.Compression.ZipFile.CreateFromDirectory(directoryInfo.FullName, fileName, CompressionLevel.NoCompression, false);

                        // 必须要删除原目录，否则PPT打开错误。
                        directoryInfo.Delete(true);
                    }
                }
            }
        }

        private string CreateFileName(string file)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var extension = Path.GetExtension(file);
            var regex = new Regex(@"\((\d+)\)");

            int i = 0;
            var matchCollection = regex.Matches(name);
            foreach (Match match in matchCollection)
            {
                var value = match.Groups[1].Value;
                var str = $"({value})";
                if (name.EndsWith(str))
                {
                    name = name.Remove(name.Length - str.Length);

                    int.TryParse(value, out i);

                    break;
                }
            }

            string directory = Path.GetDirectoryName(file)!;

            for (; true; i++)
            {
                var fileName = Path.Combine(directory!, $"{name}({i}){extension}");

                if (!File.Exists(fileName))
                {
                    return fileName;
                }
            }
        }

        private void Warn(string text)
        {
            if (Dispatcher.CheckAccess())
            {
                Warning.Text = text;
            }
            else
            {
                Dispatcher.InvokeAsync(() =>
                {
                    Warning.Text = text;
                });
            }
        }

        private void OfficeFile_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var file = GetFile();

            if (string.IsNullOrEmpty(file))
            {
                return;
            }

            var name = Path.GetFileNameWithoutExtension(file);

            string directory = Path.GetDirectoryName(file) ?? string.Empty;
            directory = Path.Combine(directory, name);

            OfficeFolder.Text = directory;
        }

        private string GetFile()
        {
            var file = OfficeFile.Text;

            if (string.IsNullOrEmpty(file))
            {
                return string.Empty;
            }

            if (file.StartsWith("\""))
            {
                file = file.Substring(1);
            }

            if (file.EndsWith("\""))
            {
                file = file.Remove(file.Length - 1);
            }

            file = Path.GetFullPath(file);
            return file;
        }

        private void ZipAndOpen_OnClick(object sender, RoutedEventArgs e)
        {
            // 涉及IO操作，最好捕获一下异常。
            try
            {
                ZipFile();
            }
            catch (Exception exception)
            {
                Warn(exception.ToString());

                // 异常了就没必要打开PPT文件了。
                return;
            }

            OpenOfficeFile_OnClick(sender, e);
        }

        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start(@"explorer", "https://github.com/dotnet-campus/dotnetCampus.OfficeDocumentZipper");
        }
    }
}
