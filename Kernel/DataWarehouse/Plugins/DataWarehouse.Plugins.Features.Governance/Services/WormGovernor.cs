using DataWarehouse.SDK.Utilities;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace DataWarehouse.Plugins.Features.Governance.Services
{
    /// <summary>
    /// The Compliance Officer. Enforces "Write Once, Read Many".
    /// Now backed by Disk. Compliance Locks persist forever.
    /// </summary>
    public class WormGovernor(string rootPath, byte[] systemSecret) : System.IDisposable
    {
        public class WormRecord
        {
            public long ExpiryTicks { get; set; }
            public string Signature { get; set; } = string.Empty;
        }

        private readonly DurableState<WormRecord> _store = new DurableState<WormRecord>(Path.Combine(rootPath, "worm_ledger.json"));
        private readonly HMACSHA256 _signer = new HMACSHA256(systemSecret);

        /// <summary>
        /// Lock a BLOB for write
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="retention"></param>
        public void LockBlob(string uri, TimeSpan retention)
        {
            long newExpiry = DateTime.UtcNow.Add(retention).Ticks;

            // 1. Calculate Signature
            string payload = $"{uri}|{newExpiry}";
            byte[] hash = _signer.ComputeHash(Encoding.UTF8.GetBytes(payload));
            string sig = Convert.ToBase64String(hash);

            // 2. Persist
            _store.Set(uri, new WormRecord { ExpiryTicks = newExpiry, Signature = sig });
        }

        /// <summary>
        /// Assert blob lock
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="isDelete"></param>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public void AssertAccess(string uri, bool isDelete)
        {
            if (!isDelete) return;

            if (_store.TryGet(uri, out var record))
            {
                // 1. Verify Integrity
                string payload = $"{uri}|{record!.ExpiryTicks}";
                byte[] hash = _signer.ComputeHash(Encoding.UTF8.GetBytes(payload));
                string computedSig = Convert.ToBase64String(hash);

                if (computedSig != record.Signature)
                {
                    throw new System.Security.SecurityException($"TAMPER DETECTED: WORM Lock for {uri} is invalid.");
                }

                // 2. Check Expiry
                if (DateTime.UtcNow.Ticks < record.ExpiryTicks)
                {
                    throw new UnauthorizedAccessException($"WORM LOCK: Blob protected until {new DateTime(record.ExpiryTicks)}.");
                }
            }
        }

        /// <summary>
        /// Safely dispose durablestate store
        /// </summary>
        public void Dispose()
        {
            _store.Dispose();
            _signer.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}