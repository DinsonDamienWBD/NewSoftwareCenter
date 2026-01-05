using System;
using System.Collections.Generic;

namespace DataWarehouse.Plugins.Storage.LocalFileSystem.Engine
{
    /// <summary>
    /// Represents a contiguous range of blocks on the virtual disk.
    /// Files are composed of one or more Extents.
    /// </summary>
    public struct VdiExtent
    {
        /// <summary>
        /// The index of the starting block.
        /// </summary>
        public long StartBlock { get; set; }

        /// <summary>
        /// The number of blocks in this range.
        /// </summary>
        public int BlockCount { get; set; }
    }

    /// <summary>
    /// The metadata record for a stored file.
    /// Persisted in the VDI Index.
    /// </summary>
    public class VdiFileRecord
    {
        /// <summary>
        /// The exact file size in bytes.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// The list of block ranges that make up this file.
        /// </summary>
        public List<VdiExtent> Extents { get; set; } = [];

        /// <summary>
        /// The timestamp of creation (Ticks).
        /// </summary>
        public long CreatedAt { get; set; }
    }

    /// <summary>
    /// The binary header structure for the VDI file (Block 0).
    /// </summary>
    public struct VdiHeader
    {
        /// <summary>
        /// Magic Bytes "DW_VDI_1" (8 bytes)
        /// </summary>
        public long Magic;

        /// <summary>
        /// Version number (e.g., 1).
        /// </summary>
        public int Version;

        /// <summary>
        /// The size of a single block in bytes (e.g., 4096).
        /// </summary>
        public int BlockSize;

        /// <summary>
        /// The total number of blocks currently allocated in the file.
        /// </summary>
        public long TotalBlocks;

        /// <summary>
        /// The block index where the Allocation Bitmap starts.
        /// </summary>
        public long BitmapStartBlock;

        /// <summary>
        /// The number of blocks consumed by the Allocation Bitmap.
        /// </summary>
        public int BitmapBlockCount;
    }
}