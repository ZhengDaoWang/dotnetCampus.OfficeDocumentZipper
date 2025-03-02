﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Xml;

// ReSharper disable ArrangeObjectCreationWhenTypeEvident
// ReSharper disable SuggestVarOrType_SimpleTypes

namespace DocumentFormat.OpenXml.Flatten.Compatibilities.Packaging
{
    /// <summary>
    /// 进行兼容的处理，用来解决一些特殊的文档无法打开
    /// <para>
    /// 能修复的内容如下
    /// </para>
    /// - 在 Package 找不到指定的 Part 的修复类型，比 <see cref="CompatiblePackage"/> 使用更加宽松的逻辑，如果某个 Part 不存在，那么返回 <see cref="CompatiblePackage.EmptyPackagePart"/> 实例
    /// <para/>
    /// - 使用 WPS 创建的文件名包含中文的采用 GBK 编码的文件，寻找不到。兼容之后，依然丢失单个文件，但是文档可以打开
    /// </summary>
    /// 这边是抄的ZipPackage源码，由于ZipPackage没开放出来方法，因此只能抄
    /// ZipPackage is a specific implementation for the abstract Package
    /// class, corresponding to the Zip file format.
    /// This is a part of the Packaging Layer APIs.
    partial class CompatiblePackage : Package
    {
        #region 定制逻辑

        /// <inheritdoc />
        public override bool PartExists(Uri partUri)
        {
            var result = base.PartExists(partUri);

            return result;
        }

        /// <inheritdoc />
        protected override PackagePart GetPartCore(Uri partUri)
        {
            return new CompatiblePackage.EmptyPackagePart(this, partUri, _contentTypeHelper);
        }

        #endregion

        #region Public Methods

        #region PackagePart Methods

        /// <summary>
        /// This method is for custom implementation for the underlying file format
        /// Adds a new item to the zip archive corresponding to the PackagePart in the package.
        /// </summary>
        /// <param name="partUri">PartName</param>
        /// <param name="contentType">Content type of the part</param>
        /// <param name="compressionOption">Compression option for this part</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">If partUri parameter is null</exception>
        /// <exception cref="ArgumentNullException">If contentType parameter is null</exception>
        /// <exception cref="ArgumentException">If partUri parameter does not conform to the valid partUri syntax</exception>
        /// <exception cref="ArgumentOutOfRangeException">If CompressionOption enumeration [compressionOption] does not have one of the valid values</exception>
        protected override PackagePart CreatePartCore(Uri partUri,
            string contentType,
            CompressionOption compressionOption)
        {
            //Validating the PartUri - this method will do the argument checking required for uri.
            partUri = CompatiblePackage.PackUriHelper.ValidatePartUri(partUri);

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            //Package.ThrowIfCompressionOptionInvalid(compressionOption);

            // Convert XPS CompressionOption to Zip CompressionMethodEnum.
            CompressionLevel level;
            GetZipCompressionMethodFromOpcCompressionOption(compressionOption,
                out level);

            // Create new Zip item.
            // We need to remove the leading "/" character at the beginning of the part name.
            // The partUri object must be a ValidatedPartUri
            string zipItemName = ((CompatiblePackage.PackUriHelper.ValidatedPartUri) partUri).PartUriString.Substring(1);

            ZipArchiveEntry zipArchiveEntry = _zipArchive.CreateEntry(zipItemName, level);

            //Store the content type of this part in the content types stream.
            _contentTypeHelper.AddContentType((CompatiblePackage.PackUriHelper.ValidatedPartUri) partUri, new CompatiblePackage.ContentType(contentType),
                level);

            return new CompatiblePackage.ZipPackagePart(this, zipArchiveEntry.Archive, zipArchiveEntry, _zipStreamManager,
                (CompatiblePackage.PackUriHelper.ValidatedPartUri) partUri, contentType, compressionOption);
        }

        ///// <summary>
        ///// This method is for custom implementation specific to the file format.
        ///// Returns the part after reading the actual physical bits. The method
        ///// returns a null to indicate that the part corresponding to the specified
        ///// Uri was not found in the container.
        ///// This method does not throw an exception if a part does not exist.
        ///// </summary>
        ///// <param name="partUri"></param>
        ///// <returns></returns>
        //protected override PackagePart? GetPartCore(Uri partUri)
        //{
        //    //Currently the design has two aspects which makes it possible to return
        //    //a null from this method -
        //    //  1. All the parts are loaded at Package.Open time and as such, this
        //    //     method would not be invoked, unless the user is asking for -
        //    //     i. a part that does not exist - we can safely return null
        //    //     ii.a part(interleaved/non-interleaved) that was added to the
        //    //        underlying package by some other means, and the user wants to
        //    //        access the updated part. This is currently not possible as the
        //    //        underlying zip i/o layer does not allow for FileShare.ReadWrite.
        //    //  2. Also, its not a straightforward task to determine if a new part was
        //    //     added as we need to look for atomic as well as interleaved parts and
        //    //     this has to be done in a case sensitive manner. So, effectively
        //    //     we will have to go through the entire list of zip items to determine
        //    //     if there are any updates.
        //    //  If ever the design changes, then this method must be updated accordingly

        //    return null;
        //}

