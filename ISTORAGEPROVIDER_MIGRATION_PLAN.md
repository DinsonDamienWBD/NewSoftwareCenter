# IStorageProvider Migration Plan - 90 References

## Problem Statement

We marked `IStorageProvider` as `[Obsolete]` but there are **90 active references** throughout the codebase. This creates:
- Build warnings (obsolete usage)
- Confusion about which abstraction to use
- Mixed architecture (old interfaces + new CategoryBase)

## Migration Strategy

Each reference must be migrated to one of:
1. **`StorageProviderBase`** - For full storage plugins
2. **`IStorageBackend`** - For internal utilities and infrastructure
3. **Removed** - If the class itself is obsolete

---

## Analysis of 90 References

### Category 1: Internal Infrastructure (Use IStorageBackend)

These are internal components that only need basic storage operations, not full plugin functionality:

#### 1.1 RAID Engine (62 references!) 🔥 HIGH PRIORITY
**File:** `Kernel/Storage/RaidEngine.cs`
**Current:** Uses `Func<int, IStorageProvider>`
**Migration:** Change to `Func<int, IStorageBackend>`
**Effort:** Medium (many methods but mechanical change)
**Rationale:**
- RaidEngine is internal infrastructure
- Only uses: SaveAsync, LoadAsync, DeleteAsync, ExistsAsync
- Doesn't need plugin lifecycle, handshake, metadata

#### 1.2 Storage Pool Manager (9 references)
**File:** `Kernel/Storage/StoragePoolManager.cs`
**Current:** `class StoragePoolManager : IStorageProvider`
**Migration:** Implement `IStorageBackend` instead
**Effort:** Low
**Rationale:**
- Internal component managing pool of storage providers
- Acts as coordinator, not a full plugin

#### 1.3 In-Memory Storage Provider (1 reference)
**File:** `Kernel/IO/InMemoryStorageProvider.cs`
**Current:** `class InMemoryStorageProvider : IStorageProvider`
**Migration:** Implement `IStorageBackend` instead
**Effort:** Low
**Rationale:**
- Testing/temporary storage utility
- No need for plugin infrastructure

#### 1.4 Local Disk Provider (1 reference)
**File:** `Kernel/IO/LocalDiskProvider.cs`
**Current:** `class LocalDiskProvider(string rootPath) : IStorageProvider`
**Migration:** Implement `IStorageBackend` instead
**Effort:** Low
**Rationale:**
- Internal utility similar to SimpleLocalStorageProvider
- Lightweight local file operations

#### 1.5 External Backup Provider (9 references)
**File:** `Kernel/Backup/ExternalBackupProvider.cs`
**Migration:** Use `IStorageBackend` instead of `IStorageProvider`
**Effort:** Low
**Rationale:**
- Backup infrastructure component
- Only needs basic storage operations

#### 1.6 Snapshot Manager (2 references)
**File:** `Kernel/Backup/SnapshotManager.cs`
**Migration:** Use `IStorageBackend` instead of `IStorageProvider`
**Effort:** Low

#### 1.7 DataWarehouse.cs (2 references)
**File:** `Kernel/Engine/DataWarehouse.cs`
**Migration:** Use `IStorageBackend` for internal operations
**Effort:** Low

---

### Category 2: Storage Plugins (Extend StorageProviderBase)

These should be full-fledged storage plugins:

#### 2.1 RAMDisk Storage Engine ⚠️ CRITICAL
**File:** `Plugins/Storage.RAMDisk/Engine/RAMDiskStorageEngine.cs`
**Current:** `class RAMDiskStorageEngine : IStorageProvider`
**Migration:** Extend `StorageProviderBase` instead
**Effort:** **HIGH** (from PLUGIN_AUDIT_REPORT - complex plugin)
**Rationale:**
- This is a full storage plugin, not infrastructure
- Should have AI metadata, capability relationships
- Already identified in Task 4 audit

#### 2.2 Network Storage Provider (2 instances)
**Files:**
- `Plugins/Interface.gRPC/Engine/NetworkStorageProvider.cs`
- `Plugins/Features.EnterpriseStorage/Engine/NetworkStorageProvider.cs`

**Current:** `class NetworkStorageProvider : IStorageProvider`
**Migration:** Extend `StorageProviderBase`
**Effort:** Medium
**Rationale:**
- Provides network-based storage capability
- Full plugin with lifecycle management

#### 2.3 Segmented Disk Provider
**File:** `Plugins/Features.EnterpriseStorage/Engine/SegmentedDiskProvider.cs`
**Current:** `class SegmentedDiskProvider : IStorageProvider`
**Migration:** Extend `StorageProviderBase`
**Effort:** Medium

