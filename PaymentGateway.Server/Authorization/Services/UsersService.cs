using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Server.Authorization.Models.Dbs;
using PaymentGateway.Server.Authorization.Models.Dtos;
using PaymentGateway.Server.Common.Models;

namespace PaymentGateway.Server.Authorization.Services
{
    public class UsersService
    {
        private readonly UserManager<Db_ApplicationUser> m_userManager;
        private readonly RoleManager<Db_ApplicationRole> m_roleManager;
        private readonly ILogger<UsersService> m_logger;

        public UsersService(
            UserManager<Db_ApplicationUser> userManager,
            RoleManager<Db_ApplicationRole> roleManager,
            ILogger<UsersService> logger)
        {
            m_userManager = userManager;
            m_roleManager = roleManager;
            m_logger = logger;
        }

        // ============================================
        // Get Users (Paginated with Search)
        // ============================================
        public async Task<DataWrapper<PaginationWrapper<Dto_UserListItem>>> GetUsersAsync(
            int page = 1,
            int pageSize = 10,
            string? search = null)
        {
            try
            {
                var query = m_userManager.Users.AsQueryable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(u => u.Email != null && u.Email.Contains(search));
                }

                var totalCount = await query.CountAsync();
                var users = await query
                    .OrderByDescending(u => u.RegisterAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var userDtos = new List<Dto_UserListItem>();
                foreach (var user in users)
                {
                    var roles = await m_userManager.GetRolesAsync(user);
                    userDtos.Add(new Dto_UserListItem
                    {
                        UserId = user.Id.ToString(),
                        Email = user.Email ?? string.Empty,
                        FullName = user.FullName,
                        IsSuperAdmin = roles.Contains("Super Admin"),
                        IsActive = user.IsActive,
                        RegisteredAt = user.RegisterAt,
                        Roles = roles.ToList()
                    });
                }

                var result = new PaginationWrapper<Dto_UserListItem>
                {
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalItems = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    Items = userDtos
                };

                return DataWrapper<PaginationWrapper<Dto_UserListItem>>.Succeed(result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error getting users list");
                return DataWrapper<PaginationWrapper<Dto_UserListItem>>.Fail_InternalError(
                    message: "Failed to retrieve users.");
            }
        }

        // ============================================
        // Get User By ID
        // ============================================
        public async Task<DataWrapper<Dto_UserDetail>> GetByIdAsync(string userId)
        {
            try
            {
                if (!Guid.TryParse(userId, out var userGuid))
                {
                    return DataWrapper<Dto_UserDetail>.BadRequest(message: "Invalid user ID format.");
                }

                var user = await m_userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return DataWrapper<Dto_UserDetail>.NotFound(message: "User not found.");
                }

                var roles = await m_userManager.GetRolesAsync(user);
                var userDto = new Dto_UserDetail
                {
                    UserId = user.Id.ToString(),
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName,
                    IsSuperAdmin = roles.Contains("Super Admin"),
                    IsActive = user.IsActive,
                    RegisteredAt = user.RegisterAt,
                    Roles = roles.ToList(),
                    EmailConfirmed = user.EmailConfirmed,
                    PhoneNumber = user.PhoneNumber
                };

                return DataWrapper<Dto_UserDetail>.Succeed(userDto);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error getting user by ID: {userId}", userId);
                return DataWrapper<Dto_UserDetail>.Fail_InternalError(
                    message: "Failed to retrieve user details.");
            }
        }

        // ============================================
        // Create User
        // ============================================
        public async Task<DataWrapper<Dto_UserDetail>> CreateAsync(Dto_UserCreateRequest request)
        {
            try
            {
                var email = request.Email.ToLower();

                // Check if user already exists
                var existingUser = await m_userManager.FindByEmailAsync(email);
                if (existingUser != null)
                {
                    return DataWrapper<Dto_UserDetail>.Conflict(
                        message: "Email already registered.");
                }

                // Create user
                var user = new Db_ApplicationUser
                {
                    Email = email,
                    UserName = email,
                    FullName = request.FullName,
                    RegisterAt = DateTime.UtcNow,
                    EmailConfirmed = false,
                    IsActive = true
                };

                var createResult = await m_userManager.CreateAsync(user, request.Password);
                if (!createResult.Succeeded)
                {
                    var errors = createResult.Errors.Select(e => e.Description).ToList();
                    return DataWrapper<Dto_UserDetail>.BadRequest(
                        message: "Failed to create user.", errors: errors);
                }

                // Assign default User role
                await m_userManager.AddToRoleAsync(user, "User");

                var roles = await m_userManager.GetRolesAsync(user);
                var userDto = new Dto_UserDetail
                {
                    UserId = user.Id.ToString(),
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName,
                    IsSuperAdmin = roles.Contains("Super Admin"),
                    IsActive = user.IsActive,
                    RegisteredAt = user.RegisterAt,
                    Roles = roles.ToList(),
                    EmailConfirmed = user.EmailConfirmed,
                    PhoneNumber = user.PhoneNumber
                };

                m_logger.LogInformation("User created successfully: {email}", email);
                return DataWrapper<Dto_UserDetail>.Succeed(userDto, 
                    message: "User created successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error creating user");
                return DataWrapper<Dto_UserDetail>.Fail_InternalError(
                    message: "Failed to create user.");
            }
        }