        /// <summary>
        /// This method is for custom implementation specific to the file format.
        /// Deletes the part corresponding to the uri specified. Deleting a part that does not
        /// exists is not an error and so we do not throw an exception in that case.
        /// </summary>
        /// <param name="partUri"></param>
        /// <exception cref="ArgumentNullException">If partUri parameter is null</exception>
        /// <exception cref="ArgumentException">If partUri parameter does not conform to the valid partUri syntax</exception>
        protected override void DeletePartCore(Uri partUri)
        {

            //Validating the PartUri - this method will do the argument checking required for uri.
            partUri = CompatiblePackage.PackUriHelper.ValidatePartUri(partUri);

            string partZipName = GetZipItemNameFromOpcName(CompatiblePackage.PackUriHelper.GetStringForPartUri(partUri));
            ZipArchiveEntry? zipArchiveEntry = _zipArchive.GetEntry(partZipName);
            if (zipArchiveEntry != null)
            {
                // Case of an atomic part.
                zipArchiveEntry.Delete();
            }

            //Delete the content type for this part if it was specified as an override
            _contentTypeHelper.DeleteContentType((CompatiblePackage.PackUriHelper.ValidatedPartUri) partUri);
        }

        /// <summary>
        /// This method is for custom implementation specific to the file format.
        /// This is the method that knows how to get the actual parts from the underlying
        /// zip archive.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Some or all of the parts may be interleaved. The Part object for an interleaved part encapsulates
        /// the Uri of the proper part name and the ZipFileInfo of the initial piece.
        /// This function does not go through the extra work of checking piece naming validity
        /// throughout the package.
        /// </para>
        /// <para>
        /// This means that interleaved parts without an initial piece will be silently ignored.
        /// Other naming anomalies get caught at the Stream level when an I/O operation involves
        /// an anomalous or missing piece.
        /// </para>
        /// <para>
        /// This function reads directly from the underlying IO layer and is supposed to be called
        /// just once in the lifetime of a package (at init time).
        /// </para>
        /// </remarks>
        /// <returns>An array of ZipPackagePart.</returns>
        protected override PackagePart[] GetPartsCore()
        {
            List<PackagePart> parts = new List<PackagePart>(InitialPartListSize);

            // The list of files has to be searched linearly (1) to identify the content type
            // stream, and (2) to identify parts.
            System.Collections.ObjectModel.ReadOnlyCollection<ZipArchiveEntry> zipArchiveEntries = _zipArchive.Entries;

            // We have already identified the [ContentTypes].xml pieces if any are present during
            // the initialization of ZipPackage object

            // Record parts and ignored items.
            foreach (ZipArchiveEntry zipArchiveEntry in zipArchiveEntries)
            {
                //Returns false if -
                // a. its a content type item
                // b. items that have either a leading or trailing slash.
                if (IsZipItemValidOpcPartOrPiece(zipArchiveEntry.FullName))
                {
                    Uri partUri = new Uri(GetOpcNameFromZipItemName(zipArchiveEntry.FullName), UriKind.Relative);
                    if (CompatiblePackage.PackUriHelper.TryValidatePartUri(partUri, out CompatiblePackage.PackUriHelper.ValidatedPartUri? validatedPartUri))
                    {
                        CompatiblePackage.ContentType? contentType = _contentTypeHelper.GetContentType(validatedPartUri);
                        if (contentType != null)
                        {
                            // In case there was some redundancy between pieces and/or the atomic
                            // part, it will be detected at this point because the part's Uri (which
                            // is independent of interleaving) will already be in the dictionary.
                            parts.Add(new CompatiblePackage.ZipPackagePart(this, zipArchiveEntry.Archive, zipArchiveEntry,
                                _zipStreamManager, validatedPartUri, contentType.ToString(),
                                GetCompressionOptionFromZipFileInfo(zipArchiveEntry)));
                        }
                    }
                    //If not valid part uri we can completely ignore this zip file item. Even if later someone adds
                    //a new part, the corresponding zip item can never map to one of these items
                }
                // If IsZipItemValidOpcPartOrPiece returns false, it implies that either the zip file Item
                // starts or ends with a "/" and as such we can completely ignore this zip file item. Even if later
                // a new part gets added, its corresponding zip item cannot map to one of these items.
            }

            return parts.ToArray();
        }

        #endregion PackagePart Methods

        #region Other Methods

        /// <summary>
        /// This method is for custom implementation corresponding to the underlying zip file format.
        /// </summary>
        protected override void FlushCore()
        {
            //Save the content type file to the archive.
            _contentTypeHelper.SaveToFile();
        }

        /// <summary>
        /// Closes the underlying ZipArchive object for this container
        /// </summary>
        /// <param name="disposing">True if called during Dispose, false if called during Finalize</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_contentTypeHelper != null)
                    {
                        _contentTypeHelper.SaveToFile();
                    }

                    if (_zipStreamManager != null)
                    {
                        _zipStreamManager.Dispose();
                    }

                    if (_zipArchive != null)
                    {
                        _zipArchive.Dispose();
                    }

                    // _containerStream may be opened given a file name, in which case it should be closed here.
                    // _containerStream may be passed into the constructor, in which case, it should not be closed here.
                    if (_shouldCloseContainerStream)
                    {
                        _containerStream.Dispose();
                    }
                    else
                    {
                    }

                    _containerStream = null!;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        #endregion Other Methods

        #endregion Public Methods

        #region Internal Constructors

