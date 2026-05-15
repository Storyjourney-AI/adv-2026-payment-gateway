using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Server.Common.Models;
using PaymentGateway.Server.Security.Operations;

namespace PaymentGateway.Server.Security.Controllers
{
    [ApiController]
    [Route("api/security/metrics")]
    [Authorize(Policy = "RequireSuperAdmin")]
    public class SecurityMetricsController : ControllerBase
    {
        private readonly ISecurityMetricsService m_securityMetricsService;

        public SecurityMetricsController(ISecurityMetricsService securityMetricsService)
        {
            m_securityMetricsService = securityMetricsService;
        }

        [HttpGet]
        public ActionResult<DataWrapper<IReadOnlyList<SecurityMetricSnapshot>>> GetMetrics()
        {
            var snapshots = m_securityMetricsService.GetSnapshots();
            return Ok(DataWrapper<IReadOnlyList<SecurityMetricSnapshot>>.Succeed(
                snapshots,
                message: "Security metrics snapshot retrieved successfully."));
        }
    }
}
