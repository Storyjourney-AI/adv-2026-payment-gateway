using Google.Apis.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PaymentGateway.Server.Authorization.Models.Dbs;
using PaymentGateway.Server.Authorization.Models.Dtos;
using PaymentGateway.Server.Authorization.Utils;
using PaymentGateway.Server.Common.Models;
using PaymentGateway.Server.Databases;
using System.Data;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using PaymentGateway.Server.Authorization.Models;

namespace PaymentGateway.Server.Authorization.Services
{
    public class AuthService
    {
        private RoleManager<Db_ApplicationRole> m_roleManager;
        private UserManager<Db_ApplicationUser> m_userManager;
        private AppDbContext m_authDbContext;
        private ILogger<AuthService> Logger;
        private readonly IHttpContextAccessor m_httpContextAccessor;

        private readonly string m_jwt_key = null;
        private readonly string m_jwt_issuer = null;
        private readonly string m_jwt_audience = null;
        private readonly int m_jwt_accessTokenExpiryMinutes = 15;
        private readonly int m_jwt_refreshTokenExpiryDays = 7;

        public AuthService(
            RoleManager<Db_ApplicationRole> roleManager,
            UserManager<Db_ApplicationUser> userManager,
            ILogger<AuthService> logger,
            AppDbContext authDbContext,
            IConfiguration config,
            IHttpContextAccessor httpContextAccessor)
        {
            m_roleManager = roleManager;
            m_userManager = userManager;
            m_authDbContext = authDbContext;
            Logger = logger;
            m_httpContextAccessor = httpContextAccessor;

            m_jwt_key = config["Jwt:Key"] ?? throw new NullReferenceException("Jwt Key does not exist");
            m_jwt_issuer = config["Jwt:Issuer"] ?? throw new NullReferenceException("Jwt Issuer does not exist");
            m_jwt_audience = config["Jwt:Audience"] ?? throw new NullReferenceException("Jwt Audience does not exist");
            var accessTokenExpiryMinutes = config["Jwt:AccessTokenExpiryMinutes"] ?? throw new NullReferenceException("Jwt AccessTokenExpiryMinutes does not exist");
            var refreshTokenExpiryDays = config["Jwt:RefreshTokenExpiryDays"] ?? throw new NullReferenceException("Jwt RefreshTokenExpiryDays does not exist");
            
            m_jwt_accessTokenExpiryMinutes = int.Parse(accessTokenExpiryMinutes);
            m_jwt_refreshTokenExpiryDays = int.Parse(refreshTokenExpiryDays);

            Logger.LogInformation($"Auth Service Configured - Issuer: {m_jwt_issuer}, Audience: {m_jwt_audience}, AccessTokenExpiry: {m_jwt_accessTokenExpiryMinutes}min, RefreshTokenExpiry: {m_jwt_refreshTokenExpiryDays}days");
        }

        #region REWRITEN LOGICS
        public async Task<DataWrapper<Dto_Register>> RegisterAsync(Dto_RegisterRequest registerData)
        {
            try
            {
                var email = registerData.Email.ToLower();

                // Check if user already exists
                var existingUser = await m_userManager.FindByEmailAsync(email);
                if (existingUser != null)
                {
                    return DataWrapper<Dto_Register>.Conflict(message: "Email already registered. Please use a different email or try logging in.");
                }

                // Check if this is the first user
                var userCount = await m_userManager.Users.CountAsync();
                var isFirstUser = userCount == 0;

                // Create the new user
                var user = new Db_ApplicationUser()
                {
                    Email = email,
                    UserName = email,
                    RegisterAt = DateTime.UtcNow,
                    EmailConfirmed = false,
                    IsActive = true
                };

                var createResult = await m_userManager.CreateAsync(user, registerData.Password);
                if (!createResult.Succeeded)
                {
                    List<string> errors = new List<string>();
                    foreach (var error in createResult.Errors)
                    {
                        errors.Add($"[{error.Code}] {error.Description}");
                    }
                    Logger.LogWarning("Register - User Creation Failed: {email}. Errors: {errors}", email, string.Join(", ", errors));
                    return DataWrapper<Dto_Register>.BadRequest(message: "Failed to create user account.", errors: errors);
                }

                // Assign roles based on whether this is the first user
                if (isFirstUser)
                {
                    // First user gets both Super Admin and User roles
                    Logger.LogInformation("Register - First user detected. Assigning Super Admin role to: {email}", email);
                    
                    var superAdminRoleResult = await m_userManager.AddToRoleAsync(user, "Super Admin");
                    if (!superAdminRoleResult.Succeeded)
                    {
                        Logger.LogWarning("Register - Super Admin Role Assignment Failed: {email}", email);
                    }
                }
                
                // All users get the default User role
                bool userRoleExists = await m_roleManager.RoleExistsAsync("User");
                if (!userRoleExists)
                {
                    await m_roleManager.CreateAsync(new Db_ApplicationRole { Name = "User", IsSystemRole = true });
                }

                var roleResult = await m_userManager.AddToRoleAsync(user, "User");
                if (!roleResult.Succeeded)
                {
                    Logger.LogWarning("Register - User Role Assignment Failed: {email}", email);
                }


                Logger.LogInformation("Register - User Registration Success: {email}, IsInitialUser: {isFirstUser}", email, isFirstUser);

                return DataWrapper<Dto_Register>.Succeed(new Dto_Register()
                {
                    UserId = user.Id.ToString(),
                    Email = email,
                    RegisteredOn = user.RegisterAt,
                    IsInitialUser = isFirstUser ? true : null // Only set when true, null otherwise
                }, message: "User registered successfully. Please check your email to confirm your account.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Register - Process Failed: Exception: {ex}", ex);
                return DataWrapper<Dto_Register>.Fail_InternalError(message: "Failed to register user. Please try again or contact support if problem persists.");
            }
        }