        /// <summary>
        /// 打开内容
        /// </summary>
        /// <param name="s"></param>
        /// <param name="packageFileMode"></param>
        /// <param name="packageFileAccess"></param>
        /// <returns></returns>
        public static CompatiblePackage OpenPackage(Stream s, FileMode packageFileMode,
            FileAccess packageFileAccess)
        {
            var package = new CompatiblePackage(s, packageFileMode, packageFileAccess);

            //We need to get all the parts if any exists from the underlying file
            //so that we have the names in the Normalized form in our in-memory
            //data structures.
            //Note: If ever this call is removed, each individual call to GetPartCore,
            //may result in undefined behavior as the underlying ZipArchive, maintains the
            //files list as being case-sensitive.
            if (package.FileOpenAccess == FileAccess.ReadWrite || package.FileOpenAccess == FileAccess.Read)
            {
                package.GetParts();
            }

            return package;
        }

        ///// <summary>
        ///// Internal constructor that is called by the OpenOnFile static method.
        ///// </summary>
        ///// <param name="path">File path to the container.</param>
        ///// <param name="packageFileMode">Container is opened in the specified mode if possible</param>
        ///// <param name="packageFileAccess">Container is opened with the specified access if possible</param>
        ///// <param name="share">Container is opened with the specified share if possible</param>

        //internal CompensationPartNotExistPackage(string path, FileMode packageFileMode, FileAccess packageFileAccess, FileShare share)
        //    : base(packageFileAccess)
        //{
        //    ZipArchive? zipArchive = null;
        //    ContentTypeHelper? contentTypeHelper = null;
        //    _packageFileMode = packageFileMode;
        //    _packageFileAccess = packageFileAccess;

        //    try
        //    {
        //        _containerStream = new FileStream(path, _packageFileMode, _packageFileAccess, share);
        //        _shouldCloseContainerStream = true;
        //        ZipArchiveMode zipArchiveMode = ZipArchiveMode.Update;
        //        if (packageFileAccess == FileAccess.Read)
        //            zipArchiveMode = ZipArchiveMode.Read;
        //        else if (packageFileAccess == FileAccess.Write)
        //            zipArchiveMode = ZipArchiveMode.Create;
        //        else if (packageFileAccess == FileAccess.ReadWrite)
        //            zipArchiveMode = ZipArchiveMode.Update;

        //        zipArchive = new ZipArchive(_containerStream, zipArchiveMode, true, System.Text.Encoding.UTF8);
        //        _zipStreamManager = new ZipStreamManager(zipArchive, _packageFileMode, _packageFileAccess);
        //        contentTypeHelper =
        //            new ContentTypeHelper(zipArchive, _packageFileMode, _packageFileAccess, _zipStreamManager);
        //    }
        //    catch
        //    {
        //        zipArchive?.Dispose();
        //        _containerStream?.Dispose();

        //        throw;
        //    }

        //    _zipArchive = zipArchive;
        //    _contentTypeHelper = contentTypeHelper;
        //}

        /// <summary>
        /// Internal constructor that is called by the Open(Stream) static methods.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="packageFileMode"></param>
        /// <param name="packageFileAccess"></param>
        private CompatiblePackage(Stream s, FileMode packageFileMode, FileAccess packageFileAccess)
            : base(packageFileAccess)
        {
            ZipArchive? zipArchive = null;
            ContentTypeHelper? contentTypeHelper = null;
            _packageFileMode = packageFileMode;
            _packageFileAccess = packageFileAccess;

            try
            {
                if (s.CanSeek)
                {
                    switch (packageFileMode)
                    {
                        case FileMode.Open:
                            if (s.Length == 0)
                            {
                                throw new FileFormatException("SR.ZipZeroSizeFileIsNotValidArchive");
                            }

                            break;

                        case FileMode.CreateNew:
                            if (s.Length != 0)
                            {
                                throw new IOException("SR.CreateNewOnNonEmptyStream");
                            }

                            break;

                        case FileMode.Create:
                            if (s.Length != 0)
                            {
                                s.SetLength(0); // Discard existing data
                            }

                            break;
                    }
                }

                ZipArchiveMode zipArchiveMode = ZipArchiveMode.Update;
                if (packageFileAccess == FileAccess.Read)
                    zipArchiveMode = ZipArchiveMode.Read;
                else if (packageFileAccess == FileAccess.Write)
                    zipArchiveMode = ZipArchiveMode.Create;
                else if (packageFileAccess == FileAccess.ReadWrite)
                    zipArchiveMode = ZipArchiveMode.Update;

                zipArchive = new ZipArchive(s, zipArchiveMode, true, System.Text.Encoding.UTF8);

                _zipStreamManager = new CompatiblePackage.ZipStreamManager(zipArchive, packageFileMode, packageFileAccess);
                contentTypeHelper =
                    new ContentTypeHelper(zipArchive, packageFileMode, packageFileAccess, _zipStreamManager);
            }
            catch (InvalidDataException)
            {
                throw new FileFormatException("SR.FileContainsCorruptedData");
            }
            catch
            {
                if (zipArchive != null)
                {
                    zipArchive.Dispose();
                }

                throw;
            }

            _containerStream = s;
            _shouldCloseContainerStream = false;
            _zipArchive = zipArchive;
            _contentTypeHelper = contentTypeHelper;
        }

        #endregion Internal Constructors

        #region Internal Methods

        // More generic function than GetZipItemNameFromPartName. In particular, it will handle piece names.
        internal static string GetZipItemNameFromOpcName(string opcName)
        {
            Debug.Assert(opcName != null && opcName.Length > 0);
            return opcName!.Substring(1);
        }