        // ============================================
        // Update User
        // ============================================
        public async Task<DataWrapper<Dto_UserDetail>> UpdateAsync(string userId, Dto_UserUpdateRequest request)
        {
            try
            {
                var user = await m_userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return DataWrapper<Dto_UserDetail>.NotFound(message: "User not found.");
                }

                user.Email = request.Email.ToLower();
                user.UserName = request.Email.ToLower();
                user.FullName = request.FullName;
                user.IsActive = request.IsActive;
                user.PhoneNumber = request.PhoneNumber;

                var updateResult = await m_userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    var errors = updateResult.Errors.Select(e => e.Description).ToList();
                    return DataWrapper<Dto_UserDetail>.BadRequest(
                        message: "Failed to update user.", errors: errors);
                }

                var roles = await m_userManager.GetRolesAsync(user);
                var userDto = new Dto_UserDetail
                {
                    UserId = user.Id.ToString(),
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName,
                    IsSuperAdmin = roles.Contains("Super Admin"),
                    IsActive = user.IsActive,
                    RegisteredAt = user.RegisterAt,
                    Roles = roles.ToList(),
                    EmailConfirmed = user.EmailConfirmed,
                    PhoneNumber = user.PhoneNumber
                };

                m_logger.LogInformation("User updated successfully: {userId}", userId);
                return DataWrapper<Dto_UserDetail>.Succeed(userDto, 
                    message: "User updated successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error updating user: {userId}", userId);
                return DataWrapper<Dto_UserDetail>.Fail_InternalError(
                    message: "Failed to update user.");
            }
        }

        // ============================================
        // Toggle Active Status
        // ============================================
        public async Task<DataWrapper<bool>> ToggleActiveAsync(string userId)
        {
            try
            {
                var user = await m_userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return DataWrapper<bool>.NotFound(message: "User not found.");
                }

                user.IsActive = !user.IsActive;
                var updateResult = await m_userManager.UpdateAsync(user);

                if (!updateResult.Succeeded)
                {
                    var errors = updateResult.Errors.Select(e => e.Description).ToList();
                    return DataWrapper<bool>.BadRequest(
                        message: "Failed to toggle user status.", errors: errors);
                }

                m_logger.LogInformation("User status toggled: {userId}, IsActive: {isActive}", 
                    userId, user.IsActive);
                return DataWrapper<bool>.Succeed(true, 
                    message: $"User {(user.IsActive ? "activated" : "deactivated")} successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error toggling user status: {userId}", userId);
                return DataWrapper<bool>.Fail_InternalError(
                    message: "Failed to toggle user status.");
            }
        }

        // ============================================
        // Batch Create Users
        // ============================================
        public async Task<DataWrapper<Dto_BatchCreateUsersResponse>> BatchCreateAsync(
            Dto_BatchCreateUsersRequest request)
        {
            try
            {
                var response = new Dto_BatchCreateUsersResponse
                {
                    TotalRequested = request.Users.Count,
                    Results = new List<Dto_BatchUserResult>()
                };

                foreach (var userItem in request.Users)
                {
                    try
                    {
                        var email = userItem.Email.ToLower();

                        // Check if user already exists
                        var existingUser = await m_userManager.FindByEmailAsync(email);
                        if (existingUser != null)
                        {
                            response.Results.Add(new Dto_BatchUserResult
                            {
                                Email = email,
                                Success = false,
                                ErrorMessage = "Email already registered"
                            });
                            response.FailureCount++;
                            continue;
                        }

                        // Create user
                        var user = new Db_ApplicationUser
                        {
                            Email = email,
                            UserName = email,
                            FullName = userItem.FullName,
                            RegisterAt = DateTime.UtcNow,
                            EmailConfirmed = false,
                            IsActive = true
                        };

                        var createResult = await m_userManager.CreateAsync(user, userItem.Password);
                        if (!createResult.Succeeded)
                        {
                            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                            response.Results.Add(new Dto_BatchUserResult
                            {
                                Email = email,
                                Success = false,
                                ErrorMessage = errors
                            });
                            response.FailureCount++;
                            continue;
                        }

                        // Assign default User role
                        await m_userManager.AddToRoleAsync(user, "User");

                        response.Results.Add(new Dto_BatchUserResult
                        {
                            Email = email,
                            Success = true,
                            UserId = user.Id.ToString()
                        });
                        response.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        m_logger.LogError(ex, "Error creating user in batch: {email}", userItem.Email);
                        response.Results.Add(new Dto_BatchUserResult
                        {
                            Email = userItem.Email,
                            Success = false,
                            ErrorMessage = "Internal error occurred"
                        });
                        response.FailureCount++;
                    }
                }

                m_logger.LogInformation("Batch user creation completed. Success: {success}, Failure: {failure}",
                    response.SuccessCount, response.FailureCount);
                
                return DataWrapper<Dto_BatchCreateUsersResponse>.Succeed(response,
                    message: $"Batch operation completed. {response.SuccessCount} succeeded, {response.FailureCount} failed.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error in batch user creation");
                return DataWrapper<Dto_BatchCreateUsersResponse>.Fail_InternalError(
                    message: "Failed to process batch user creation.");
            }
        }
    }
}
