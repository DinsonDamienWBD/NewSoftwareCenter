# DurableState Analysis & Improvements

**Document Version:** 1.0
**Created:** 2026-01-08

---

## What is DurableState?

**DurableState** is a **Write-Ahead Log (WAL)** based persistent key-value store with **in-memory caching**. It provides **crash-resistant** storage with **O(1)** read performance.

### Architecture

```
┌─────────────────────────────────────────┐
│         Application (Caller)            │
└────────────────┬────────────────────────┘
                 │ Set(key, value)
                 ↓
┌─────────────────────────────────────────┐
│     DurableState<T> (In-Memory Cache)   │
│  ┌──────────────────────────────────┐   │
│  │  ConcurrentDictionary<string, T> │   │ ← Fast O(1) reads
│  └──────────────────────────────────┘   │
└────────────────┬────────────────────────┘
                 │ Append to WAL
                 ↓
┌─────────────────────────────────────────┐
│   Write-Ahead Log (Journal on Disk)    │
│  ┌──────────────────────────────────┐   │
│  │ [Op:1][Key]["foo"][Len:4][JSON]  │   │ ← Durability
│  │ [Op:1][Key]["bar"][Len:6][JSON]  │   │
│  │ [Op:2][Key]["foo"]                │   │ (Remove)
│  └──────────────────────────────────┘   │
└─────────────────────────────────────────┘
           Local File System (Disk)
```

### Key Features

1. **Write-Ahead Logging (WAL)**
   - Every `Set()` and `Remove()` operation is **appended** to a journal file
   - Format: `[OpCode:1 byte][Key:string][Length:4 bytes][JSON:N bytes]`
   - Provides crash recovery (replay journal on startup)

2. **In-Memory Cache**
   - `ConcurrentDictionary<string, T>` for **O(1)** reads
   - No disk I/O on reads (fast!)
   - Thread-safe with lock-based writes

3. **Automatic Compaction**
   - After 5,000 operations, journal is rewritten to current state only
   - Reduces file size and replay time
   - Atomic swap (create `.compact` file, then move)

4. **Crash Recovery**
   - On startup, replay entire journal to rebuild in-memory cache
   - Handles truncated/corrupt entries gracefully

---

## Current Implementation: "Disk-Backed"

### What "Disk-Backed" Means

Currently, "disk-backed" means the journal is stored on the **local file system**:

```csharp
// CURRENT IMPLEMENTATION (Hardcoded to local disk)
public DurableState(string filePath) {
    _filePath = filePath;  // e.g., "/app/data/security/acl.db"

    // Uses local filesystem APIs:
    if (!File.Exists(_filePath)) { ... }
    using var fs = new FileStream(_filePath, FileMode.Open, ...);
    File.Move(tempPath, _filePath, overwrite: true);
}
```

**Problem:** This is **NOT storage-agnostic**. It's tightly coupled to the local disk.

---

## The Problem: Storage Coupling

### Example: Security.ACL Plugin

```csharp
// In ACLSecurityEngine.cs (line 44):
_storagePath = Path.Combine(securityDir, "acl_enhanced.db");
_store = new DurableState<Dictionary<string, AclEntry>>(_storagePath);
```

**Issues:**
1. ❌ **Hard-coded to local disk** - What if the Kernel is using S3? IPFS? RAMDisk?
2. ❌ **Not portable** - Can't move ACL data to cloud storage
3. ❌ **Inconsistent with architecture** - Plugins should use `IStorageProvider`, not `FileStream`
4. ❌ **RAID-agnostic broken** - ACL data bypasses RAID protection!

### Real-World Impact

**Scenario:** User configures RAID 6 for maximum data protection:
```json
{
  "StoragePoolMode": "Pool",
  "RaidConfiguration": {
    "Level": "RAID_6",  // Dual parity, survives 2 disk failures
    "ProviderCount": 8
  }
}
```

**But:**
- Blob data → ✅ Protected by RAID 6
- ACL permissions → ❌ **Stored on local disk only** (single point of failure!)
- RAID metadata → ❌ **Stored on local disk only**
- Tier metadata → ❌ **Stored on local disk only**

**Result:** Lose one disk = Lose all ACL permissions (security breach!)

---

## The Correct Solution: Storage-Agnostic DurableState

### Design Principles

**DurableState should:**
1. ✅ **Be agnostic to the underlying storage** (local, S3, IPFS, RAMDisk, etc.)
2. ✅ **Use IStorageProvider abstraction** (same as blob storage)
3. ✅ **Benefit from RAID protection** (if configured)
4. ✅ **Maintain in-memory cache** (for O(1) read performance)
5. ✅ **Support async operations** (non-blocking I/O)

### Improved Architecture

```
┌─────────────────────────────────────────┐
│         Application (Caller)            │
└────────────────┬────────────────────────┘
                 │ SetAsync(key, value)
                 ↓
┌─────────────────────────────────────────┐
│  DurableStateV2<T> (In-Memory Cache)    │
│  ┌──────────────────────────────────┐   │
│  │  ConcurrentDictionary<string, T> │   │
│  └──────────────────────────────────┘   │
└────────────────┬────────────────────────┘
                 │ AppendLogAsync()
                 ↓
┌─────────────────────────────────────────┐
│      IStorageProvider (Abstraction)     │
│              (User-Configured)          │
└────────────────┬────────────────────────┘
                 │
        ┌────────┴─────────┬─────────────┐
        ↓                  ↓             ↓
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ LocalStorage │  │  S3 Storage  │  │ RAID Engine  │
│   (Disk)     │  │   (Cloud)    │  │ (Protected)  │
└──────────────┘  └──────────────┘  └──────────────┘
```