        // More generic function than GetPartNameFromZipItemName. In particular, it will handle piece names.
        internal static string GetOpcNameFromZipItemName(string zipItemName)
        {
            return string.Concat(ForwardSlashString, zipItemName);
        }

        // Convert from XPS CompressionOption to ZipFileInfo compression properties.
        internal static void GetZipCompressionMethodFromOpcCompressionOption(
            CompressionOption compressionOption,
            out CompressionLevel compressionLevel)
        {
            switch (compressionOption)
            {
                case CompressionOption.NotCompressed:
                    {
                        compressionLevel = CompressionLevel.NoCompression;
                    }
                    break;
                case CompressionOption.Normal:
                    {
                        compressionLevel = CompressionLevel.Optimal;
                    }
                    break;
                case CompressionOption.Maximum:
                    {
                        compressionLevel = CompressionLevel.Optimal;
                    }
                    break;
                case CompressionOption.Fast:
                    {
                        compressionLevel = CompressionLevel.Fastest;
                    }
                    break;
                case CompressionOption.SuperFast:
                    {
                        compressionLevel = CompressionLevel.Fastest;
                    }
                    break;

                // fall-through is not allowed
                default:
                    {
                        Debug.Fail("Encountered an invalid CompressionOption enum value");
                        goto case CompressionOption.NotCompressed;
                    }
            }
        }

        #endregion Internal Methods

        internal FileMode PackageFileMode
        {
            get
            {
                return _packageFileMode;
            }
        }

        #region Private Methods

        //returns a boolean indicating if the underlying zip item is a valid metro part or piece
        // This mainly excludes the content type item, as well as entries with leading or trailing
        // slashes.
        private bool IsZipItemValidOpcPartOrPiece(string zipItemName)
        {
            Debug.Assert(zipItemName != null, "The parameter zipItemName should not be null");

            //check if the zip item is the Content type item -case sensitive comparison
            // The following test will filter out an atomic content type file, with name
            // "[Content_Types].xml", as well as an interleaved one, with piece names such as
            // "[Content_Types].xml/[0].piece" or "[Content_Types].xml/[5].last.piece".
            if (zipItemName.StartsWith(ContentTypeHelper.ContentTypeFileName, StringComparison.OrdinalIgnoreCase))
                return false;
            else
            {
                //Could be an empty zip folder
                //We decided to ignore zip items that contain a "/" as this could be a folder in a zip archive
                //Some of the tools support this and some don't. There is no way ensure that the zip item never have
                //a leading "/", although this is a requirement we impose on items created through our API
                //Therefore we ignore them at the packaging api level.
                if (zipItemName.StartsWith(ForwardSlashString, StringComparison.Ordinal))
                    return false;
                //This will ignore the folder entries found in the zip package created by some zip tool
                //PartNames ending with a "/" slash is also invalid so we are skipping these entries,
                //this will also prevent the PackUriHelper.CreatePartUri from throwing when it encounters a
                // partname ending with a "/"
                if (zipItemName.EndsWith(ForwardSlashString, StringComparison.Ordinal))
                    return false;
                else
                    return true;
            }
        }

        // convert from Zip CompressionMethodEnum and DeflateOptionEnum to XPS CompressionOption
        private static CompressionOption GetCompressionOptionFromZipFileInfo(ZipArchiveEntry zipFileInfo)
        {
            // Note: we can't determine compression method / level from the ZipArchiveEntry.
            CompressionOption result = CompressionOption.Normal;
            return result;
        }

        #endregion Private Methods

        #region Private Members

        private const int InitialPartListSize = 50;

        private readonly ZipArchive _zipArchive;
        private Stream _containerStream; // stream we are opened in if Open(Stream) was called
        private readonly bool _shouldCloseContainerStream;

        private readonly ContentTypeHelper
            _contentTypeHelper; // manages the content types for all the parts in the container

        private readonly CompatiblePackage.ZipStreamManager
            _zipStreamManager; // manages streams for all parts, avoiding opening streams multiple times

        private readonly FileAccess _packageFileAccess;
        private readonly FileMode _packageFileMode;

        private const string ForwardSlashString = "/"; //Required for creating a part name from a zip item name

        //IEqualityComparer for extensions
        private static readonly ExtensionEqualityComparer s_extensionEqualityComparer = new ExtensionEqualityComparer();

        #endregion Private Members

        /// <summary>
        /// ExtensionComparer
        /// The Extensions are stored in the Default Dictionary in their original form,
        /// however they are compared in a normalized manner.
        /// Equivalence for extensions in the content type stream, should follow
        /// the same rules as extensions of partnames. Also, by the time this code is invoked,
        /// we have already validated, that the extension is in the correct format as per the
        /// part name rules.So we are simplifying the logic here to just convert the extensions
        /// to Upper invariant form and then compare them.
        /// </summary>
        private sealed class ExtensionEqualityComparer : IEqualityComparer<string>
        {
            bool IEqualityComparer<string>.Equals(string? extensionA, string? extensionB)
            {
                Debug.Assert(extensionA != null, "extension should not be null");
                Debug.Assert(extensionB != null, "extension should not be null");

                //Important Note: any change to this should be made in accordance
                //with the rules for comparing/normalizing partnames.
                //Refer to PackUriHelper.ValidatedPartUri.GetNormalizedPartUri method.
                //Currently normalization just involves upper-casing ASCII and hence the simplification.
                return (string.CompareOrdinal(extensionA.ToUpperInvariant(), extensionB.ToUpperInvariant()) == 0);
            }

