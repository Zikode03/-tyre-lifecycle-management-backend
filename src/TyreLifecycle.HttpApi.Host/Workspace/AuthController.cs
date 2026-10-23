using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TyreLifecycle.EntityFrameworkCore;
using TyreLifecycle.Workspace;
using Volo.Abp.Security.Claims;

namespace TyreLifecycle.HttpApi.Host.Workspace;

[ApiController, Route("api/v1/auth"), IgnoreAntiforgeryToken]
public class AuthController(TyreLifecycleDbContext db, IWebHostEnvironment env) : ControllerBase
{
    public record LoginInput(string Email, string Password, bool RememberMe = false);
    public record PinRequest(string Mobile);
    public record PinInput(Guid ChallengeId, string Pin);

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginInput input)
    {
        var user = await db.StaffAccounts.SingleOrDefaultAsync(x => x.Email == input.Email.Trim().ToLower());
        var hasher = new PasswordHasher<StaffAccount>();
        // Verify a dummy hash too so unknown addresses do not skip the expensive operation.
        var target = user ?? new StaffAccount();
        var hash = user?.PasswordHash ?? hasher.HashPassword(target, "invalid-account-password");
        var result = hasher.VerifyHashedPassword(target, hash, input.Password);
        if (user is null || user.LockedUntil > DateTime.UtcNow || result == PasswordVerificationResult.Failed) {
            if (user is not null) {
                user.FailedAttempts++;
                if (user.FailedAttempts >= 5) user.LockedUntil = DateTime.UtcNow.AddMinutes(10);
                await db.SaveChangesAsync();
            }
            return Unauthorized(new { message = "Incorrect email or password, or account temporarily locked." });
        }
        user.FailedAttempts = 0; user.LockedUntil = null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded) user.PasswordHash = hasher.HashPassword(user, input.Password);
        await db.SaveChangesAsync();
        var claims = new[] { new Claim(AbpClaimTypes.UserId, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name), new Claim(ClaimTypes.Email, user.Email),
            new Claim(AbpClaimTypes.Role, user.Role), new Claim("kind", "staff") };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme,
                ClaimTypes.Name, AbpClaimTypes.Role)),
            new AuthenticationProperties { IsPersistent = input.RememberMe, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
        return Ok(Profile(user));
    }

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me()
    {
        if (User.FindFirstValue("kind") == "customer") {
            var customerId = User.FindFirstValue("customerId");
            var profile = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x=>x.CustomerNumber==customerId && x.IsActive);
            if(profile is null || User.FindFirstValue("customerMobile")!=profile.MobileNumber) return Unauthorized();
            return Ok(new {kind="customer", id=profile.CustomerNumber, name=(profile.FirstName+" "+profile.LastName).Trim().TrimEnd('—').Trim()});
        }
        if (!Guid.TryParse(User.FindFirstValue(AbpClaimTypes.UserId), out var id)) return Unauthorized();
        var user = await db.StaffAccounts.FindAsync(id);
        return user is null ? Unauthorized() : Ok(Profile(user));
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout() {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return NoContent();
    }

    [HttpPost("customer/request-pin"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> RequestPin(PinRequest input)
    {
        // No SMS provider is configured. Never pretend a code was delivered in production.
        if (!env.IsDevelopment()) return StatusCode(503, new { message = "SMS delivery has not been configured." });
        string mobile;
        try { mobile = TyreLifecycle.Customers.CustomerContact.NormalizeMobile(input.Mobile); }
        catch (ArgumentException ex) { return BadRequest(new { message=ex.Message }); }
        var customers = await db.Customers.AsNoTracking().Where(x=>x.IsActive).ToListAsync();
        var customer = customers.SingleOrDefault(x=>TyreLifecycle.Customers.CustomerContact.NormalizeMobile(x.MobileNumber)==mobile);
        if (customer is null) return BadRequest(new { message = "This mobile number is not registered or the customer account is inactive." });
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var challenge = new PortalChallenge { Id = Guid.NewGuid(), CustomerId = customer.CustomerNumber,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5) };
        challenge.CodeHash = Hash(challenge.Id, code);
        db.PortalChallenges.Add(challenge); await db.SaveChangesAsync();
        return Ok(new { challengeId = challenge.Id, developmentCode = code, message = "Local test code — SMS delivery is not configured." });
    }

    [HttpPost("customer/verify-pin"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> VerifyPin(PinInput input)
    {
        // Atomically consume the challenge so the same code cannot issue two sessions.
        var challenge = await db.PortalChallenges.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.ChallengeId);
        if (challenge is null || challenge.Used || challenge.Attempts >= 5 || challenge.ExpiresAt <= DateTime.UtcNow)
            return Unauthorized(new { message = "The code is invalid or expired. Request a new code." });
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(challenge.CodeHash), Encoding.UTF8.GetBytes(Hash(challenge.Id, input.Pin)))) {
            await db.PortalChallenges.Where(x => x.Id == challenge.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Attempts, x => x.Attempts + 1));
            return Unauthorized(new { message = "Incorrect code." });
        }
        var consumed = await db.PortalChallenges.Where(x => x.Id == challenge.Id && !x.Used && x.Attempts < 5 && x.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Used, true));
        if (consumed != 1) return Unauthorized();
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x=>x.CustomerNumber==challenge.CustomerId && x.IsActive);
        if(customer is null) return Unauthorized(new { message="This customer account is inactive or unavailable." });
        var name = (customer.FirstName+" "+customer.LastName).Trim().TrimEnd('—').Trim();
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("kind", "customer"), new Claim("customerId", challenge.CustomerId), new Claim("customerMobile",customer.MobileNumber), new Claim(ClaimTypes.Name, name) },
                CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) });
        return Ok(new { kind = "customer", id = challenge.CustomerId, name });
    }

    public record MobileChangeRequest(string CustomerId, string Mobile);
    [HttpPost("customer/mobile-change/request"), Authorize(Roles="system-admin,branch-manager,service-advisor"), EnableRateLimiting("login")]
    public async Task<IActionResult> RequestMobileChange(MobileChangeRequest input)
    {
        if (!env.IsDevelopment()) return StatusCode(503,new {message="SMS delivery has not been configured. The existing mobile remains unchanged."});
        string mobile;
        try { mobile=TyreLifecycle.Customers.CustomerContact.NormalizeMobile(input.Mobile); }
        catch(ArgumentException ex){return BadRequest(new {message=ex.Message});}
        if(!Guid.TryParse(User.FindFirstValue(AbpClaimTypes.UserId),out var staffId))return Forbid();
        var staff=await db.StaffAccounts.FindAsync(staffId);
        if(staff is null||!User.IsInRole(staff.Role))return Forbid();
        if(staff.Role!="system-admin") {
            await CoreWorkspace.RefreshAsync(db);
            var documents=await db.WorkspaceDocuments.AsNoTracking().ToListAsync();
            var access=new LinkedWorkspaceAccess(staff,documents);
            var customersDocument=documents.SingleOrDefault(x=>x.Key=="tyretrack.demo.customers");
            if(customersDocument is null||!access.Filter(customersDocument.Key,customersDocument.Json).Any(x=>x.GetProperty("id").GetString()==input.CustomerId))return Forbid();
        }
        var customer=await db.Customers.SingleOrDefaultAsync(x=>x.CustomerNumber==input.CustomerId);
        if(customer is null) return NotFound();
        if(await db.Customers.AnyAsync(x=>x.CustomerNumber!=input.CustomerId && x.MobileNumber==mobile)) return Conflict(new {message="This mobile is already registered."});
        var code=RandomNumberGenerator.GetInt32(100000,1000000).ToString();
        var challenge=new PortalChallenge{Id=Guid.NewGuid(),CustomerId="mobile:"+input.CustomerId+":"+mobile,ExpiresAt=DateTime.UtcNow.AddMinutes(5)};
        challenge.CodeHash=Hash(challenge.Id,code);db.PortalChallenges.Add(challenge);await db.SaveChangesAsync();
        return Ok(new {challengeId=challenge.Id,developmentCode=code,message="Local verification only. SMS delivery is not configured."});
    }

    private static string Hash(Guid id, string pin) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id + ":" + pin)));
    private static object Profile(StaffAccount user) => new { kind = "staff", id = user.Id, user.Name, user.Email,
        user.Role, user.RoleLabel, user.Branch, user.BranchId, user.FleetId, initials = string.Concat(user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => x[0])) };
}