#### 2.4 Self-Healing Mirror
**File:** `Plugins/Features.EnterpriseStorage/Engine/SelfHealingMirror.cs`
**Current:** `class SelfHealingMirror(IStorageProvider primary, IStorageProvider secondary, ILogger logger) : IStorageProvider`
**Migration:**
- Constructor params: Change to `IStorageBackend`
- Class: Extend `StorageProviderBase`
**Effort:** Medium
**Rationale:**
- Coordinates two backends (use IStorageBackend for constructor)
- Provides full storage plugin functionality (extend StorageProviderBase)

---

### Category 3: Marker/Composite Interfaces (Already Obsolete)

#### 3.1 IStoragePlugin
**File:** `SDK/Contracts/ProviderInterfaces.cs`
**Current:** `public interface IStoragePlugin : IStorageProvider, IFeaturePlugin`
**Status:** Already marked `[Obsolete]`
**Action:** Keep as-is (deprecation path documented)

---

## Migration Priority

### Phase 1: High-Impact Infrastructure (Removes ~75 references)
1. **RaidEngine.cs** (62 refs) - Change `Func<int, IStorageProvider>` → `Func<int, IStorageBackend>`
2. **StoragePoolManager.cs** (9 refs) - Implement `IStorageBackend`
3. **ExternalBackupProvider.cs** (9 refs) - Use `IStorageBackend`

**Estimated Time:** 4-6 hours
**Impact:** Removes 80% of obsolete references

### Phase 2: Internal Utilities (Removes ~15 references)
4. InMemoryStorageProvider.cs
5. LocalDiskProvider.cs
6. SnapshotManager.cs
7. DataWarehouse.cs

**Estimated Time:** 2-3 hours
**Impact:** All internal code clean

### Phase 3: Plugin Migration (Task 4 continuation)
8. RAMDiskStorageEngine → StorageProviderBase (HIGH COMPLEXITY)
9. NetworkStorageProvider (2 files) → StorageProviderBase
10. SegmentedDiskProvider → StorageProviderBase
11. SelfHealingMirror → StorageProviderBase

**Estimated Time:** 8-12 hours
**Impact:** Completes CategoryBase migration

---

## Decision Matrix

| Component | Current | Migrate To | Reason |
|-----------|---------|------------|--------|
| RaidEngine | `Func<int, IStorageProvider>` | `Func<int, IStorageBackend>` | Internal infrastructure |
| StoragePoolManager | `: IStorageProvider` | `: IStorageBackend` | Internal coordinator |
| InMemoryStorageProvider | `: IStorageProvider` | `: IStorageBackend` | Testing utility |
| LocalDiskProvider | `: IStorageProvider` | `: IStorageBackend` | Simple file ops |
| ExternalBackupProvider | Uses `IStorageProvider` | Uses `IStorageBackend` | Backup infrastructure |
| RAMDiskStorageEngine | `: IStorageProvider` | `: StorageProviderBase` | **Full plugin** |
| NetworkStorageProvider | `: IStorageProvider` | `: StorageProviderBase` | **Full plugin** |
| SegmentedDiskProvider | `: IStorageProvider` | `: StorageProviderBase` | **Full plugin** |
| SelfHealingMirror | `: IStorageProvider` | `: StorageProviderBase` | **Full plugin** |

---

## Architecture Principle

**The Dividing Line:**

```
┌─────────────────────────────────────────┐
│     User-Facing Plugins                 │
│  → Extend StorageProviderBase           │
│  → Have AI metadata                     │
│  → Registered in plugin system          │
│  → Examples: S3Storage, LocalStorage    │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│     Internal Infrastructure             │
│  → Implement IStorageBackend            │
│  → No plugin overhead                   │
│  → Simple storage operations only       │
│  → Examples: RaidEngine, PoolManager    │
└─────────────────────────────────────────┘
```

---

## Immediate Action

Start with **Phase 1** (RaidEngine, StoragePoolManager, ExternalBackupProvider):
- Highest impact (80% of references)
- Mechanical changes
- Clear architectural benefit
- Removes most build warnings

Then proceed to Phase 2 and 3 systematically.

---

## Summary

- **90 references** to obsolete `IStorageProvider`
- **62 references** in RaidEngine alone (migration priority #1)
- **Clear migration path**: Internal → `IStorageBackend`, Plugins → `StorageProviderBase`
- **Estimated total effort**: 14-21 hours
- **Benefit**: Clean architecture, no obsolete warnings, CategoryBase pattern complete

This should be completed before declaring production-ready status.
