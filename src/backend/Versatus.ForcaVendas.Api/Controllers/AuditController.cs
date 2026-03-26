using Microsoft.AspNetCore.Mvc;
using Versatus.ForcaVendas.Infrastructure.Data.Repositories;
using Versatus.ForcaVendas.Domain.Auditoria;

namespace Versatus.ForcaVendas.Api.Controllers;

[ApiController]
[Route("admin/audit")]
public class AuditController(ISessionAuditEventRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var events = await repository.GetAllAsync(cancellationToken);
        return Ok(events);
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetByUserId(string userId, CancellationToken cancellationToken)
    {
        var events = await repository.GetByUserIdAsync(userId, cancellationToken);
        return Ok(events);
    }

    [HttpGet("tenant/{tenantId}")]
    public async Task<IActionResult> GetByTenantId(string tenantId, CancellationToken cancellationToken)
    {
        var events = await repository.GetByTenantIdAsync(tenantId, cancellationToken);
        return Ok(events);
    }
}