        public async Task<DataWrapper<Dto_Login>> LoginAsync(Dto_LoginRequest loginData)
        {
            try
            {
                var email = loginData.Email.ToLower();

                // Find user by email
                var user = await m_userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    Logger.LogWarning("Login - User Not Found: {email}", email);
                    return DataWrapper<Dto_Login>.Unauthorized(message: "Invalid email or password.");
                }

                // Verify password
                var passwordValid = await m_userManager.CheckPasswordAsync(user, loginData.Password);
                if (!passwordValid)
                {
                    Logger.LogWarning("Login - Invalid Password: {email}", email);
                    return DataWrapper<Dto_Login>.Unauthorized(message: "Invalid email or password.");
                }

                // Generate JWT access token (now includes role permissions)
                var accessToken = await GenerateJWTAsync(user);

                // Generate refresh token and save to database
                var refreshToken = await GenerateRefreshTokenAsync(user.Id);

                Logger.LogInformation("Login - Successful Login: {email}", email);

                return DataWrapper<Dto_Login>.Succeed(new Dto_Login()
                {
                    Email = email,
                    Token = accessToken,
                    RefreshToken = refreshToken,
                    IsNewUser = false
                }, message: "Login successful.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Login - Process Failed: Exception: {ex}", ex);
                return DataWrapper<Dto_Login>.Fail_InternalError(message: "Failed to process login. Please try again or contact support if problem persists.");
            }
        }

