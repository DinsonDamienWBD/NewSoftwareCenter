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
            int bitmapBytes = (int)Math.Ceiling(totalBlocks / 8.0);
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
    }
}