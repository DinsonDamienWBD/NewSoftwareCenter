using DataWarehouse.SDK.AI.Math;
using System.Text.Json;

namespace DataWarehouse.CLI
{
    /// <summary>
    /// The 'dw-cli' tool.
    /// Usage: dw-cli repair <path-to-data-root>
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Data Warehouse Silver Tier CLI Tool");

            if (args.Length < 2)
            {
                Console.WriteLine("Usage: dw-cli [repair|inspect] <path>");
                return;
            }

            string command = args[0];
            string path = args[1];

            if (command == "inspect")
            {
                InspectVdi(path);
            }
            else
            {
                Console.WriteLine("Unknown command.");
            }
        }

        static void InspectVdi(string rootPath)
        {
            string vdiPath = Path.Combine(rootPath, "main.vdi");
            if (!File.Exists(vdiPath))
            {
                Console.WriteLine($"Error: No VDI found at {vdiPath}");
                return;
            }

            Console.WriteLine($"Inspecting VDI: {vdiPath}");
            using var fs = new FileStream(vdiPath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            // Read Header
            long magic = reader.ReadInt64();
            int version = reader.ReadInt32();
            int blockSize = reader.ReadInt32();
            long totalBlocks = reader.ReadInt64();
            long bitmapStart = reader.ReadInt64();
            int bitmapBlocks = reader.ReadInt32();

            Console.WriteLine($"[Header] Magic: {magic:X}");
            Console.WriteLine($"[Header] Version: {version}");
            Console.WriteLine($"[Header] BlockSize: {blockSize}");
            Console.WriteLine($"[Header] TotalBlocks: {totalBlocks} ({totalBlocks * blockSize / 1024 / 1024} MB)");
            Console.WriteLine($"[Header] AllocationMap: Start Block {bitmapStart}, Size {bitmapBlocks} Blocks");

            // Read Bitmap
            fs.Seek(bitmapStart * blockSize, SeekOrigin.Begin);
            int bitmapBytes = (int)MathUtils.Ceiling(totalBlocks / 8.0);
            byte[] bitmap = reader.ReadBytes(bitmapBytes);

            int usedBlocks = 0;
            for (int i = 0; i < bitmap.Length; i++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((bitmap[i] & (1 << bit)) != 0) usedBlocks++;
                }
            }

            Console.WriteLine($"[Usage] Used Blocks: {usedBlocks}");
            Console.WriteLine($"[Usage] Free Blocks: {totalBlocks - usedBlocks}");
            Console.WriteLine($"[Usage] Utilization: {(double)usedBlocks / totalBlocks * 100:F2}%");
        }

        static void RepairVdi(string rootPath)
        {
            string mapPath = Path.Combine(rootPath, "main.map");
            string indexPath = Path.Combine(rootPath, "vdi_metadata.json");

            if (!File.Exists(mapPath) || !File.Exists(indexPath))
            {
                Console.WriteLine("Error: Critical files missing.");
                return;
            }

            Console.WriteLine("Step 1: Loading Index...");
            var indexJson = File.ReadAllText(indexPath);
            // DTOs defined locally or imported from SDK
            var indexData = JsonSerializer.Deserialize<Dictionary<string, VdiFileRecord>>(indexJson);
            if (indexData == null) return;

            Console.WriteLine("Step 2: Loading Bitmap...");
            byte[] diskBitmap = File.ReadAllBytes(mapPath);
            byte[] computedBitmap = new byte[diskBitmap.Length];

            Console.WriteLine("Step 3: Recomputing Allocation...");
            long blocksMarked = 0;

            foreach (var file in indexData.Values)
            {
                foreach (var extent in file.Extents)
                {
                    for (long i = 0; i < extent.BlockCount; i++)
                    {
                        long blockAbs = extent.StartBlock + i;
                        SetBit(computedBitmap, blockAbs);
                        blocksMarked++;
                    }
                }
            }

            Console.WriteLine($"Step 4: Comparing... (Computed {blocksMarked} active blocks)");

            int fixedLeaks = 0;
            int fixedCorruption = 0;

            for (int i = 0; i < diskBitmap.Length; i++)
            {
                if (diskBitmap[i] == computedBitmap[i]) continue;

                // Iterate bits in byte
                for (int b = 0; b < 8; b++)
                {
                    bool diskSet = (diskBitmap[i] & (1 << b)) != 0;
                    bool compSet = (computedBitmap[i] & (1 << b)) != 0;

                    if (diskSet && !compSet)
                    {
                        // Leak: Disk says used, Index says free. Safe to free.
                        fixedLeaks++;
                        diskBitmap[i] &= (byte)~(1 << b);
                    }
                    else if (!diskSet && compSet)
                    {
                        // Corruption: Index expects data, Bitmap says empty.
                        // We MUST mark it used to prevent overwriting this data in future allocations.
                        fixedCorruption++;
                        diskBitmap[i] |= (byte)(1 << b);
                    }
                }
            }

            if (fixedLeaks > 0 || fixedCorruption > 0)
            {
                Console.WriteLine($"Found Issues: {fixedLeaks} Leaked Blocks freed, {fixedCorruption} Corrupt Blocks protected.");
                File.WriteAllBytes(mapPath, diskBitmap);
                Console.WriteLine("Success: Bitmap repaired and saved to disk.");
            }
            else
            {
                Console.WriteLine("Success: VDI structure is healthy. No changes needed.");
            }
        }

        // Bitwise Helper
        static void SetBit(byte[] map, long blockIndex)
        {
            long byteIdx = blockIndex / 8;
            int bitIdx = (int)(blockIndex % 8);
            if (byteIdx < map.Length)
            {
                map[byteIdx] |= (byte)(1 << bitIdx);
            }
        }

        // Minimal DTOs for JSON deserialization
        public class VdiFileRecord
        {
            public List<VdiExtent> Extents { get; set; } = [];
        }
        public class VdiExtent
        {
            public long StartBlock { get; set; }
            public long BlockCount { get; set; }
        }
    }
}