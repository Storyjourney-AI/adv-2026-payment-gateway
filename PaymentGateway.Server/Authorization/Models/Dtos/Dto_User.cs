namespace PaymentGateway.Server.Authorization.Models.Dtos
{
    // ============================================
    // User List Item DTO
    // ============================================
    public class Dto_UserListItem
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        /// <summary>
        /// User's full name (optional)
        /// </summary>
        public string? FullName { get; set; }
        public bool IsSuperAdmin { get; set; }
        public bool IsActive { get; set; }
        public DateTime RegisteredAt { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }

    // ============================================
    // User Detail DTO
    // ============================================
    public class Dto_UserDetail
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        /// <summary>
        /// User's full name (optional)
        /// </summary>
        public string? FullName { get; set; }
        public bool IsSuperAdmin { get; set; }
        public bool IsActive { get; set; }
        public DateTime RegisteredAt { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
        public bool EmailConfirmed { get; set; }
        public string? PhoneNumber { get; set; }
    }

    // ============================================
    // User Create Request DTO
    // ============================================
    public class Dto_UserCreateRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        /// <summary>
        /// User's full name (optional)
        /// </summary>
        public string? FullName { get; set; }
    }

    // ============================================
    // User Update Request DTO
    // ============================================
    public class Dto_UserUpdateRequest
    {
        public string Email { get; set; } = string.Empty;
        /// <summary>
        /// User's full name (optional)
        /// </summary>
        public string? FullName { get; set; }
        public bool IsActive { get; set; }
        public string? PhoneNumber { get; set; }
    }

    // ============================================
    // Batch Create Users Request DTO
    // ============================================
    public class Dto_BatchCreateUsersRequest
    {
        public List<Dto_BatchUserItem> Users { get; set; } = new List<Dto_BatchUserItem>();
    }

    public class Dto_BatchUserItem
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        /// <summary>
        /// User's full name (optional)
        /// </summary>
        public string? FullName { get; set; }
    }

    // ============================================
    // Batch Create Users Response DTO
    // ============================================
    public class Dto_BatchCreateUsersResponse
    {
        public int TotalRequested { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<Dto_BatchUserResult> Results { get; set; } = new List<Dto_BatchUserResult>();
    }

    public class Dto_BatchUserResult
    {
        public string Email { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? UserId { get; set; }
    }
}
