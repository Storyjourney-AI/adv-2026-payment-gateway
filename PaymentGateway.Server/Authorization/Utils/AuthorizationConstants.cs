namespace PaymentGateway.Server.Authorization.Utils
{
    /// <summary>
    /// Hard-coded permissions and policies for authorization
    /// </summary>
    public static class AuthorizationConstants
    {
        /// <summary>
        /// Claim type for permissions
        /// </summary>
        public const string PermissionClaimType = "permission";

        /// <summary>
        /// Available permissions (actions) in the system
        /// </summary>
        public static class Permissions
        {
            // User Management Permissions
            public const string UserView = "user_view";
            public const string UserCreate = "user_create";
            public const string UserEdit = "user_edit";
            public const string UserDelete = "user_delete";

            // Role Management Permissions
            public const string RoleView = "role_view";
            public const string RoleCreate = "role_create";
            public const string RoleEdit = "role_edit";
            public const string RoleDelete = "role_delete";

            // System Management Permissions
            public const string SystemView = "system_view";
            public const string SystemUpdate = "system_update";

            // Branch Management Permissions
            public const string BranchView = "branch_view";
            public const string BranchCreate = "branch_create";
            public const string BranchEdit = "branch_edit";

            // Part Management Permissions
            public const string PartView = "part_view";
            public const string PartCreate = "part_create";
            public const string PartEdit = "part_edit";
            public const string PartDelete = "part_delete";
            
            // Procedure Management Permissions
            public const string ProcedureView = "procedure_view";
            public const string ProcedureCreate = "procedure_create";
            public const string ProcedureEdit = "procedure_edit";
            public const string ProcedureDelete = "procedure_delete";
            public const string ProcedureExecute = "procedure_execute";
            
            // Runtime Management Permissions
            public const string RuntimeExecute = "runtime_execute";
            public const string RuntimeView = "runtime_view";
            public const string RuntimeManage = "runtime_manage";

            // Reports Section
            public const string ReportView = "report_view";
            public const string ReportExport = "report_export";
            public const string AnalyticsView = "analytics_view";

            /// <summary>
            /// Get all available permissions
            /// </summary>
            public static List<string> GetAll() => new()
            {
                UserView,
                UserCreate,
                UserEdit,
                UserDelete,
                RoleView,
                RoleCreate,
                RoleEdit,
                RoleDelete,
                SystemView,
                SystemUpdate,
                BranchView,
                BranchCreate,
                BranchEdit,
                PartView,
                PartCreate,
                PartEdit,
                PartDelete,
                ProcedureView,
                ProcedureCreate,
                ProcedureEdit,
                ProcedureDelete,
                ProcedureExecute,
                RuntimeExecute,
                RuntimeView,
                RuntimeManage,
                ReportView,
                ReportExport,
                AnalyticsView
            };

            /// <summary>
            /// Get all permissions organized by sections
            /// </summary>
            public static List<PermissionSection> GetAllSections() => new()
            {
                new PermissionSection
                {
                    SectionName = "User Management",
                    SectionKey = "user_management",
                    Description = "Permissions for managing users",
                    Permissions = new List<PermissionInfo>
                    {
                        new PermissionInfo
                        {
                            Name = UserView,
                            DisplayName = "View Users",
                            Description = "View user information and list users"
                        },
                        new PermissionInfo
                        {
                            Name = UserCreate,
                            DisplayName = "Create Users",
                            Description = "Create new user accounts"
                        },
                        new PermissionInfo
                        {
                            Name = UserEdit,
                            DisplayName = "Edit Users",
                            Description = "Modify existing user information"
                        },
                        new PermissionInfo
                        {
                            Name = UserDelete,
                            DisplayName = "Delete Users",
                            Description = "Delete user accounts"
                        }
                    }
                },
                new PermissionSection
                {
                    SectionName = "Role Management",
                    SectionKey = "role_management",
                    Description = "Permissions for managing roles and permissions",
                    Permissions = new List<PermissionInfo>
                    {
                        new PermissionInfo
                        {
                            Name = RoleView,
                            DisplayName = "View Roles",
                            Description = "View roles and their permissions"
                        },
                        new PermissionInfo
                        {
                            Name = RoleCreate,
                            DisplayName = "Create Roles",
                            Description = "Create new roles"
                        },
                        new PermissionInfo
                        {
                            Name = RoleEdit,
                            DisplayName = "Edit Roles",
                            Description = "Modify roles and assign permissions"
                        },
                        new PermissionInfo
                        {
                            Name = RoleDelete,
                            DisplayName = "Delete Roles",
                            Description = "Delete roles from the system"
                        }
                    }
                },
                new PermissionSection
                {
                    SectionName = "System Management",
                    SectionKey = "system_management",
                    Description = "Permissions for managing system configuration",
                    Permissions = new List<PermissionInfo>
                    {
                        new PermissionInfo
                        {
                            Name = SystemView,
                            DisplayName = "View System Settings",
                            Description = "View system configuration and settings"
                        },
                        new PermissionInfo
                        {
                            Name = SystemUpdate,
                            DisplayName = "Update System Settings",
                            Description = "Modify system configuration and settings"
                        }
                    }
                },
                new PermissionSection
                {
                    SectionName = "Branch Management",
                    SectionKey = "branch_management",
                    Description = "Permissions for managing branches and workspaces",
                    Permissions = new List<PermissionInfo>
                    {
                        new PermissionInfo
                        {
                            Name = BranchView,
                            DisplayName = "View Branches",
                            Description = "View branch information and select active branch"
                        },
                        new PermissionInfo
                        {
                            Name = BranchCreate,
                            DisplayName = "Create Branches",
                            Description = "Create new branches"
                        },
                        new PermissionInfo
                        {
                            Name = BranchEdit,
                            DisplayName = "Edit Branches",
                            Description = "Modify branch details and activate/deactivate branches"
                        }
                    }
                },
                new PermissionSection
                {
                    SectionName = "Part Management",
                    SectionKey = "part_management",
                    Description = "Permissions for managing parts, revisions, and measurements",
                    Permissions = new List<PermissionInfo>
                    {
                        new PermissionInfo
                        {
                            Name = PartView,
                            DisplayName = "View Parts",
                            Description = "View parts, revisions, and measurements"
                        },
                        new PermissionInfo
                        {
                            Name = PartCreate,
                            DisplayName = "Create Parts",
                            Description = "Create new parts and initial revisions"
                        },
                        new PermissionInfo
                        {
                            Name = PartEdit,
                            DisplayName = "Edit Parts",
                            Description = "Modify parts, create/edit revisions, and manage measurements"
                        },
                        new PermissionInfo
                        {
                            Name = PartDelete,
                            DisplayName = "Delete Parts",
                            Description = "Delete parts, revisions, and measurements"
                        }
                    }
                },
                new PermissionSection
                {
                    SectionName = "Procedure Management",
                    SectionKey = "procedure_management",
                    Description = "Permissions for managing and executing procedures",
                    Permissions = new List<PermissionInfo>
                    {
                        new PermissionInfo
                        {
                            Name = ProcedureView,
                            DisplayName = "View Procedures",
                            Description = "View procedures, schedules, and execution history"
                        },
                        new PermissionInfo
                        {
                            Name = ProcedureCreate,
                            DisplayName = "Create Procedures",
                            Description = "Create new procedures"
                        },
                        new PermissionInfo
                        {
                            Name = ProcedureEdit,
                            DisplayName = "Edit Procedures",
                            Description = "Modify procedures, schedules, and assignments"
                        },
                        new PermissionInfo
                        {
                            Name = ProcedureDelete,
                            DisplayName = "Delete Procedures",
                            Description = "Delete procedures"
                        },
                        new PermissionInfo
                        {
                            Name = ProcedureExecute,
                            DisplayName = "Execute Procedures",
                            Description = "Execute procedures and record measurements"
                        }
                    }
                },
                new PermissionSection
                {
                    SectionName = "Runtime Management",
                    SectionKey = "runtime_management",
                    Description = "Permissions for managing procedure runtimes",
                    Permissions = new List<PermissionInfo>
                    {
                        new PermissionInfo
                        {
                            Name = RuntimeExecute,
                            DisplayName = "Execute Runtimes",
                            Description = "Execute and manage own runtime executions"
                        },
                        new PermissionInfo
                        {
                            Name = RuntimeView,
                            DisplayName = "View All Runtimes",
                            Description = "View all runtime executions across the system"
                        },
                        new PermissionInfo
                        {
                            Name = RuntimeManage,
                            DisplayName = "Manage Runtimes",
                            Description = "Cancel and manage runtime executions"
                        }
                    }
                },
                new PermissionSection
                {
                    SectionName = "Reports",
                    SectionKey = "reports",
                    Description = "Permissions for viewing and exporting reports",
                    Permissions = new List<PermissionInfo>
                    {
                        new PermissionInfo
                        {
                            Name = ReportView,
                            DisplayName = "View Reports",
                            Description = "View reports and dashboards"
                        },
                        new PermissionInfo
                        {
                            Name = ReportExport,
                            DisplayName = "Export Reports",
                            Description = "Export reports in various formats"
                        },
                        new PermissionInfo
                        {
                            Name = AnalyticsView,
                            DisplayName = "View Analytics",
                            Description = "View analytical data and insights"
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Permission information for display
        /// </summary>
        public class PermissionInfo
        {
            public string Name { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
        }

        /// <summary>
        /// Permission section for grouping
        /// </summary>
        public class PermissionSection
        {
            public string SectionName { get; set; }
            public string SectionKey { get; set; }
            public string Description { get; set; }
            public List<PermissionInfo> Permissions { get; set; }
        }

        /// <summary>
        /// Available policies (for authorization)
        /// </summary>
        public static class Policies
        {
            // User Management Policies
            public const string UserView = "policy_user_view";
            public const string UserCreate = "policy_user_create";
            public const string UserEdit = "policy_user_edit";
            public const string UserDelete = "policy_user_delete";

            // Role Management Policies
            public const string RoleView = "policy_role_view";
            public const string RoleCreate = "policy_role_create";
            public const string RoleEdit = "policy_role_edit";
            public const string RoleDelete = "policy_role_delete";

            // System Management Policies
            public const string SystemView = "policy_system_view";
            public const string SystemUpdate = "policy_system_update";

            // Branch Management Policies
            public const string BranchView = "policy_branch_view";
            public const string BranchCreate = "policy_branch_create";
            public const string BranchEdit = "policy_branch_edit";

            // Part Management Policies
            public const string PartView = "policy_part_view";
            public const string PartCreate = "policy_part_create";
            public const string PartEdit = "policy_part_edit";
            public const string PartDelete = "policy_part_delete";

            // Procedure Management Policies
            public const string ProcedureView = "policy_procedure_view";
            public const string ProcedureCreate = "policy_procedure_create";
            public const string ProcedureEdit = "policy_procedure_edit";
            public const string ProcedureDelete = "policy_procedure_delete";
            public const string ProcedureExecute = "policy_procedure_execute";
            
            // Runtime Management Policies
            public const string RuntimeExecute = "policy_runtime_execute";
            public const string RuntimeView = "policy_runtime_view";
            public const string RuntimeManage = "policy_runtime_manage";

            // Report Management Policies
            public const string ReportView = "policy_report_view";
            public const string ReportExport = "policy_report_export";
            public const string AnalyticsView = "policy_analytics_view";

            /// <summary>
            /// Get all policies
            /// </summary>
            public static List<string> GetAll() => new()
            {
                UserView,
                UserCreate,
                UserEdit,
                UserDelete,
                RoleView,
                RoleCreate,
                RoleEdit,
                RoleDelete,
                SystemView,
                SystemUpdate,
                BranchView,
                BranchCreate,
                BranchEdit,
                PartView,
                PartCreate,
                PartEdit,
                PartDelete,
                ProcedureView,
                ProcedureCreate,
                ProcedureEdit,
                ProcedureDelete,
                ProcedureExecute,
                RuntimeExecute,
                RuntimeView,
                RuntimeManage,
                ReportView,
                ReportExport,
                AnalyticsView
            };
        }

        /// <summary>
        /// Policy to permission mapping (1:1)
        /// </summary>
        public static class PolicyPermissionMapping
        {
            public static readonly Dictionary<string, string> Mapping = new()
            {
                { Policies.UserView, Permissions.UserView },
                { Policies.UserCreate, Permissions.UserCreate },
                { Policies.UserEdit, Permissions.UserEdit },
                { Policies.UserDelete, Permissions.UserDelete },
                { Policies.RoleView, Permissions.RoleView },
                { Policies.RoleCreate, Permissions.RoleCreate },
                { Policies.RoleEdit, Permissions.RoleEdit },
                { Policies.RoleDelete, Permissions.RoleDelete },
                { Policies.SystemView, Permissions.SystemView },
                { Policies.SystemUpdate, Permissions.SystemUpdate },
                { Policies.BranchView, Permissions.BranchView },
                { Policies.BranchCreate, Permissions.BranchCreate },
                { Policies.BranchEdit, Permissions.BranchEdit },
                { Policies.PartView, Permissions.PartView },
                { Policies.PartCreate, Permissions.PartCreate },
                { Policies.PartEdit, Permissions.PartEdit },
                { Policies.PartDelete, Permissions.PartDelete },
                { Policies.ProcedureView, Permissions.ProcedureView },
                { Policies.ProcedureCreate, Permissions.ProcedureCreate },
                { Policies.ProcedureEdit, Permissions.ProcedureEdit },
                { Policies.ProcedureDelete, Permissions.ProcedureDelete },
                { Policies.ProcedureExecute, Permissions.ProcedureExecute },
                { Policies.RuntimeExecute, Permissions.RuntimeExecute },
                { Policies.RuntimeView, Permissions.RuntimeView },
                { Policies.RuntimeManage, Permissions.RuntimeManage },
                { Policies.ReportView, Permissions.ReportView },
                { Policies.ReportExport, Permissions.ReportExport },
                { Policies.AnalyticsView, Permissions.AnalyticsView }
            };

            /// <summary>
            /// Get permission required by a policy
            /// </summary>
            public static string GetPermissionForPolicy(string policyName)
            {
                return Mapping.TryGetValue(policyName, out var permission) ? permission : null;
            }

            /// <summary>
            /// Get policy name for a permission
            /// </summary>
            public static string GetPolicyForPermission(string permission)
            {
                return Mapping.FirstOrDefault(x => x.Value == permission).Key;
            }
        }
    }
}
