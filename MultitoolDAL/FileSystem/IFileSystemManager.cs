﻿using Multitool.Data.FileSystem.Events;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Windows.Foundation;

namespace Multitool.Data.FileSystem
{
    /// <summary>
    /// Represents a object that can list the content of directories and cache them
    /// for improved performance.
    /// </summary>
    public interface IFileSystemManager : IProgressNotifier
    {
        /// <summary>
        /// Raised when the one or more items in the cache have changed.
        /// </summary>
        event TypedEventHandler<IFileSystemManager, ChangeEventArgs> Changed;
        /// <summary>
        /// Raised when a cached path is updating.
        /// </summary>
        event TypedEventHandler<IFileSystemManager, CacheUpdatingEventArgs> CacheUpdating;

        /// <summary>
        /// How often should the <see cref="IFileSystemManager"/> update each individual cache (1 cache 
        /// per path loaded).
        /// </summary>
        double CacheTimeout { get; set; }

        /// <summary>
        /// List the content of a directory as a <see cref="IList{T}"/>.
        /// Because each directory size is calculated, the task can be 
        /// cancelled with the <paramref name="cancellationToken"/>.
        /// </summary>
        /// <typeparam name="ItemType">Generic param of the <see cref="IList{T}"/></typeparam>
        /// <param name="path">System file path</param>
        /// <param name="cancellationToken">Cancellation token to cancel this method</param>
        /// <param name="list">Collection to add items to</param>
        /// <param name="addDelegate">Delegate to add items to the <paramref name="list"/></param>
        /// <exception cref="System.ArgumentNullException">
        /// If either <paramref name="list"/> or <paramref name="cancellationToken"/> is null/>
        /// </exception>
        Task GetEntries<ItemType>(
            string path, IList<ItemType> list,
            AddDelegate<ItemType> addDelegate, CancellationToken cancellationToken) where ItemType : IFileSystemEntry;
        /// <summary>
        /// Get the case sensitive path for the <paramref name="path"/> parameter.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>The "real" path</returns>
        string GetRealPath(string path);
        /// <summary>
        /// Cleans the internal cache.
        /// </summary>
        void Reset();
    }
}