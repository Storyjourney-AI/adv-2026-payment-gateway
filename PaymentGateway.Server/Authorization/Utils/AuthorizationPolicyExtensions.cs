using Microsoft.AspNetCore.Authorization;

namespace PaymentGateway.Server.Authorization.Utils
{
    /// <summary>
    /// Extension methods for adding authorization policies
    /// </summary>
    public static class AuthorizationPolicyExtensions
    {
        /// <summary>
        /// Add custom authorization policies
        /// MVP: Using role-based authorization, permission-based policies commented out for future use
        /// </summary>
        public static IServiceCollection AddCustomAuthorizationPolicies(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                // MVP: Role-based policies
                options.AddPolicy("RequireSuperAdmin", policy => 
                    policy.RequireRole("Super Admin"));
                
                options.AddPolicy("RequireAdmin", policy => 
                    policy.RequireRole("Admin", "Super Admin"));
                
                options.AddPolicy("RequireUser", policy => 
                    policy.RequireRole("User", "Super Admin"));

                // User Management Policies (Active for MVP)
                options.AddPolicy(AuthorizationConstants.Policies.UserView, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.UserView)));

                options.AddPolicy(AuthorizationConstants.Policies.UserCreate, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.UserCreate)));

                // FUTURE: Permission-based policies (from old project, kept as samples)
                // Uncomment and customize as needed when implementing fine-grained permissions
                
                /*
                // User Management Policies (Full set)
                options.AddPolicy(AuthorizationConstants.Policies.UserEdit, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.UserEdit)));

                options.AddPolicy(AuthorizationConstants.Policies.UserDelete, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.UserDelete)));

                // Role Management Policies
                options.AddPolicy(AuthorizationConstants.Policies.RoleView, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.RoleView)));

                options.AddPolicy(AuthorizationConstants.Policies.RoleCreate, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.RoleCreate)));

                options.AddPolicy(AuthorizationConstants.Policies.RoleEdit, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.RoleEdit)));

                options.AddPolicy(AuthorizationConstants.Policies.RoleDelete, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.RoleDelete)));

                // System Management Policies
                options.AddPolicy(AuthorizationConstants.Policies.SystemView, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.SystemView)));

                options.AddPolicy(AuthorizationConstants.Policies.SystemUpdate, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.SystemUpdate)));

                // Branch Management Policies
                options.AddPolicy(AuthorizationConstants.Policies.BranchView, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.BranchView)));

                options.AddPolicy(AuthorizationConstants.Policies.BranchCreate, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.BranchCreate)));

                options.AddPolicy(AuthorizationConstants.Policies.BranchEdit, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.BranchEdit)));

                // Part Management Policies
                options.AddPolicy(AuthorizationConstants.Policies.PartView, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.PartView)));

                options.AddPolicy(AuthorizationConstants.Policies.PartCreate, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.PartCreate)));

                options.AddPolicy(AuthorizationConstants.Policies.PartEdit, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.PartEdit)));

                options.AddPolicy(AuthorizationConstants.Policies.PartDelete, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.PartDelete)));
                
                // Procedure Management Policies
                options.AddPolicy(AuthorizationConstants.Policies.ProcedureView, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.ProcedureView)));

                options.AddPolicy(AuthorizationConstants.Policies.ProcedureCreate, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.ProcedureCreate)));

                options.AddPolicy(AuthorizationConstants.Policies.ProcedureEdit, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.ProcedureEdit)));

                options.AddPolicy(AuthorizationConstants.Policies.ProcedureDelete, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.ProcedureDelete)));

                options.AddPolicy(AuthorizationConstants.Policies.ProcedureExecute, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.ProcedureExecute)));
                
                // Runtime Management Policies
                options.AddPolicy(AuthorizationConstants.Policies.RuntimeExecute, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.RuntimeExecute)));

                options.AddPolicy(AuthorizationConstants.Policies.RuntimeView, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.RuntimeView)));

                options.AddPolicy(AuthorizationConstants.Policies.RuntimeManage, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.RuntimeManage)));

                // Report Management Policies
                options.AddPolicy(AuthorizationConstants.Policies.ReportView, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.ReportView)));

                options.AddPolicy(AuthorizationConstants.Policies.ReportExport, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.ReportExport)));

                options.AddPolicy(AuthorizationConstants.Policies.AnalyticsView, policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == AuthorizationConstants.PermissionClaimType && 
                                                    c.Value == AuthorizationConstants.Permissions.AnalyticsView)));
                */
            });

            return services;
        }
    }
}
