using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TyreLifecycle.EntityFrameworkCore;
namespace TyreLifecycle.HttpApi.Host.Workspace;
[ApiController, Route("api/v1/connection"), Authorize]
public class ConnectionController(TyreLifecycleDbContext db) : ControllerBase {
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(new { database = await db.Database.CanConnectAsync(), sms = false, telematics = false });
}
