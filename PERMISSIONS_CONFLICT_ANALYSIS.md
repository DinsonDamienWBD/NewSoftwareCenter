# Permissions vs Role Conflict Analysis

## Problem Statement

DataWarehouse has **two competing authorization systems** that use the same `Permission` enum but in incompatible ways:

1. **AuthenticationManager** (RBAC - Role-Based Access Control)
2. **ACLSecurityEngine** (ABAC - Attribute-Based Access Control / Resource-Based)

This creates confusion, inconsistency, and potential security issues.

---

## Current Implementation

### System 1: AuthenticationManager (RBAC)

**Location:** `Kernel/Security/AuthenticationManager.cs`

**Pattern:** Role-Based Access Control
- Users are assigned a **Role** (admin, power_user, user, readonly)
- Each Role contains a `List<Permission>` (e.g., admin has Read, Write, Delete, Admin, Security, Config)
- Authorization checks: "Does user's role have Permission.Read?"

**Code:**
```csharp
public class Role
{
    public required string RoleId { get; init; }
    public required string Name { get; init; }
    public required List<Permission> Permissions { get; init; }  // ← Coarse-grained
}

public class UserSession
{
    public required string RoleId { get; init; }
    public required List<Permission> Permissions { get; init; }  // ← Copied from role
}

public bool HasPermission(UserSession session, Permission requiredPermission)
{
    return session.Permissions.Contains(requiredPermission) ||
           session.Permissions.Contains(Permission.Admin);
}
```

**Permissions used:**
- `Permission.Read` - Can read any data
- `Permission.Write` - Can write any data
- `Permission.Delete` - Can delete any data
- `Permission.Admin` - Full system admin
- `Permission.Security` - Manage security
- `Permission.Config` - Change configuration

**Problem:** This is **coarse-grained**. If a user has `Permission.Read`, they can read EVERYTHING. No resource-level control.

---

### System 2: ACLSecurityEngine (ABAC/Resource-Based)

**Location:** `Plugins/Security.ACL/Engine/ACLSecurityEngine.cs`

**Pattern:** Attribute-Based / Resource-Based Access Control
- Permissions are granted on specific **resources** (e.g., "users/damien/docs")
- Each resource has a dictionary of `subject → (Allow, Deny)` permissions
- Hierarchical path expansion (permissions on parents apply to children)
- Authorization checks: "Does user 'damien' have Permission.Read on resource 'users/damien/docs'?"

**Code:**
```csharp
public class AclEntry
{
    public Permission Allow { get; set; }   // ← Fine-grained, per-resource
    public Permission Deny { get; set; }    // ← Explicit deny
}

public bool HasAccess(string resource, string subject, Permission requested)
{
    // Check hierarchical permissions on this specific resource
}
```

**Permissions used:**
- `Permission.Read` - Can read this specific resource
- `Permission.Write` - Can write this specific resource
- `Permission.Delete` - Can delete this specific resource
- `Permission.FullControl` - Full control over this specific resource

**Problem:** This is **fine-grained**. Permissions are per-resource, but there's no integration with AuthenticationManager's roles.

---

## The Conflict

### Same Permission Enum, Different Meanings

The `Permission` enum is used in **two incompatible ways**:

| Permission | AuthenticationManager (RBAC) | ACLSecurityEngine (ABAC) |
|------------|------------------------------|--------------------------|
| `Read` | "Can read **any** data in system" | "Can read **this specific resource**" |
| `Write` | "Can write **any** data in system" | "Can write **this specific resource**" |
| `Delete` | "Can delete **any** data in system" | "Can delete **this specific resource**" |
| `Admin` | "Full system administrator" | Not used (use `FullControl` instead) |
| `FullControl` | Not used | "Full control over **this specific resource**" |

### No Integration

**Problem:** The two systems don't work together!

```csharp
// In AuthenticationManager
if (!HasPermission(session, Permission.Write))
    return Unauthorized("No write permission");

// But this doesn't check ACLSecurityEngine!
// User might have global Write but ACL denies access to specific resource
```

**Result:** Security holes or overly restrictive access

---

## The Solution

### Architectural Decision

Use **BOTH systems together** in a **layered** approach:

1. **Authentication** (AuthenticationManager) - "Who are you?"
   - Username/password, API keys, JWT tokens
   - Creates a UserSession with identity

2. **Global Authorization** (AuthenticationManager Roles) - "What tier are you?"
   - Coarse-grained role check
   - Prevents unauthorized users from even attempting operations
   - Admin, PowerUser, User, ReadOnly

3. **Resource Authorization** (ACLSecurityEngine) - "Can you access this specific resource?"
   - Fine-grained resource-level permissions
   - Hierarchical path-based access control
   - Explicit Allow/Deny rules

### Proposed Changes

#### Change 1: Remove Permissions from Roles

Roles should be **identity groupings**, not permission containers.

**Before:**
```csharp
public class Role
{
    public required string RoleId { get; init; }
    public required string Name { get; init; }
    public required List<Permission> Permissions { get; init; };  // ❌ Remove this
}
```