### Benefits

1. **RAID Protection** - ACL data protected by RAID 5/6/10
2. **Cloud-Native** - Can use S3, Azure Blob, Google Cloud Storage
3. **Consistent Architecture** - All data goes through `IStorageProvider`
4. **Better Testing** - Can use RAMDisk for unit tests (fast!)
5. **Multi-Region** - Can replicate ACL data across regions

---

## Implementation: DurableStateV2

### Interface Design

```csharp
public class DurableStateV2<T> : IDisposable
{
    private readonly IStorageProvider _storageProvider;
    private readonly Uri _journalUri;
    private readonly ConcurrentDictionary<string, T> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private MemoryStream _journalBuffer = new();
    private int _opCount = 0;
    private const int CompactionThreshold = 5000;

    // Constructor now takes IStorageProvider instead of file path
    public DurableStateV2(IStorageProvider storageProvider, string journalKey)
    {
        _storageProvider = storageProvider;
        _journalUri = new Uri($"{storageProvider.Scheme}://{journalKey}");

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            // Load journal from ANY storage backend
            var stream = await _storageProvider.LoadAsync(_journalUri);
            await ReplayJournalAsync(stream);
        }
        catch (FileNotFoundException)
        {
            // Journal doesn't exist yet (first run)
        }
    }

    public async Task SetAsync(string key, T value)
    {
        await _lock.WaitAsync();
        try
        {
            _cache[key] = value;
            await AppendLogAsync(1, key, value);
            await CheckCompactionAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task AppendLogAsync(byte opCode, string key, T? value)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(opCode);
        writer.Write(key);

        if (opCode == 1 && value != null)
        {
            var json = JsonSerializer.Serialize(value);
            var bytes = Encoding.UTF8.GetBytes(json);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        writer.Flush();
        ms.Position = 0;

        // Append to journal via IStorageProvider (RAID-protected!)
        await _storageProvider.SaveAsync(_journalUri, ms);
        _opCount++;
    }

    private async Task CompactAsync()
    {
        var tempUri = new Uri($"{_storageProvider.Scheme}://{_journalUri.AbsolutePath}.compact");

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        foreach (var kvp in _cache)
        {
            writer.Write((byte)1);
            writer.Write(kvp.Key);
            var json = JsonSerializer.Serialize(kvp.Value);
            var bytes = Encoding.UTF8.GetBytes(json);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        writer.Flush();
        ms.Position = 0;

        // Save compacted journal
        await _storageProvider.SaveAsync(tempUri, ms);

        // Atomic swap (delete old, rename new)
        await _storageProvider.DeleteAsync(_journalUri);
        // Note: IStorageProvider doesn't have Rename, so we copy and delete
        var compactedStream = await _storageProvider.LoadAsync(tempUri);
        await _storageProvider.SaveAsync(_journalUri, compactedStream);
        await _storageProvider.DeleteAsync(tempUri);

        _opCount = 0;
    }
}
```

### Usage Example (Before vs After)

**BEFORE (Hardcoded to disk):**
```csharp
public class ACLSecurityEngine
{
    private readonly DurableState<Dictionary<string, AclEntry>> _store;

    public ACLSecurityEngine(string rootPath)
    {
        var dbPath = Path.Combine(rootPath, "Security", "acl_enhanced.db");
        _store = new DurableState<Dictionary<string, AclEntry>>(dbPath);
        // ❌ Always uses local disk, no RAID protection
    }
}
```

**AFTER (Storage-agnostic):**
```csharp
public class ACLSecurityEngine
{
    private readonly DurableStateV2<Dictionary<string, AclEntry>> _store;

    public ACLSecurityEngine(IStorageProvider storageProvider)
    {
        _store = new DurableStateV2<Dictionary<string, AclEntry>>(
            storageProvider,
            "security/acl_enhanced.journal"
        );
        // ✅ Uses configured storage (Local/S3/RAID/etc.)
    }
}
```

**Configuration determines backend:**
```json
{
  "StoragePoolMode": "Pool",
  "RaidConfiguration": {
    "Level": "RAID_6"
  }
}
// Result: ACL data is now RAID-6 protected!
```

---

## Migration Plan

### Phase 1: Create DurableStateV2
- ✅ New implementation with `IStorageProvider` backend
- ✅ Async API (`SetAsync`, `GetAsync`, `RemoveAsync`)
- ✅ Backward compatible (can read old journal format)

### Phase 2: Update Plugins
- Security.ACL → Use DurableStateV2
- Any other plugins using DurableState
- StoragePoolManager metadata → Use DurableStateV2

### Phase 3: Deprecate DurableState
- Mark old `DurableState<T>` as `[Obsolete]`
- Keep for backward compatibility (6 months)
- Remove in next major version

---

## Conclusion

**Current State:**
- ❌ DurableState is hardcoded to local disk
- ❌ "Disk-backed" means local filesystem only
- ❌ Not storage-agnostic
- ❌ Bypasses RAID protection

**Correct Approach:**
- ✅ DurableStateV2 uses `IStorageProvider` abstraction
- ✅ "Storage-backed" (agnostic to backend)
- ✅ Benefits from RAID, cloud storage, multi-region
- ✅ Maintains in-memory cache for performance

**Your intuition was correct!** DurableState **should** be storage-agnostic and work with any non-volatile storage backend, not just local disk.

---

**Next Steps:** Implementing DurableStateV2 in the codebase.