            int IEqualityComparer<string>.GetHashCode(string extension)
            {
                Debug.Assert(extension != null, "extension should not be null");

                //Important Note: any change to this should be made in accordance
                //with the rules for comparing/normalizing partnames.
                //Refer to PackUriHelper.ValidatedPartUri.GetNormalizedPartUri method.
                //Currently normalization just involves upper-casing ASCII and hence the simplification.
                return extension.ToUpperInvariant().GetHashCode();
            }
        }

        /// <summary>
        /// This is a helper class that maintains the Content Types File related to
        /// this ZipPackage.
        /// </summary>
        private sealed class ContentTypeHelper
        {
            /// <summary>
            /// Initialize the object without uploading any information from the package.
            /// Complete initialization in read mode also involves calling ParseContentTypesFile
            /// to deserialize content type information.
            /// </summary>
            internal ContentTypeHelper(ZipArchive zipArchive, FileMode packageFileMode, FileAccess packageFileAccess,
                CompatiblePackage.ZipStreamManager zipStreamManager)
            {
                _zipArchive = zipArchive; //initialized in the ZipPackage constructor
                _packageFileMode = packageFileMode;
                _packageFileAccess = packageFileAccess;
                _zipStreamManager = zipStreamManager; //initialized in the ZipPackage constructor
                // The extensions are stored in the default Dictionary in their original form , but they are compared
                // in a normalized manner using the ExtensionComparer.
                _defaultDictionary =
                    new Dictionary<string, CompatiblePackage.ContentType>(DefaultDictionaryInitialSize, s_extensionEqualityComparer);

                // Identify the content type file or files before identifying parts and piece sequences.
                // This is necessary because the name of the content type stream is not a part name and
                // the information it contains is needed to recognize valid parts.
                if (_zipArchive.Mode == ZipArchiveMode.Read || _zipArchive.Mode == ZipArchiveMode.Update)
                    ParseContentTypesFile(_zipArchive.Entries);

                //No contents to persist to the disk -
                _dirty = false; //by default

                //Lazy initialize these members as required
                //_overrideDictionary      - Overrides should be rare
                //_contentTypeFileInfo     - We will either find an atomin part, or
                //_contentTypeStreamPieces - an interleaved part
                //_contentTypeStreamExists - defaults to false - not yet found
            }

            internal static string ContentTypeFileName
            {
                get
                {
                    return ContentTypesFile;
                }
            }

            //Adds the Default entry if it is the first time we come across
            //the extension for the partUri, does nothing if the content type
            //corresponding to the default entry for the extension matches or
            //adds a override corresponding to this part and content type.
            //This call is made when a new part is being added to the package.

            // This method assumes the partUri is valid.
            internal void AddContentType(CompatiblePackage.PackUriHelper.ValidatedPartUri partUri, CompatiblePackage.ContentType contentType,
                CompressionLevel compressionLevel)
            {
                //save the compressionOption and deflateOption that should be used
                //to create the content type item later
                if (!_contentTypeStreamExists)
                {
                    _cachedCompressionLevel = compressionLevel;
                }

                // Figure out whether the mapping matches a default entry, can be made into a new
                // default entry, or has to be entered as an override entry.
                bool foundMatchingDefault = false;
                string extension = partUri.PartUriExtension;

                // Need to create an override entry?
                if (extension.Length == 0
                    || (_defaultDictionary.ContainsKey(extension)
                        && !(foundMatchingDefault =
                            _defaultDictionary[extension].AreTypeAndSubTypeEqual(contentType))))
                {
                    AddOverrideElement(partUri, contentType);
                }

                // Else, either there is already a mapping from extension to contentType,
                // or one needs to be created.
                else if (!foundMatchingDefault)
                {
                    AddDefaultElement(extension, contentType);
                }
            }


            //Returns the content type for the part, if present, else returns null.
            internal CompatiblePackage.ContentType? GetContentType(CompatiblePackage.PackUriHelper.ValidatedPartUri partUri)
            {
                //Step 1: Check if there is an override entry present corresponding to the
                //partUri provided. Override takes precedence over the default entries
                if (_overrideDictionary != null)
                {
                    if (_overrideDictionary.ContainsKey(partUri))
                        return _overrideDictionary[partUri];
                }

                //Step 2: Check if there is a default entry corresponding to the
                //extension of the partUri provided.
                string extension = partUri.PartUriExtension;

                if (_defaultDictionary.ContainsKey(extension))
                    return _defaultDictionary[extension];

                //Step 3: If we did not find an entry in the override and the default
                //dictionaries, this is an error condition
                return null;
            }

            //Deletes the override entry corresponding to the partUri, if it exists
            internal void DeleteContentType(CompatiblePackage.PackUriHelper.ValidatedPartUri partUri)
            {
                if (_overrideDictionary != null)
                {
                    if (_overrideDictionary.Remove(partUri))
                        _dirty = true;
                }
            }

