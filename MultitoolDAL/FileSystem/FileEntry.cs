﻿using System;
using System.IO;
using System.Security.AccessControl;
using System.Threading.Tasks;

using Windows.Storage;

namespace Multitool.Data
{
    internal class FileEntry : FileSystemEntry
    {
        private FileInfo fileInfo;

        public FileEntry(FileInfo info) : base(info)
        {
            fileInfo = info;
            Partial = false;
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">The size cannot be set on a file.</exception>
        public override long Size
        {
            get => fileInfo.Length;
            set => throw new InvalidOperationException("Cannot set the size of a file, property relies on the actual file system infos.");
        }

        #region public methods
        /// <inheritdoc/>
        public override async Task<IStorageItem> AsIStorageItem()
        {
            return await StorageFile.GetFileFromPathAsync(Path);
        }

        /// <inheritdoc/>
        public override void Rename(string newName)
        {
            if (CanRename(newName))
            {
                throw new NotImplementedException();
            }
        }

        /// <inheritdoc/>
        public override void CopyTo(string newPath)
        {
            if (File.Exists(newPath))
            {
                fileInfo.CopyTo(newPath);
            }
        }

        /// <inheritdoc/>
        public override void Move(string newPath)
        {
            if (CanMove(newPath, out MoveCodes _))
            {
                fileInfo.MoveTo(newPath);
            }
            else
            {
                throw new IOException(Name + " cannot be moved to " + newPath);
            }
        }

        /// <inheritdoc/>
        public override void RefreshInfos()
        {
            string oldPath = Path;
            fileInfo.Refresh();
            if (!fileInfo.Exists)
            {
                fileInfo = new FileInfo(oldPath);
            }
            SetInfos(fileInfo);
        }

        /// <inheritdoc/>
        public override FileSystemSecurity GetAccessControl()
        {
            return fileInfo.GetAccessControl();
        }
        #endregion
    }
}