        private string GenerateJWT(Db_ApplicationUser user, IList<string> userRoles, IList<Claim> userClaims)
        {
            var claims = new List<Claim>
            {
                new Claim("sub_id", user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("IsActive", user.IsActive.ToString())
            };

            // Add user's personal claims
            claims.AddRange(userClaims);

            // Add role claims
            foreach (var role in userRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(m_jwt_key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiry = DateTime.UtcNow.AddMinutes(m_jwt_accessTokenExpiryMinutes);

            var token = new JwtSecurityToken(
                m_jwt_issuer,
                m_jwt_audience,
                claims,
                expires: expiry,
                signingCredentials: creds
                );

            var genToken = new JwtSecurityTokenHandler().WriteToken(token);
            Logger.LogInformation($"------ GENERATED TOKEN ------- {genToken}");
            return genToken;
        }

        /// <summary>
        /// Generate JWT access token with user roles, user claims, and role permissions
        /// </summary>
        private async Task<string> GenerateJWTAsync(Db_ApplicationUser user)
        {
            try
            {
                // Get user roles and user claims
                var userRoles = await m_userManager.GetRolesAsync(user);
                var userClaims = await m_userManager.GetClaimsAsync(user);

                var claims = new List<Claim>
                {
                    new Claim("sub_id", user.Id.ToString()),
                    new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
                };

                // Add user's personal claims
                claims.AddRange(userClaims);

                // Add role claims and get role permissions
                var allPermissions = new HashSet<string>();
                foreach (var roleName in userRoles)
                {
                    // Add role claim
                    claims.Add(new Claim(ClaimTypes.Role, roleName));

                    // Get role and its permissions
                    var role = await m_roleManager.FindByNameAsync(roleName);
                    if (role != null)
                    {
                        var roleClaims = await m_roleManager.GetClaimsAsync(role);
                        
                        // Extract permission claims from role
                        var rolePermissions = roleClaims
                            .Where(c => c.Type == AuthorizationConstants.PermissionClaimType)
                            .Select(c => c.Value);
                        
                        foreach (var permission in rolePermissions)
                        {
                            allPermissions.Add(permission);
                        }
                    }
                }

                // Add all unique permissions to JWT token as multiple claims with same type
                foreach (var permission in allPermissions)
                {
                    claims.Add(new Claim(AuthorizationConstants.PermissionClaimType, permission));
                }

                Logger.LogInformation("JWT - User: {userId}, Roles: {roles}, Permissions: {permissions}", 
                    user.Id, 
                    string.Join(", ", userRoles), 
                    string.Join(", ", allPermissions));

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(m_jwt_key));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var expiry = DateTime.UtcNow.AddMinutes(m_jwt_accessTokenExpiryMinutes);

                var token = new JwtSecurityToken(
                    m_jwt_issuer,
                    m_jwt_audience,
                    claims,
                    expires: expiry,
                    signingCredentials: creds
                );

                var genToken = new JwtSecurityTokenHandler().WriteToken(token);
                Logger.LogInformation("JWT access token generated for user: {userId}", user.Id);
                return genToken;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "JWT token generation failed for user: {userId}", user.Id);
                throw;
            }
        }

        /// <summary>
        /// Generate a cryptographically secure refresh token and save it to the database
        /// </summary>
        private async Task<string> GenerateRefreshTokenAsync(Guid userId)
        {
            const int maxRetries = 3;
            int attempt = 0;

            while (attempt < maxRetries)
            {
                try
                {
                    // Generate cryptographically secure random token (64 bytes = 512 bits)
                    using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                    {
                        var randomBytes = new byte[64];
                        rng.GetBytes(randomBytes);
                        var token = Convert.ToBase64String(randomBytes);

                        // Create refresh token entity
                        var refreshToken = new Db_RefreshToken
                        {
                            Id = Guid.NewGuid(),
                            Token = token,
                            UserId = userId,
                            CreatedAt = DateTime.UtcNow,
                            ExpiresAt = DateTime.UtcNow.AddDays(m_jwt_refreshTokenExpiryDays)
                        };

                        // Save to database
                        m_authDbContext.RefreshTokens.Add(refreshToken);
                        await m_authDbContext.SaveChangesAsync();

                        Logger.LogInformation("Refresh token generated for user: {userId}", userId);
                        return token;
                    }
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex) when (attempt < maxRetries - 1)
                {
                    attempt++;
                    Logger.LogWarning(ex, "Concurrency conflict generating refresh token for user: {userId}. Retry attempt {attempt}/{maxRetries}", userId, attempt, maxRetries);
                    
                    // Clear tracked entities to avoid conflicts
                    m_authDbContext.ChangeTracker.Clear();
                    
                    // Small exponential backoff delay
                    await Task.Delay(50 * attempt);
                    continue;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Refresh token generation failed for user: {userId}", userId);
                    throw;
                }
            }

            throw new InvalidOperationException($"Failed to generate refresh token after {maxRetries} attempts due to concurrency conflicts");
        }

        /// <summary>
        /// Validate a refresh token and return the associated user (public method for controllers)
        /// </summary>
        public async Task<DataWrapper<Db_ApplicationUser>> ValidateRefreshTokenAsync(string refreshToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    Logger.LogWarning("Refresh token validation failed: token is empty");
                    return DataWrapper<Db_ApplicationUser>.Unauthorized(message: "Refresh token is invalid or expired.");
                }

                var tokenRecord = await m_authDbContext.RefreshTokens
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

                if (tokenRecord == null)
                {
                    Logger.LogWarning("Refresh token validation failed: token not found");
                    return DataWrapper<Db_ApplicationUser>.Unauthorized(message: "Refresh token is invalid or expired.");
                }

                if (tokenRecord.IsExpired)
                {
                    Logger.LogWarning("Refresh token validation failed: token expired for user: {userId}", tokenRecord.UserId);
                    return DataWrapper<Db_ApplicationUser>.Unauthorized(message: "Refresh token is invalid or expired.");
                }

                if (tokenRecord.User == null)
                {
                    Logger.LogWarning("Refresh token validation failed: user not found for token");
                    return DataWrapper<Db_ApplicationUser>.Unauthorized(message: "Refresh token is invalid or expired.");
                }

                Logger.LogInformation("Refresh token validated successfully for user: {userId}", tokenRecord.UserId);
                return DataWrapper<Db_ApplicationUser>.Succeed(tokenRecord.User, message: "Refresh token is valid.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Refresh token validation error");
                return DataWrapper<Db_ApplicationUser>.Fail_InternalError(message: "An error occurred while validating the refresh token.");
            }
        }

        /// <summary>
        /// Refresh the access token using a valid refresh token
        /// Performs token rotation: invalidates old refresh token and generates new one
        /// </summary>
        public async Task<DataWrapper<Dto_RefreshTokenResponse>> RefreshAccessTokenAsync(string refreshToken)
        {
            const int maxRetries = 3;
            int attempt = 0;

            while (attempt < maxRetries)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(refreshToken))
                    {
                        Logger.LogWarning("Token refresh failed: refresh token is empty");
                        return DataWrapper<Dto_RefreshTokenResponse>.Unauthorized(message: "Refresh token is invalid or expired.");
                    }

                    // Use a new DbContext scope to avoid tracking conflicts in concurrent requests
                    using (var transaction = await m_authDbContext.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            // Validate refresh token and get user with pessimistic lock
                            var tokenRecord = await m_authDbContext.RefreshTokens
                                .Include(rt => rt.User)
                                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

                            if (tokenRecord == null || tokenRecord.IsExpired || tokenRecord.User == null)
                            {
                                Logger.LogWarning("Token refresh failed: invalid or expired refresh token");
                                return DataWrapper<Dto_RefreshTokenResponse>.Unauthorized(message: "Refresh token is invalid or expired.");
                            }

                            var user = tokenRecord.User;

                            // Generate new access token (doesn't touch DB)
                            var newAccessToken = await GenerateJWTAsync(user);

                            // Delete old refresh token
                            m_authDbContext.RefreshTokens.Remove(tokenRecord);
                            await m_authDbContext.SaveChangesAsync();

                            // Generate new refresh token (adds and saves to DB)
                            var newRefreshToken = await GenerateRefreshTokenAsync(user.Id);

                            // Commit transaction
                            await transaction.CommitAsync();

                            Logger.LogInformation("Token refresh successful for user: {userId}", user.Id);

                            return DataWrapper<Dto_RefreshTokenResponse>.Succeed(
                                new Dto_RefreshTokenResponse
                                {
                                    Token = newAccessToken,
                                    RefreshToken = newRefreshToken
                                },
                                message: "Token refreshed successfully.");
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex) when (attempt < maxRetries - 1)
                {
                    attempt++;
                    Logger.LogWarning(ex, "Concurrency conflict during token refresh. Retry attempt {attempt}/{maxRetries}", attempt, maxRetries);
                    
                    // Clear tracked entities to avoid conflicts
                    m_authDbContext.ChangeTracker.Clear();
                    
                    // Small exponential backoff delay
                    await Task.Delay(50 * attempt);
                    continue;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("concurrency") && attempt < maxRetries - 1)
                {
                    attempt++;
                    Logger.LogWarning(ex, "Concurrency conflict during token refresh. Retry attempt {attempt}/{maxRetries}", attempt, maxRetries);
                    
                    // Clear tracked entities
                    m_authDbContext.ChangeTracker.Clear();
                    
                    await Task.Delay(50 * attempt);
                    continue;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Token refresh failed");
                    return DataWrapper<Dto_RefreshTokenResponse>.Fail_InternalError(message: "An error occurred while refreshing the token.");
                }
            }

            // If we get here, all retries failed
            Logger.LogError("Token refresh failed after {maxRetries} attempts due to concurrency conflicts", maxRetries);
            return DataWrapper<Dto_RefreshTokenResponse>.Fail_InternalError(message: "Unable to refresh token due to high server load. Please try again.");
        }

        /// <summary>
        /// Logout user by deleting their refresh token
        /// </summary>
        public async Task<DataWrapper<bool>> LogoutAsync(string refreshToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    Logger.LogWarning("Logout failed: refresh token is empty");
                    return DataWrapper<bool>.BadRequest(message: "Invalid refresh token.");
                }

                var tokenRecord = await m_authDbContext.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

                if (tokenRecord == null)
                {
                    Logger.LogWarning("Logout failed: refresh token not found");
                    return DataWrapper<bool>.BadRequest(message: "Invalid refresh token.");
                }

                // Delete the refresh token
                m_authDbContext.RefreshTokens.Remove(tokenRecord);
                await m_authDbContext.SaveChangesAsync();

                Logger.LogInformation("Logout successful for user: {userId}", tokenRecord.UserId);

                return DataWrapper<bool>.Succeed(true, message: "Logout successful.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Logout failed");
                return DataWrapper<bool>.Fail_InternalError(message: "An error occurred during logout.");
            }
        }

        public async Task<DataWrapper<bool>> SeedSuperAdminAsync()
        {
            try
            {
                const string superAdminEmail = "technical@advine.id";
                const string superAdminPassword = "P@ssw0rd";
                const string superAdminRole = "Super Admin";

                // Check if super admin user already exists
                var existingUser = await m_userManager.FindByEmailAsync(superAdminEmail);
                if (existingUser != null)
                {
                    Logger.LogInformation("Super admin user already exists: {email}", superAdminEmail);
                    return DataWrapper<bool>.Succeed(false, message: "Super admin user already exists.");
                }

                // Create super admin user
                var superAdminUser = new Db_ApplicationUser
                {
                    Email = superAdminEmail,
                    UserName = superAdminEmail,
                    EmailConfirmed = true,
                    RegisterAt = DateTime.UtcNow,
                    IsActive = true
                };

                var createResult = await m_userManager.CreateAsync(superAdminUser, superAdminPassword);
                if (!createResult.Succeeded)
                {
                    var errors = createResult.Errors.Select(e => $"[{e.Code}] {e.Description}").ToList();
                    Logger.LogError("Failed to create super admin user: {errors}", string.Join(", ", errors));
                    return DataWrapper<bool>.Fail_InternalError(
                        message: "Failed to create super admin user.",
                        errors: errors);
                }

                // Assign Super Admin role
                var roleResult = await m_userManager.AddToRoleAsync(superAdminUser, superAdminRole);
                if (!roleResult.Succeeded)
                {
                    var errors = roleResult.Errors.Select(e => $"[{e.Code}] {e.Description}").ToList();
                    Logger.LogError("Failed to assign Super Admin role: {errors}", string.Join(", ", errors));
                    return DataWrapper<bool>.Fail_InternalError(
                        message: "Failed to assign Super Admin role.",
                        errors: errors);
                }

                Logger.LogInformation("Super admin user created successfully: {email}", superAdminEmail);
                return DataWrapper<bool>.Succeed(true, message: "Super admin user created successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error seeding super admin user");
                return DataWrapper<bool>.Fail_InternalError(message: "An error occurred while seeding super admin user.");
            }
        }

        public async Task<Db_ApplicationUser?> GetUserByEmailAsync(string email)
        {
            return await m_userManager.FindByEmailAsync(email.ToLower());
        }

        public async Task<Db_ApplicationUser?> GetUserByIdAsync(string userId)
        {
            return await m_userManager.FindByIdAsync(userId);
        }

        public async Task<bool> ValidatePasswordAsync(Db_ApplicationUser user, string password)
        {
            return await m_userManager.CheckPasswordAsync(user, password);
        }

        public async Task<DataWrapper<bool>> ChangePasswordAsync(Db_ApplicationUser user, string currentPassword, string newPassword)
        {
            try
            {
                var result = await m_userManager.ChangePasswordAsync(user, currentPassword, newPassword);

                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => $"[{e.Code}] {e.Description}").ToList();
                    Logger.LogWarning("Failed to change password for user {email}: {errors}",
                        user.Email, string.Join(", ", errors));
                    return DataWrapper<bool>.BadRequest(
                        message: "Failed to change password",
                        errors: errors);
                }

                Logger.LogInformation("Password changed successfully for user: {email}", user.Email);
                return DataWrapper<bool>.Succeed(true, message: "Password changed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error changing password for user {email}", user.Email);
                return DataWrapper<bool>.Fail_InternalError(message: "An error occurred while changing password");
            }
        }
        #endregion

    }
}