            internal void SaveToFile()
            {
                if (_dirty)
                {
                    //Lazy init: Initialize when the first part is added.
                    if (!_contentTypeStreamExists)
                    {
                        _contentTypeZipArchiveEntry =
                            _zipArchive.CreateEntry(ContentTypesFile, _cachedCompressionLevel);
                        _contentTypeStreamExists = true;
                    }
                    else
                    {
                        // delete and re-create entry for content part.  When writing this, the stream will not truncate the content
                        // if the XML is shorter than the existing content part.
                        var contentTypefullName = _contentTypeZipArchiveEntry!.FullName;
                        var thisArchive = _contentTypeZipArchiveEntry.Archive;
                        _zipStreamManager.Close(_contentTypeZipArchiveEntry);
                        _contentTypeZipArchiveEntry.Delete();
                        _contentTypeZipArchiveEntry = thisArchive.CreateEntry(contentTypefullName);
                    }

                    using (Stream s = _zipStreamManager.Open(_contentTypeZipArchiveEntry, _packageFileMode,
                               FileAccess.ReadWrite))
                    {
                        // use UTF-8 encoding by default
                        using (XmlWriter writer = XmlWriter.Create(s,
                                   new XmlWriterSettings { Encoding = System.Text.Encoding.UTF8 }))
                        {
                            writer.WriteStartDocument();

                            // write root element tag - Types
                            writer.WriteStartElement(TypesTagName, TypesNamespaceUri);

                            // for each default entry
                            foreach (string key in _defaultDictionary.Keys)
                            {
                                WriteDefaultElement(writer, key, _defaultDictionary[key]);
                            }

                            if (_overrideDictionary != null)
                            {
                                // for each override entry
                                foreach (CompatiblePackage.PackUriHelper.ValidatedPartUri key in _overrideDictionary.Keys)
                                {
                                    WriteOverrideElement(writer, key, _overrideDictionary[key]);
                                }
                            }

                            // end of Types tag
                            writer.WriteEndElement();

                            // close the document
                            writer.WriteEndDocument();

                            _dirty = false;
                        }
                    }
                }
            }

            //[MemberNotNull(nameof(_overrideDictionary))]
            private void EnsureOverrideDictionary()
            {
                // The part Uris are stored in the Override Dictionary in their original form , but they are compared
                // in a normalized manner using the PartUriComparer
                if (_overrideDictionary == null)
                    _overrideDictionary =
                        new Dictionary<CompatiblePackage.PackUriHelper.ValidatedPartUri, CompatiblePackage.ContentType>(OverrideDictionaryInitialSize);
            }

            private void ParseContentTypesFile(
                System.Collections.ObjectModel.ReadOnlyCollection<ZipArchiveEntry> zipFiles)
            {
                // Find the content type stream, allowing for interleaving. Naming collisions
                // (as between an atomic and an interleaved part) will result in an exception being thrown.
                Stream? s = OpenContentTypeStream(zipFiles);

                // Allow non-existent content type stream.
                if (s == null)
                    return;

                XmlReaderSettings xrs = new XmlReaderSettings();
                xrs.IgnoreWhitespace = true;

                using (s)
                using (XmlReader reader = XmlReader.Create(s, xrs))
                {
                    //This method expects the reader to be in ReadState.Initial.
                    //It will make the first read call.
                    CompatiblePackage.PackagingUtilities.PerformInitialReadAndVerifyEncoding(reader);

                    //Note: After the previous method call the reader should be at the first tag in the markup.
                    //MoveToContent - Skips over the following - ProcessingInstruction, DocumentType, Comment, Whitespace, or SignificantWhitespace
                    //If the reader is currently at a content node then this function call is a no-op
                    reader.MoveToContent();

                    // look for our root tag and namespace pair - ignore others in case of version changes
                    // Make sure that the current node read is an Element
                    if ((reader.NodeType == XmlNodeType.Element)
                        && (reader.Depth == 0)
                        && (string.CompareOrdinal(reader.NamespaceURI, TypesNamespaceUri) == 0)
                        && (string.CompareOrdinal(reader.Name, TypesTagName) == 0))
                    {
                        //There should be a namespace Attribute present at this level.
                        //Also any other attribute on the <Types> tag is an error including xml: and xsi: attributes
                        if (CompatiblePackage.PackagingUtilities.GetNonXmlnsAttributeCount(reader) > 0)
                        {
                            throw new XmlException("SR.TypesTagHasExtraAttributes", null,
                                ((IXmlLineInfo) reader).LineNumber, ((IXmlLineInfo) reader).LinePosition);
                        }

                        // start tag encountered
                        // now parse individual Default and Override tags
                        while (reader.Read())
                        {
                            //Skips over the following - ProcessingInstruction, DocumentType, Comment, Whitespace, or SignificantWhitespace
                            //If the reader is currently at a content node then this function call is a no-op
                            reader.MoveToContent();

                            //If MoveToContent() takes us to the end of the content
                            if (reader.NodeType == XmlNodeType.None)
                                continue;

                            // Make sure that the current node read is an element
                            // Currently we expect the Default and Override Tag at Depth 1
                            if (reader.NodeType == XmlNodeType.Element
                                && reader.Depth == 1
                                && (string.CompareOrdinal(reader.NamespaceURI, TypesNamespaceUri) == 0)
                                && (string.CompareOrdinal(reader.Name, DefaultTagName) == 0))
                            {
                                ProcessDefaultTagAttributes(reader);
                            }
                            else if (reader.NodeType == XmlNodeType.Element
                                     && reader.Depth == 1
                                     && (string.CompareOrdinal(reader.NamespaceURI, TypesNamespaceUri) == 0)
                                     && (string.CompareOrdinal(reader.Name, OverrideTagName) == 0))
                            {
                                ProcessOverrideTagAttributes(reader);
                            }
                            else if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == 0 &&
                                     string.CompareOrdinal(reader.Name, TypesTagName) == 0)
                            {
                                continue;
                            }
                            else
                            {
                                throw new XmlException("SR.TypesXmlDoesNotMatchSchema", null,
                                    ((IXmlLineInfo) reader).LineNumber, ((IXmlLineInfo) reader).LinePosition);
                            }
                        }
                    }
                    else
                    {
                        throw new XmlException("SR.TypesElementExpected", null, ((IXmlLineInfo) reader).LineNumber,
                            ((IXmlLineInfo) reader).LinePosition);
                    }
                }
            }

