using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Security;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace DataWarehouse.Kernel.Security
{
    /// <summary>
    /// Manages authentication and authorization for DataWarehouse operations.
    /// Supports API keys, JWT tokens, and role-based access control (RBAC).
    /// </summary>
    public class AuthenticationManager
    {
        private readonly IKernelContext _context;
        private readonly ConcurrentDictionary<string, UserSession> _sessions = new();
        private readonly ConcurrentDictionary<string, ApiKey> _apiKeys = new();
        private readonly ConcurrentDictionary<string, UserAccount> _users = new();
        private readonly ConcurrentDictionary<string, Role> _roles = new();

        // Token configuration
        private readonly TimeSpan _sessionExpiration = TimeSpan.FromHours(24);
        private readonly TimeSpan _tokenExpiration = TimeSpan.FromHours(1);
        private readonly string _jwtSecret;

        public AuthenticationManager(IKernelContext context, string? jwtSecret = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _jwtSecret = jwtSecret ?? GenerateSecureSecret();
            InitializeDefaultRoles();
        }

        private void InitializeDefaultRoles()
        {
#pragma warning disable CS0618 // Permissions is obsolete but kept for backward compatibility
            // Admin role - full access
            _roles["admin"] = new Role
            {
                RoleId = "admin",
                Name = "Administrator",
                Tier = RoleTier.Admin,
                Permissions = [
                    Permission.Read,
                    Permission.Write,
                    Permission.Delete,
                    Permission.Admin,
                    Permission.Security,
                    Permission.Config
                ]
            };

            // Power user - read/write but no admin
            _roles["power_user"] = new Role
            {
                RoleId = "power_user",
                Name = "Power User",
                Tier = RoleTier.PowerUser,
                Permissions = [
                    Permission.Read,
                    Permission.Write,
                    Permission.Delete
                ]
            };

            // Standard user - read/write
            _roles["user"] = new Role
            {
                RoleId = "user",
                Name = "User",
                Tier = RoleTier.User,
                Permissions = [
                    Permission.Read,
                    Permission.Write
                ]
            };

            // Read-only user
            _roles["readonly"] = new Role
            {
                RoleId = "readonly",
                Name = "Read-Only",
                Tier = RoleTier.ReadOnly,
                Permissions = [Permission.Read]
            };
#pragma warning restore CS0618

            _context.LogInfo("[Auth] Initialized default roles");
        }

        /// <summary>
        /// Create a new user account.
        /// </summary>
        public async Task<UserAccount> CreateUserAsync(string username, string password, string roleId = "user")
        {
            if (_users.ContainsKey(username))
                throw new InvalidOperationException($"User '{username}' already exists");

            if (!_roles.ContainsKey(roleId))
                throw new ArgumentException($"Role '{roleId}' does not exist");

            var passwordHash = HashPassword(password);
            var user = new UserAccount
            {
                UserId = Guid.NewGuid().ToString(),
                Username = username,
                PasswordHash = passwordHash,
                RoleId = roleId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _users[username] = user;
            _context.LogInfo($"[Auth] Created user: {username} with role: {roleId}");

            await Task.CompletedTask;
            return user;
        }

        /// <summary>
        /// Authenticate user with username and password.
        /// </summary>
        public async Task<AuthenticationResult> AuthenticateAsync(string username, string password)
        {
            _context.LogDebug($"[Auth] Authentication attempt for: {username}");

            if (!_users.TryGetValue(username, out var user))
            {
                _context.LogWarning($"[Auth] User not found: {username}");
                return AuthenticationResult.Failed("Invalid credentials");
            }

            if (!user.IsActive)
            {
                _context.LogWarning($"[Auth] Inactive user attempted login: {username}");
                return AuthenticationResult.Failed("Account is inactive");
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                user.FailedLoginAttempts++;
                user.LastFailedLogin = DateTime.UtcNow;

                if (user.FailedLoginAttempts >= 5)
                {
                    user.IsActive = false;
                    _context.LogWarning($"[Auth] Account locked due to failed attempts: {username}");
                    return AuthenticationResult.Failed("Account locked due to failed login attempts");
                }

                return AuthenticationResult.Failed("Invalid credentials");
            }

            // Reset failed attempts on successful login
            user.FailedLoginAttempts = 0;
            user.LastLoginAt = DateTime.UtcNow;

            // Create session
            var session = CreateSession(user);
            _sessions[session.SessionId] = session;

            _context.LogInfo($"[Auth] User authenticated: {username}");

            await Task.CompletedTask;
            return AuthenticationResult.Success(session);
        }

        /// <summary>
        /// Validate API key authentication.
        /// </summary>
        public async Task<AuthenticationResult> AuthenticateApiKeyAsync(string apiKey)
        {
            if (!_apiKeys.TryGetValue(apiKey, out var key))
            {
                _context.LogWarning("[Auth] Invalid API key attempted");
                return AuthenticationResult.Failed("Invalid API key");
            }

            if (!key.IsActive)
            {
                _context.LogWarning($"[Auth] Inactive API key attempted: {key.Name}");
                return AuthenticationResult.Failed("API key is inactive");
            }

            if (key.ExpiresAt.HasValue && key.ExpiresAt.Value < DateTime.UtcNow)
            {
                _context.LogWarning($"[Auth] Expired API key attempted: {key.Name}");
                return AuthenticationResult.Failed("API key has expired");
            }

            key.LastUsedAt = DateTime.UtcNow;
            key.UsageCount++;

            // Create session from API key
#pragma warning disable CS0618 // Permissions is obsolete but still used for backward compatibility
            var session = new UserSession
            {
                SessionId = Guid.NewGuid().ToString(),
                UserId = key.OwnerId,
                Username = $"api:{key.Name}",
                RoleId = key.RoleId,
                Permissions = _roles[key.RoleId].Permissions,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow + _sessionExpiration,
                AuthMethod = AuthenticationMethod.ApiKey
            };
#pragma warning restore CS0618

            _context.LogDebug($"[Auth] API key authenticated: {key.Name}");

            await Task.CompletedTask;
            return AuthenticationResult.Success(session);
        }

        /// <summary>
        /// Validate session token.
        /// </summary>
        public async Task<UserSession?> ValidateSessionAsync(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return null;

            if (session.ExpiresAt < DateTime.UtcNow)
            {
                _sessions.TryRemove(sessionId, out _);
                _context.LogDebug($"[Auth] Session expired: {sessionId}");
                return null;
            }

            session.LastActivity = DateTime.UtcNow;
            await Task.CompletedTask;
            return session;
        }

        /// <summary>
        /// Check if user has required permission.
        /// </summary>
        [Obsolete("Use AuthorizeWithAclAsync instead")]
        public bool HasPermission(UserSession session, Permission requiredPermission)
        {
            return session.Permissions.Contains(requiredPermission) ||
                   session.Permissions.Contains(Permission.Admin);
        }

        /// <summary>
        /// Authorize an operation.
        /// </summary>
        [Obsolete("Use AuthorizeWithAclAsync instead")]
        public async Task<AuthorizationResult> AuthorizeAsync(
            UserSession session,
            Permission requiredPermission,
            string? resource = null)
        {
#pragma warning disable CS0618 // HasPermission is obsolete
            if (!HasPermission(session, requiredPermission))
#pragma warning restore CS0618
            {
                _context.LogWarning($"[Auth] Permission denied for {session.Username}: {requiredPermission}");
                return AuthorizationResult.Denied($"User lacks {requiredPermission} permission");
            }

            // Additional resource-level checks could be added here
            if (!string.IsNullOrEmpty(resource))
            {
                // Future: Check resource-specific ACLs
            }

            await Task.CompletedTask;
            return AuthorizationResult.Allowed();
        }

        /// <summary>
        /// Get the role tier for a given role ID.
        /// </summary>
        public RoleTier GetRoleTier(string roleId)
        {
            if (_roles.TryGetValue(roleId, out var role))
                return role.Tier;
            return RoleTier.ReadOnly;
        }

        /// <summary>
        /// Check if a role tier allows a specific permission.
        /// </summary>
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

        /// <summary>
        /// Authorize an operation using integrated role tier + ACL approach.
        /// This is the preferred authorization method going forward.
        /// </summary>
        /// <param name="session">The user session</param>
        /// <param name="resource">The resource being accessed (e.g., "data/users/damien")</param>
        /// <param name="requestedPermission">The permission being requested</param>
        /// <param name="aclEngine">Optional ACL engine for fine-grained checks (if null, only role tier is checked)</param>
        /// <returns>Authorization result indicating whether access is allowed</returns>
        public async Task<AuthorizationResult> AuthorizeWithAclAsync(
            UserSession session,
            string resource,
            Permission requestedPermission,
            IACLSecurityEngine? aclEngine = null)
        {
            // Step 1: Check role tier (coarse-grained gate)
            var roleTier = GetRoleTier(session.RoleId);

            // Admin bypasses all ACLs
            if (roleTier == RoleTier.Admin)
            {
                _context.LogDebug($"[Auth] Admin access granted for {session.Username} on {resource}");
                return AuthorizationResult.Allowed();
            }

            // Check if role tier allows this operation at all
            if (!RoleTierAllowsOperation(roleTier, requestedPermission))
            {
                _context.LogWarning($"[Auth] Role tier {roleTier} denies {requestedPermission} for {session.Username}");
                return AuthorizationResult.Denied($"Role {session.RoleId} cannot perform {requestedPermission}");
            }

            // Step 2: Check resource-level ACL (fine-grained check) if engine is provided
            if (aclEngine != null)
            {
                if (!aclEngine.HasAccess(resource, session.Username, requestedPermission))
                {
                    _context.LogWarning($"[Auth] ACL denies {requestedPermission} on {resource} for {session.Username}");
                    return AuthorizationResult.Denied($"ACL denies {requestedPermission} on {resource}");
                }
            }

            // Both checks passed
            _context.LogDebug($"[Auth] Access granted for {session.Username} on {resource} ({requestedPermission})");
            await Task.CompletedTask;
            return AuthorizationResult.Allowed();
        }

        /// <summary>
        /// Create API key for a user.
        /// </summary>
        public async Task<ApiKey> CreateApiKeyAsync(
            string ownerId,
            string name,
            string roleId,
            DateTime? expiresAt = null)
        {
            if (!_roles.ContainsKey(roleId))
                throw new ArgumentException($"Role '{roleId}' does not exist");

            var keyValue = GenerateApiKey();
            var apiKey = new ApiKey
            {
                KeyId = Guid.NewGuid().ToString(),
                KeyValue = keyValue,
                Name = name,
                OwnerId = ownerId,
                RoleId = roleId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsActive = true
            };

            _apiKeys[keyValue] = apiKey;
            _context.LogInfo($"[Auth] Created API key: {name} for user: {ownerId}");

            await Task.CompletedTask;
            return apiKey;
        }

        /// <summary>
        /// Revoke API key.
        /// </summary>
        public async Task RevokeApiKeyAsync(string keyValue)
        {
            if (_apiKeys.TryGetValue(keyValue, out var key))
            {
                key.IsActive = false;
                key.RevokedAt = DateTime.UtcNow;
                _context.LogInfo($"[Auth] Revoked API key: {key.Name}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Revoke user session (logout).
        /// </summary>
        public async Task RevokeSessionAsync(string sessionId)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                _context.LogInfo($"[Auth] Session revoked: {session.Username}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Create custom role with role tier.
        /// </summary>
        public async Task CreateRoleAsync(string roleId, string name, RoleTier tier)
        {
            if (_roles.ContainsKey(roleId))
                throw new InvalidOperationException($"Role '{roleId}' already exists");

            var role = new Role
            {
                RoleId = roleId,
                Name = name,
                Tier = tier
            };

            _roles[roleId] = role;
            _context.LogInfo($"[Auth] Created role: {name} with tier: {tier}");

            await Task.CompletedTask;
        }

        /// <summary>
        /// Create custom role with permissions list (obsolete).
        /// </summary>
        [Obsolete("Use CreateRoleAsync(roleId, name, RoleTier) instead")]
        public async Task CreateRoleAsync(string roleId, string name, List<Permission> permissions)
        {
            if (_roles.ContainsKey(roleId))
                throw new InvalidOperationException($"Role '{roleId}' already exists");

            // Map permissions to tier (best effort)
            var tier = RoleTier.ReadOnly;
            if (permissions.Contains(Permission.Admin))
                tier = RoleTier.Admin;
            else if (permissions.Contains(Permission.Delete))
                tier = RoleTier.PowerUser;
            else if (permissions.Contains(Permission.Write))
                tier = RoleTier.User;

#pragma warning disable CS0618 // Permissions is obsolete
            var role = new Role
            {
                RoleId = roleId,
                Name = name,
                Tier = tier,
                Permissions = permissions
            };
#pragma warning restore CS0618

            _roles[roleId] = role;
            _context.LogInfo($"[Auth] Created role: {name} with {permissions.Count} permissions (mapped to tier: {tier})");

            await Task.CompletedTask;
        }

        /// <summary>
        /// Get all active sessions.
        /// </summary>
        public List<UserSession> GetActiveSessions()
        {
            var now = DateTime.UtcNow;
            return _sessions.Values.Where(s => s.ExpiresAt > now).ToList();
        }

        /// <summary>
        /// Cleanup expired sessions.
        /// </summary>
        public async Task CleanupExpiredSessionsAsync()
        {
            var now = DateTime.UtcNow;
            var expired = _sessions.Where(kvp => kvp.Value.ExpiresAt < now)
                                  .Select(kvp => kvp.Key)
                                  .ToList();

            foreach (var sessionId in expired)
            {
                _sessions.TryRemove(sessionId, out _);
            }

            if (expired.Count > 0)
            {
                _context.LogInfo($"[Auth] Cleaned up {expired.Count} expired sessions");
            }

            await Task.CompletedTask;
        }

        private UserSession CreateSession(UserAccount user)
        {
            var role = _roles[user.RoleId];
#pragma warning disable CS0618 // Permissions is obsolete but still used for backward compatibility
            return new UserSession
            {
                SessionId = Guid.NewGuid().ToString(),
                UserId = user.UserId,
                Username = user.Username,
                RoleId = user.RoleId,
                Permissions = role.Permissions,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow + _sessionExpiration,
                AuthMethod = AuthenticationMethod.Password
            };
#pragma warning restore CS0618
        }

        private static string HashPassword(string password)
        {
            // Use PBKDF2 for password hashing
            var salt = RandomNumberGenerator.GetBytes(32);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations: 100000,
                HashAlgorithmName.SHA256,
                outputLength: 32
            );

            var combined = new byte[salt.Length + hash.Length];
            Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
            Buffer.BlockCopy(hash, 0, combined, salt.Length, hash.Length);

            return Convert.ToBase64String(combined);
        }

        private static bool VerifyPassword(string password, string hashedPassword)
        {
            var combined = Convert.FromBase64String(hashedPassword);
            var salt = new byte[32];
            var storedHash = new byte[32];

            Buffer.BlockCopy(combined, 0, salt, 0, 32);
            Buffer.BlockCopy(combined, 32, storedHash, 0, 32);

            var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations: 100000,
                HashAlgorithmName.SHA256,
                outputLength: 32
            );

            return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
        }

        private static string GenerateApiKey()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return $"dwk_{Convert.ToBase64String(bytes).Replace("/", "_").Replace("+", "-")}";
        }

        private static string GenerateSecureSecret()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }
    }

    public class UserAccount
    {
        public required string UserId { get; init; }
        public required string Username { get; init; }
        public required string PasswordHash { get; init; }
        public required string RoleId { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime? LastFailedLogin { get; set; }
        public int FailedLoginAttempts { get; set; }
        public bool IsActive { get; set; }
    }

    public class UserSession
    {
        public required string SessionId { get; init; }
        public required string UserId { get; init; }
        public required string Username { get; init; }
        public required string RoleId { get; init; }
        public required List<Permission> Permissions { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime ExpiresAt { get; init; }
        public DateTime LastActivity { get; set; }
        public AuthenticationMethod AuthMethod { get; init; }
    }

    public class ApiKey
    {
        public required string KeyId { get; init; }
        public required string KeyValue { get; init; }
        public required string Name { get; init; }
        public required string OwnerId { get; init; }
        public required string RoleId { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? ExpiresAt { get; init; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public bool IsActive { get; set; }
        public int UsageCount { get; set; }
    }

    public class Role
    {
        public required string RoleId { get; init; }
        public required string Name { get; init; }
        public required RoleTier Tier { get; init; }

        [Obsolete("Use RoleTier + ACLSecurityEngine instead")]
        public List<Permission>? Permissions { get; init; }
    }

    public enum AuthenticationMethod
    {
        Password,
        ApiKey,
        OAuth,
        Certificate
    }

    /// <summary>
    /// Coarse-grained role tiers for global authorization checks.
    /// </summary>
    public enum RoleTier
    {
        ReadOnly = 0,
        User = 1,
        PowerUser = 2,
        Admin = 3
    }

    public class AuthenticationResult
    {
        public bool IsSuccess { get; init; }
        public UserSession? Session { get; init; }
        public string? Error { get; init; }

        public static AuthenticationResult Success(UserSession session) =>
            new() { IsSuccess = true, Session = session };

        public static AuthenticationResult Failed(string error) =>
            new() { IsSuccess = false, Error = error };
    }

    public class AuthorizationResult
    {
        public bool IsAllowed { get; init; }
        public string? Reason { get; init; }

        public static AuthorizationResult Allowed() =>
            new() { IsAllowed = true };

        public static AuthorizationResult Denied(string reason) =>
            new() { IsAllowed = false, Reason = reason };
    }

    /// <summary>
    /// Interface for ACL security engine integration.
    /// This allows AuthenticationManager to perform fine-grained resource-level authorization.
    /// </summary>
    public interface IACLSecurityEngine
    {
        /// <summary>
        /// Check if a subject has access to a resource with the requested permission.
        /// </summary>
        /// <param name="resource">The resource path (e.g., "data/users/damien")</param>
        /// <param name="subject">The subject (usually username)</param>
        /// <param name="requestedPermission">The permission being requested</param>
        /// <returns>True if access is granted, false otherwise</returns>
        bool HasAccess(string resource, string subject, Permission requestedPermission);
    }
}