**After:**
```csharp
public class Role
{
    public required string RoleId { get; init; }
    public required string Name { get; init; }
    public RoleTier Tier { get; init; }  // ✅ Coarse-grained tier only
}

public enum RoleTier
{
    ReadOnly = 0,   // Can only read (if ACL allows)
    User = 1,       // Can read/write (if ACL allows)
    PowerUser = 2,  // Can read/write/delete (if ACL allows)
    Admin = 3       // System administrator (bypasses ACLs)
}
```

#### Change 2: Integrate Authorization Flow

**New Authorization Flow:**

```csharp
public async Task<AuthorizationResult> AuthorizeAsync(
    UserSession session,
    string resource,
    Permission requestedPermission)
{
    // Step 1: Check role tier (coarse-grained gate)
    var roleTier = GetRoleTier(session.RoleId);

    // Admin bypasses all ACLs
    if (roleTier == RoleTier.Admin)
        return AuthorizationResult.Allowed("Admin access");

    // Check if role tier allows this operation at all
    if (!RoleTierAllowsOperation(roleTier, requestedPermission))
        return AuthorizationResult.Denied($"Role {session.RoleId} cannot perform {requestedPermission}");

    // Step 2: Check resource-level ACL (fine-grained check)
    if (!_aclEngine.HasAccess(resource, session.Username, requestedPermission))
        return AuthorizationResult.Denied($"ACL denies {requestedPermission} on {resource}");

    // Both checks passed
    return AuthorizationResult.Allowed();
}

private bool RoleTierAllowsOperation(RoleTier tier, Permission permission)
{
    return tier switch
    {
        RoleTier.ReadOnly => permission == Permission.Read,
        RoleTier.User => permission is Permission.Read or Permission.Write,
        RoleTier.PowerUser => permission is Permission.Read or Permission.Write or Permission.Delete,
        RoleTier.Admin => true,
        _ => false
    };
}
```

#### Change 3: Update Permission Enum Documentation

```csharp
/// <summary>
/// Resource-level permissions for fine-grained access control.
/// Used by ACLSecurityEngine for per-resource authorization.
/// </summary>
[Flags]
public enum Permission : long
{
    /// <summary>No permissions</summary>
    None = 0,

    /// <summary>Read access to a resource</summary>
    Read = 1,

    /// <summary>Write/modify access to a resource</summary>
    Write = 2,

    /// <summary>Delete access to a resource</summary>
    Delete = 4,

    /// <summary>Full control over a resource (all permissions)</summary>
    FullControl = Read | Write | Delete | 8,

    // System-level permissions (only for ACL management, not resources)

    /// <summary>Manage security and ACLs (system-level only)</summary>
    Security = 16,

    /// <summary>Modify system configuration (system-level only)</summary>
    Config = 32,

    /// <summary>System administrator (bypasses all ACLs)</summary>
    Admin = FullControl | Security | Config | 64
}
```

---

## Migration Plan

### Phase 1: Update AuthenticationManager (Low Risk)

1. Add `RoleTier` enum
2. Add `Tier` property to `Role` class
3. Keep `Permissions` list for backward compatibility (mark `[Obsolete]`)
4. Update role initialization to set tiers

**Estimated Time:** 1 hour

### Phase 2: Integrate ACLSecurityEngine (Medium Risk)

1. Add `ACLSecurityEngine` as dependency in `AuthenticationManager`
2. Update `AuthorizeAsync` to use layered authorization
3. Add `RoleTierAllowsOperation` helper

**Estimated Time:** 2 hours

### Phase 3: Update Call Sites (High Effort)

1. Find all `HasPermission` / `AuthorizeAsync` calls
2. Update to pass resource parameter
3. Add ACL entries for resources as needed

**Estimated Time:** 4-6 hours

### Phase 4: Remove Obsolete Code (Cleanup)

1. Remove `Permissions` list from `Role` after migration complete
2. Update documentation

**Estimated Time:** 1 hour

---

## Benefits

1. **Security:** Fine-grained resource-level access control
2. **Clarity:** Clear separation of authentication vs authorization
3. **Flexibility:** Can grant/deny access per-resource
4. **Consistency:** One authorization system, not two competing ones
5. **Audit:** ACL changes are logged and traceable

---

## Example Usage

**Before (Broken):**
```csharp
// Only checks role, not resource ACL
if (!authManager.HasPermission(session, Permission.Write))
    return Unauthorized();

await dataStore.WriteAsync(key, data);  // Might violate ACL!
```

**After (Correct):**
```csharp
// Checks both role tier AND resource ACL
var authResult = await authManager.AuthorizeAsync(
    session,
    resource: $"data/{key}",
    requestedPermission: Permission.Write
);

if (!authResult.IsAllowed)
    return Unauthorized(authResult.Reason);

await dataStore.WriteAsync(key, data);  // Safe - ACL checked
```

---

## Summary

- **Problem:** Two competing authorization systems using same Permission enum
- **Root Cause:** AuthenticationManager uses coarse RBAC, ACLSecurityEngine uses fine-grained ABAC
- **Solution:** Integrate both in layered approach (role tier → ACL check)
- **Benefit:** Proper security with fine-grained resource control
- **Effort:** ~8-10 hours to fully implement and test

The fix is straightforward architectural integration, not a rewrite!