            /// <summary>
            /// Find the content type stream, allowing for interleaving. Naming collisions
            /// (as between an atomic and an interleaved part) will result in an exception being thrown.
            /// Return null if no content type stream has been found.
            /// </summary>
            /// <remarks>
            /// The input array is lexicographically sorted
            /// </remarks>
            private Stream? OpenContentTypeStream(
                System.Collections.ObjectModel.ReadOnlyCollection<ZipArchiveEntry> zipFiles)
            {
                foreach (ZipArchiveEntry zipFileInfo in zipFiles)
                {
                    if (zipFileInfo.Name.ToUpperInvariant()
                        .StartsWith(ContentTypesFileUpperInvariant, StringComparison.Ordinal))
                    {
                        // Atomic name.
                        if (zipFileInfo.Name.Length == ContentTypeFileName.Length)
                        {
                            // Record the file info.
                            _contentTypeZipArchiveEntry = zipFileInfo;
                        }
                    }
                }


                // If an atomic file was found, open a stream on it.
                if (_contentTypeZipArchiveEntry != null)
                {
                    _contentTypeStreamExists = true;
                    return _zipStreamManager.Open(_contentTypeZipArchiveEntry, _packageFileMode, FileAccess.ReadWrite);
                }

                // No content type stream was found.
                return null;
            }

            // Process the attributes for the Default tag
            private void ProcessDefaultTagAttributes(XmlReader reader)
            {
                //There could be a namespace Attribute present at this level.
                //Also any other attribute on the <Default> tag is an error including xml: and xsi: attributes
                if (CompatiblePackage.PackagingUtilities.GetNonXmlnsAttributeCount(reader) != 2)
                    throw new XmlException("SR.DefaultTagDoesNotMatchSchema", null, ((IXmlLineInfo) reader).LineNumber,
                        ((IXmlLineInfo) reader).LinePosition);

                // get the required Extension and ContentType attributes

                string? extensionAttributeValue = reader.GetAttribute(ExtensionAttributeName);
                ValidateXmlAttribute(ExtensionAttributeName, extensionAttributeValue, DefaultTagName, reader);

                string? contentTypeAttributeValue = reader.GetAttribute(ContentTypeAttributeName);
                ThrowIfXmlAttributeMissing(ContentTypeAttributeName, contentTypeAttributeValue, DefaultTagName, reader);

                // The extensions are stored in the Default Dictionary in their original form , but they are compared
                // in a normalized manner using the ExtensionComparer.
                CompatiblePackage.PackUriHelper.ValidatedPartUri temporaryUri = CompatiblePackage.PackUriHelper.ValidatePartUri(
                    new Uri(TemporaryPartNameWithoutExtension + extensionAttributeValue, UriKind.Relative));
                _defaultDictionary.Add(temporaryUri.PartUriExtension, new CompatiblePackage.ContentType(contentTypeAttributeValue!));

                //Skip the EndElement for Default Tag
                if (!reader.IsEmptyElement)
                    ProcessEndElement(reader, DefaultTagName);
            }

            // Process the attributes for the Default tag
            private void ProcessOverrideTagAttributes(XmlReader reader)
            {
                //There could be a namespace Attribute present at this level.
                //Also any other attribute on the <Override> tag is an error including xml: and xsi: attributes
                if (CompatiblePackage.PackagingUtilities.GetNonXmlnsAttributeCount(reader) != 2)
                    throw new XmlException("SR.OverrideTagDoesNotMatchSchema", null, ((IXmlLineInfo) reader).LineNumber,
                        ((IXmlLineInfo) reader).LinePosition);

                // get the required Extension and ContentType attributes

                string? partNameAttributeValue = reader.GetAttribute(PartNameAttributeName);
                ValidateXmlAttribute(PartNameAttributeName, partNameAttributeValue, OverrideTagName, reader);

                string? contentTypeAttributeValue = reader.GetAttribute(ContentTypeAttributeName);
                ThrowIfXmlAttributeMissing(ContentTypeAttributeName, contentTypeAttributeValue, OverrideTagName,
                    reader);

                CompatiblePackage.PackUriHelper.ValidatedPartUri partUri =
                    CompatiblePackage.PackUriHelper.ValidatePartUri(new Uri(partNameAttributeValue!, UriKind.Relative));

                //Lazy initializing - ensure that the override dictionary has been initialized
                EnsureOverrideDictionary();

                // The part Uris are stored in the Override Dictionary in their original form , but they are compared
                // in a normalized manner using PartUriComparer.
                _overrideDictionary.Add(partUri, new CompatiblePackage.ContentType(contentTypeAttributeValue!));

                //Skip the EndElement for Override Tag
                if (!reader.IsEmptyElement)
                    ProcessEndElement(reader, OverrideTagName);
            }

            //If End element is present for Relationship then we process it
            private void ProcessEndElement(XmlReader reader, string elementName)
            {
                Debug.Assert(!reader.IsEmptyElement,
                    "This method should only be called it the Relationship Element is not empty");

                reader.Read();

                //Skips over the following - ProcessingInstruction, DocumentType, Comment, Whitespace, or SignificantWhitespace
                reader.MoveToContent();

                if (reader.NodeType == XmlNodeType.EndElement &&
                    string.CompareOrdinal(elementName, reader.LocalName) == 0)
                    return;
                else
                    throw new XmlException("SR.Format(SR.ElementIsNotEmptyElement, elementName)", null,
                        ((IXmlLineInfo) reader).LineNumber, ((IXmlLineInfo) reader).LinePosition);
            }

            private void AddOverrideElement(CompatiblePackage.PackUriHelper.ValidatedPartUri partUri, CompatiblePackage.ContentType contentType)
            {
                //Delete any entry corresponding in the Override dictionary
                //corresponding to the PartUri for which the contentType is being added.
                //This is to compensate for dead override entries in the content types file.
                DeleteContentType(partUri);

                //Lazy initializing - ensure that the override dictionary has been initialized
                EnsureOverrideDictionary();

                // The part Uris are stored in the Override Dictionary in their original form , but they are compared
                // in a normalized manner using PartUriComparer.
                _overrideDictionary.Add(partUri, contentType);
                _dirty = true;
            }

            private void AddDefaultElement(string extension, CompatiblePackage.ContentType contentType)
            {
                // The extensions are stored in the Default Dictionary in their original form , but they are compared
                // in a normalized manner using the ExtensionComparer.
                _defaultDictionary.Add(extension, contentType);

                _dirty = true;
            }

            private void WriteOverrideElement(XmlWriter xmlWriter, CompatiblePackage.PackUriHelper.ValidatedPartUri partUri,
                CompatiblePackage.ContentType contentType)
            {
                xmlWriter.WriteStartElement(OverrideTagName);
                xmlWriter.WriteAttributeString(PartNameAttributeName,
                    partUri.PartUriString);
                xmlWriter.WriteAttributeString(ContentTypeAttributeName, contentType.ToString());
                xmlWriter.WriteEndElement();
            }

            private void WriteDefaultElement(XmlWriter xmlWriter, string extension, CompatiblePackage.ContentType contentType)
            {
                xmlWriter.WriteStartElement(DefaultTagName);
                xmlWriter.WriteAttributeString(ExtensionAttributeName, extension);
                xmlWriter.WriteAttributeString(ContentTypeAttributeName, contentType.ToString());
                xmlWriter.WriteEndElement();
            }

            //Validate if the required XML attribute is present and not an empty string
            private void ValidateXmlAttribute(string attributeName, string? attributeValue, string tagName,
                XmlReader reader)
            {
                ThrowIfXmlAttributeMissing(attributeName, attributeValue, tagName, reader);

                //Checking for empty attribute
                if (attributeValue!.Length == 0)
                    throw new XmlException("SR.Format(SR.RequiredAttributeEmpty, tagName, attributeName)", null,
                        ((IXmlLineInfo) reader).LineNumber, ((IXmlLineInfo) reader).LinePosition);
            }


            //Validate if the required Content type XML attribute is present
            //Content type of a part can be empty
            private void ThrowIfXmlAttributeMissing(string attributeName, string? attributeValue, string tagName,
                XmlReader reader)
            {
                if (attributeValue == null)
                    throw new XmlException("SR.Format(SR.RequiredAttributeMissing, tagName, attributeName)", null,
                        ((IXmlLineInfo) reader).LineNumber, ((IXmlLineInfo) reader).LinePosition);
            }

            internal Dictionary<CompatiblePackage.PackUriHelper.ValidatedPartUri, CompatiblePackage.ContentType>?
                GetOverrideDictionary() => _overrideDictionary;

            internal Dictionary<string, CompatiblePackage.ContentType> GetDefaultDictionary() => _defaultDictionary;


            private Dictionary<CompatiblePackage.PackUriHelper.ValidatedPartUri, CompatiblePackage.ContentType>? _overrideDictionary;
            private readonly Dictionary<string, CompatiblePackage.ContentType> _defaultDictionary;
            private readonly ZipArchive _zipArchive;
            private readonly FileMode _packageFileMode;
            private readonly FileAccess _packageFileAccess;
            private readonly CompatiblePackage.ZipStreamManager _zipStreamManager;
            private ZipArchiveEntry? _contentTypeZipArchiveEntry;
            private bool _contentTypeStreamExists;
            private bool _dirty;
            private CompressionLevel _cachedCompressionLevel;
            private const string ContentTypesFile = "[Content_Types].xml";
            private const string ContentTypesFileUpperInvariant = "[CONTENT_TYPES].XML";
            private const int DefaultDictionaryInitialSize = 16;
            private const int OverrideDictionaryInitialSize = 8;

            //Xml tag specific strings for the Content Type file
            private const string TypesNamespaceUri = "http://schemas.openxmlformats.org/package/2006/content-types";
            private const string TypesTagName = "Types";
            private const string DefaultTagName = "Default";
            private const string ExtensionAttributeName = "Extension";
            private const string ContentTypeAttributeName = "ContentType";
            private const string OverrideTagName = "Override";
            private const string PartNameAttributeName = "PartName";
            private const string TemporaryPartNameWithoutExtension = "/tempfiles/sample.";
        }
    }
}
