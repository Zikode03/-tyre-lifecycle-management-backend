using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TyreLifecycle.EntityFrameworkCore;
using TyreLifecycle.Workspace;
using Volo.Abp.Security.Claims;

namespace TyreLifecycle.HttpApi.Host.Workspace;

[ApiController, Route("api/v1/workspace"), Authorize, IgnoreAntiforgeryToken]
public class WorkspaceController(TyreLifecycleDbContext db) : ControllerBase
{
    private static readonly HashSet<string> Keys = new(StringComparer.Ordinal) {
        "tyretrack.demo.customers", "tyretrack.demo.vehicles", "tyretrack.demo.bookings", "tyretrack.demo.inspections",
        "tyretrack.demo.warranty", "tyretrack.demo.tyreScans", "tyretrack.demo.tpms", "tyretrack.demo.devices",
        "tyretrack.demo.branches", "tyretrack.demo.fleets", "tyretrack.demo.jobCards", "tyretrack.demo.stock",
        "tyretrack.demo.notifications", "tyretrack.demo.notificationOutbox", "tyretrack.demo.fitments", "tyretrack.demo.removals", "tyretrack.demo.rotations", "tyretrack.demo.jobWork", "tyretrack.demo.settings", "tyretrack.lifecycle.events", "tyretrack.lifecycle.recalls",
        "tyretrack.lifecycle.orders", "tyretrack.lifecycle.audit", "tyretrack.lifecycle.offline",
        "tyretrack.data.tyres", "tyretrack.data.customers", "tyretrack.data.vehicles", "tyretrack.data.mileageReadings",
        "tyretrack.data.telematicsConnections", "tyretrack.data.recentActivity"
    };
    public record SaveInput(JsonElement Data, long Version);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (User.FindFirstValue("kind") == "customer" && !await ActiveCustomerAsync()) return Forbid();
        await CoreWorkspace.RefreshAsync(db);
        var docs = await db.WorkspaceDocuments.AsNoTracking().ToListAsync();
        if (User.FindFirstValue("kind") == "customer") {
            var scope = CustomerScope(docs);
            return Ok(docs.Where(x => CustomerKeys.Contains(x.Key)).ToDictionary(x => x.Key,
                x => new { data = Filter(x.Key, x.Json, scope), x.Version }));
        }
        if (User.FindFirstValue("kind") != "staff") return Forbid();
        var staff=await CurrentStaffAsync();if(staff is null || !User.IsInRole(staff.Role))return Forbid();
        if(staff.Role=="system-admin")return Ok(docs.ToDictionary(x => x.Key,x=>new{data=JsonSerializer.Deserialize<JsonElement>(x.Json),x.Version}));
        var access=new LinkedWorkspaceAccess(staff,docs);
        return Ok(docs.Where(x=>access.Readable(x.Key)).ToDictionary(x=>x.Key,x=>new{data=access.Filter(x.Key,x.Json),x.Version}));
    }

    [HttpPut("{key}"), RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Save(string key, SaveInput input)
    {
        var isCustomer = User.FindFirstValue("kind") == "customer";
        if (isCustomer && !await ActiveCustomerAsync()) return Forbid();
        if (!isCustomer && User.FindFirstValue("kind") != "staff") return Forbid();
        if (!Keys.Contains(key) && !key.StartsWith("tyretrack.demo.evidence.", StringComparison.Ordinal)) return BadRequest();
        if(key is "tyretrack.demo.fitments" or "tyretrack.demo.removals" or "tyretrack.demo.rotations" or "tyretrack.demo.jobWork")return StatusCode(403,new{message="Fitment history is recorded by the fitment action and cannot be overwritten."});
        if (key.Length > 160 || input.Data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return BadRequest();
        if ((key == "tyretrack.demo.settings" || key == "tyretrack.demo.branches" || key == "tyretrack.demo.fleets") &&
            !User.IsInRole("system-admin") && !User.IsInRole("branch-manager")) return Forbid();
        var staff=isCustomer?null:await CurrentStaffAsync();
        if(!isCustomer&&(staff is null||!User.IsInRole(staff.Role)))return Forbid();
        if(staff?.Role!="system-admin" && key=="tyretrack.data.vehicles")return Forbid();
        await CoreWorkspace.RefreshAsync(db);
        var doc = await db.WorkspaceDocuments.FindAsync(key);
        if(staff is not null && staff.Role!="system-admin") {
            var access=new LinkedWorkspaceAccess(staff,await db.WorkspaceDocuments.AsNoTracking().ToListAsync());
            try{input=input with{Data=access.Merge(key,input.Data,doc?.Json)};}
            catch(UnauthorizedAccessException ex){HttpContext.RequestServices.GetRequiredService<ILogger<WorkspaceController>>().LogWarning("Workspace access denied for {Key}: {Reason}",key,ex.Message);return Forbid();}
            catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or JsonException){return BadRequest(new{message="Enter valid records and references."});}
        }
        if (isCustomer) {
            if (!CustomerWriteKeys.Contains(key) || doc is null || input.Data.ValueKind != JsonValueKind.Array) return Forbid();
            var scope = CustomerScope(await db.WorkspaceDocuments.AsNoTracking().ToListAsync());
            var oldData = JsonSerializer.Deserialize<JsonElement[]>(doc.Json)!;
            var proposed = input.Data.EnumerateArray().ToArray();
            if(key=="tyretrack.lifecycle.audit") {
                var nodes=System.Text.Json.Nodes.JsonNode.Parse(input.Data.GetRawText())!.AsArray();
                foreach(var node in nodes) {
                    if(node is not System.Text.Json.Nodes.JsonObject record)return BadRequest();
                    var id=record["id"]?.GetValue<string>()??"";
                    if(!oldData.Any(x=>Text(x,"id")==id)){record["actor"]=scope.Name;record["at"]=DateTime.UtcNow.ToString("O");record["customerId"]=scope.Id;}
                }
                proposed=JsonSerializer.Deserialize<JsonElement[]>(nodes.ToJsonString())!;
            }
            if (proposed.Any(x => !Owned(key, x, scope))) return Forbid();
            var oldOwned = oldData.Where(x => Owned(key, x, scope)).ToArray();
            if (!ValidCustomerEdit(key, oldOwned, proposed)) return BadRequest(new { message = "This customer action is not allowed." });
            var merged = oldData.Where(x => !Owned(key, x, scope)).Concat(proposed).ToArray();
            input = input with { Data = JsonSerializer.SerializeToElement(merged) };
        }
        else if (!User.IsInRole("system-admin") && !User.IsInRole("branch-manager")) {
            if ((key == "tyretrack.demo.customers" || key == "tyretrack.demo.bookings") && !User.IsInRole("service-advisor")) return Forbid();
            if (key == "tyretrack.demo.warranty") return Forbid();
            if (key == "tyretrack.demo.jobCards" && User.IsInRole("fleet-manager")) return Forbid();
        }
        if(key=="tyretrack.demo.inspections"&&doc is not null&&input.Data.ValueKind==JsonValueKind.Array) {
            var locked=await db.Inspections.Where(i=>i.Status==TyreLifecycle.Inspections.InspectionStatus.Completed).ToListAsync();
            var before=JsonSerializer.Deserialize<JsonElement[]>(doc.Json)!;var proposed=input.Data.EnumerateArray().ToArray();
            foreach(var inspection in locked.Where(i=>i.ExtraProperties.ContainsKey("WorkshopInspectionId"))) {
                var old=before.FirstOrDefault(x=>Text(x,"id")==inspection.InspectionNumber);var updated=proposed.FirstOrDefault(x=>Text(x,"id")==inspection.InspectionNumber);
                if(updated.ValueKind==JsonValueKind.Undefined||old.ValueKind==JsonValueKind.Undefined||!EqualJson(old,updated))return BadRequest(new{message="Completed workshop inspections are immutable. Record a new inspection for new findings."});
            }
        }
        var validation=await ValidateAsync(key,input.Data);
        if(validation is not null)return BadRequest(new{message=validation});
        if (doc is null) {
            if (input.Version != 0) return Conflict(new { message = "This data changed in another session. Reload before saving." });
            doc = new WorkspaceDocument { Key = key }; db.WorkspaceDocuments.Add(doc);
        } else if (doc.Version != input.Version) return Conflict(new { message = "This data changed in another session. Reload before saving." });
        if(key=="tyretrack.demo.customers") {
            foreach(var item in input.Data.EnumerateArray()) {
                var existing=await db.Customers.SingleOrDefaultAsync(x=>x.CustomerNumber==Text(item,"id"));
                if(existing is null || existing.MobileNumber==TyreLifecycle.Customers.CustomerContact.NormalizeMobile(Text(item,"mobile"))) continue;
                var challengeId=Guid.Parse(Text(item.GetProperty("mobileProof"),"challengeId"));
                var challenge=await db.PortalChallenges.FindAsync(challengeId);
                challenge!.Used=true;
                foreach(var loginChallenge in await db.PortalChallenges.Where(x=>x.CustomerId==existing.CustomerNumber && !x.Used).ToListAsync()) loginChallenge.Used=true;
            }
            var sanitized=System.Text.Json.Nodes.JsonNode.Parse(input.Data.GetRawText())!.AsArray();
            foreach(var record in sanitized) record!.AsObject().Remove("mobileProof");
            input=input with {Data=JsonSerializer.SerializeToElement(sanitized)};
        }
        doc.Json = input.Data.GetRawText(); doc.Version++; doc.UpdatedAt = DateTime.UtcNow;
        try {
            await CoreWorkspace.ApplyAsync(db,key,input.Data,actor:User.Identity?.Name??"Staff");
            await db.SaveChangesAsync();
        }
        catch (ArgumentException ex) { db.ChangeTracker.Clear(); return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return Conflict(new { message = "Another session saved first. Reload before saving." }); }
        catch (DbUpdateException ex) when (key == "tyretrack.demo.vehicles" && ex.InnerException is Microsoft.Data.SqlClient.SqlException vehicleSql && vehicleSql.Number is 2601 or 2627) { db.ChangeTracker.Clear(); return Conflict(new {message="This registration or vehicle reference already exists. Refresh the vehicle list."}); }
        catch (DbUpdateException ex) when (key == "tyretrack.demo.customers" && ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627) { db.ChangeTracker.Clear(); return Conflict(new { message = "This mobile number or customer reference is already registered. Refresh the customer list." }); }
        catch (DbUpdateException) when (input.Version == 0) { db.ChangeTracker.Clear(); return Conflict(new { message = "Another session initialized this data. Reload before saving." }); }
        await CoreWorkspace.RefreshAsync(db);
        var updates = (await db.WorkspaceDocuments.AsNoTracking().Where(x => x.Key==key || CoreWorkspace.Keys.Contains(x.Key) || x.Key=="tyretrack.demo.bookings" || x.Key=="tyretrack.demo.jobCards").ToListAsync())
            .ToDictionary(x=>x.Key,x=>new { data=JsonSerializer.Deserialize<JsonElement>(x.Json),x.Version });
        if (isCustomer) {
            var scope=CustomerScope(await db.WorkspaceDocuments.AsNoTracking().ToListAsync());
            return Ok(new { doc.Version, updates=updates.Where(x=>CustomerKeys.Contains(x.Key)).ToDictionary(x=>x.Key,x=>new {data=Filter(x.Key,JsonSerializer.Serialize(x.Value.data),scope),x.Value.Version}) });
        }
        if(staff is not null && staff.Role!="system-admin") {
            var access=new LinkedWorkspaceAccess(staff,await db.WorkspaceDocuments.AsNoTracking().ToListAsync());
            // Include the saved collection as well as canonical core updates, with identical scope filtering.
            var documents=await db.WorkspaceDocuments.AsNoTracking().Where(x=>x.Key==key||CoreWorkspace.Keys.Contains(x.Key)||x.Key=="tyretrack.demo.bookings"||x.Key=="tyretrack.demo.jobCards").ToListAsync();
            return Ok(new{doc.Version,updates=documents.Where(x=>access.Readable(x.Key)).ToDictionary(x=>x.Key,x=>new{data=access.Filter(x.Key,x.Json),x.Version})});
        }
        return Ok(new { doc.Version, updates });
    }

    private record PortalScope(string Id,string Name,HashSet<string> Registrations,HashSet<string> VehicleIds,HashSet<string> TyreIds,HashSet<string> JobIds);
    private static readonly HashSet<string> CustomerKeys = new() {
        "tyretrack.demo.inspections", "tyretrack.demo.jobWork",
        "tyretrack.demo.customers", "tyretrack.demo.vehicles", "tyretrack.demo.bookings", "tyretrack.demo.jobCards",
        "tyretrack.demo.tyreScans", "tyretrack.data.tyres", "tyretrack.demo.warranty", "tyretrack.lifecycle.audit"
    };
    private static readonly HashSet<string> CustomerWriteKeys = new() {
        "tyretrack.demo.vehicles", "tyretrack.demo.bookings", "tyretrack.demo.jobCards", "tyretrack.demo.tyreScans", "tyretrack.lifecycle.audit"
    };
    private PortalScope CustomerScope(List<WorkspaceDocument> docs)
    {
        var id = User.FindFirstValue("customerId")!;
        var customers = JsonSerializer.Deserialize<JsonElement[]>(docs.First(x => x.Key == "tyretrack.demo.customers").Json)!;
        var name = Text(customers.First(x => Text(x, "id") == id), "name");
        var vehicles = JsonSerializer.Deserialize<JsonElement[]>(docs.First(x => x.Key == "tyretrack.demo.vehicles").Json)!;
        var ownedVehicles=vehicles.Where(x=>Text(x,"customerId")==id).ToArray();
        var registrations=ownedVehicles.Select(x=>Text(x,"registration")).ToHashSet();
        var tyresDoc=docs.FirstOrDefault(x=>x.Key=="tyretrack.data.tyres");
        var tyres=tyresDoc is null?[]:JsonSerializer.Deserialize<JsonElement[]>(tyresDoc.Json)!;
        var jobsDoc=docs.FirstOrDefault(x=>x.Key=="tyretrack.demo.jobCards");
        var jobs=jobsDoc is null?[]:JsonSerializer.Deserialize<JsonElement[]>(jobsDoc.Json)!;
        return new PortalScope(id,name,registrations,ownedVehicles.Select(x=>Text(x,"id")).ToHashSet(),tyres.Where(x=>registrations.Contains(Text(x,"vehicle"))).Select(x=>Text(x,"id")).ToHashSet(),jobs.Where(x=>registrations.Contains(Text(x,"registration"))).Select(x=>Text(x,"id")).ToHashSet());
    }
    private static string Text(JsonElement value, string key) => value.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
    private static bool Owned(string key, JsonElement item, PortalScope scope) => key switch {
        "tyretrack.demo.customers" => Text(item, "id") == scope.Id,
        "tyretrack.demo.vehicles" => Text(item, "customerId") == scope.Id,
        "tyretrack.demo.bookings" or "tyretrack.demo.jobCards" => Text(item, "customer") == scope.Name && scope.Registrations.Contains(Text(item, "registration")),
        "tyretrack.data.tyres" => scope.Registrations.Contains(Text(item, "vehicle")),
        "tyretrack.demo.jobWork" => scope.JobIds.Contains(Text(item,"jobCardId"))&&scope.Registrations.Contains(Text(item,"registration")),
        "tyretrack.demo.inspections" => Text(item,"status")=="Completed" && scope.Registrations.Contains(Text(item,"registration")) && scope.TyreIds.Contains(Text(item,"tyreId")),
        "tyretrack.demo.tyreScans" => scope.Registrations.Contains(Text(item, "registration")),
        "tyretrack.lifecycle.audit" => (Text(item,"customerId")==""||Text(item,"customerId")==scope.Id) && (Text(item,"entityType") switch {
            "Vehicle"=>scope.VehicleIds.Contains(Text(item,"entityId"))||scope.Registrations.Contains(Text(item,"entityId")),
            "Tyre"=>scope.TyreIds.Contains(Text(item,"entityId")),
            "Job card"=>scope.JobIds.Contains(Text(item,"entityId")),
            _=>false
        }),
        _ => false
    };
    private static JsonElement[] Filter(string key, string json, PortalScope scope) =>
        JsonSerializer.Deserialize<JsonElement[]>(json)!.Where(x => Owned(key, x, scope)).ToArray();
    private static bool ValidCustomerEdit(string key, JsonElement[] before, JsonElement[] after)
    {
        // Customer updates cannot remove records, alter prices, move vehicles, or review their own scans.
        if (after.Select(x => Text(x, "id")).Distinct().Count() != after.Length) return false;
        foreach (var old in before) {
            var item = after.FirstOrDefault(x => Text(x, "id") == Text(old, "id"));
            if (item.ValueKind == JsonValueKind.Undefined) return false;
            var allowed = key switch {
                "tyretrack.demo.vehicles" => new[] { "mileage" },
                "tyretrack.demo.jobCards" => new[] { "quoteDecision", "status", "decisionAt", "customerNote" },
                _ => Array.Empty<string>()
            };
            foreach (var property in old.EnumerateObject()) {
                if (allowed.Contains(property.Name)) continue;
                if (!item.TryGetProperty(property.Name, out var value) || !EqualJson(property.Value, value)) return false;
            }
            foreach (var property in item.EnumerateObject())
                if (!old.TryGetProperty(property.Name, out _) && !allowed.Contains(property.Name)) return false;
            if (key == "tyretrack.demo.vehicles" && (!item.GetProperty("mileage").TryGetInt64(out var km) || km < old.GetProperty("mileage").GetInt64())) return false;
            if (key == "tyretrack.demo.jobCards" && !EqualJson(item, old)) {
                var decision = Text(item, "quoteDecision");
                if (Text(old, "quoteDecision") != "Pending" || Text(old, "status") != "Quote sent" ||
                    decision is not ("Approved" or "Declined") || Text(item, "status") != decision) return false;
            }
        }
        foreach (var item in after.Where(x => !before.Any(y => Text(x, "id") == Text(y, "id")))) {
            if (string.IsNullOrWhiteSpace(Text(item, "id"))) return false;
            if (key is "tyretrack.demo.vehicles" or "tyretrack.demo.jobCards") return false;
            if (key == "tyretrack.demo.bookings" && Text(item, "status") != "Pending") return false;
            if (key == "tyretrack.demo.tyreScans" && (Text(item, "submittedBy") != "Customer" || Text(item, "reviewStatus") != "Awaiting review")) return false;
        }
        return true;
    }

    private async Task<string?> ValidateAsync(string key, JsonElement data)
    {
        if (key == "tyretrack.demo.settings") return data.ValueKind == JsonValueKind.Object ? null : "Settings must be an object.";
        if (data.ValueKind != JsonValueKind.Array) return "Records must be an array.";
        if (data.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.Object) && !key.StartsWith("tyretrack.demo.evidence.")) return "Each record must be an object.";
        if (key.StartsWith("tyretrack.demo.evidence.")) return null;
        var records = data.EnumerateArray().ToArray();
        if (key == "tyretrack.demo.customers") {
            var mobiles = new HashSet<string>();
            foreach (var item in records) {
                if (string.IsNullOrWhiteSpace(Text(item,"id")) || Text(item,"id").Length > 32) return "A valid customer reference is required.";
                if (Text(item,"status") is not ("Active" or "Inactive")) return "Choose Active or Inactive.";
                try {
                    TyreLifecycle.Customers.CustomerContact.NormalizeName(Text(item,"name"));
                    TyreLifecycle.Customers.CustomerContact.NormalizeEmail(Text(item,"email"));
                    var mobile = TyreLifecycle.Customers.CustomerContact.NormalizeMobile(Text(item,"mobile"));
                    if (!mobiles.Add(mobile)) return "A customer with this mobile number already exists. Open their existing profile instead.";
                } catch (ArgumentException ex) { return ex.Message; }
            }
            foreach(var item in records) {
                if(item.TryGetProperty("preferences",out var preferences)) {
                    if(preferences.ValueKind!=JsonValueKind.Object || preferences.EnumerateObject().Any(x=>x.Name is not ("serviceSms" or "serviceEmail" or "marketing") || x.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))) return "Choose valid communication preferences.";
                    if(preferences.TryGetProperty("serviceEmail",out var emailChoice) && emailChoice.ValueKind==JsonValueKind.True && string.IsNullOrWhiteSpace(Text(item,"email"))) return "Enter an email address to enable email updates.";
                }
                var id=Text(item,"id");var existing=await db.Customers.AsNoTracking().SingleOrDefaultAsync(x=>x.CustomerNumber==id);
                var mobile=TyreLifecycle.Customers.CustomerContact.NormalizeMobile(Text(item,"mobile"));
                if(existing is null || existing.MobileNumber==mobile) continue;
                if(!item.TryGetProperty("mobileProof",out var proof) || proof.ValueKind!=JsonValueKind.Object || !Guid.TryParse(Text(proof,"challengeId"),out var challengeId)) return "Verify the new mobile number before saving.";
                var challenge=await db.PortalChallenges.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==challengeId);
                if(challenge is null || challenge.CustomerId!="mobile:"+id+":"+mobile || challenge.Used || challenge.Attempts>=5 || challenge.ExpiresAt<=DateTime.UtcNow) return "The mobile verification code is invalid or expired.";
                var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(challengeId+":"+Text(proof,"pin"))));
                if(!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(hash),System.Text.Encoding.UTF8.GetBytes(challenge.CodeHash))) {
                    await db.PortalChallenges.Where(x=>x.Id==challengeId).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.Attempts,x=>x.Attempts+1));return "Incorrect mobile verification code.";
                }
            }
            if (records.Select(x => Text(x,"id")).Distinct().Count() != records.Length) return "Customer references must be unique.";
        }
        if(key=="tyretrack.demo.inspections") {
            var oldDoc=await db.WorkspaceDocuments.FindAsync(key);var oldRows=oldDoc is null?[]:JsonSerializer.Deserialize<JsonElement[]>(oldDoc.Json)!;
            foreach(var row in records) {
                var old=oldRows.FirstOrDefault(x=>Text(x,"id")==Text(row,"id"));
                if((Text(row,"status")=="Completed"||Text(row,"workshopVisitId").Length>0)&&(old.ValueKind==JsonValueKind.Undefined||!EqualJson(old,row)))return "Complete measured inspections through the workshop inspection action.";
            }
        }
        if (key == "tyretrack.demo.vehicles") {
            var customers = await db.WorkspaceDocuments.AsNoTracking().SingleAsync(x => x.Key == "tyretrack.demo.customers");
            var ids = JsonSerializer.Deserialize<JsonElement[]>(customers.Json)!.Select(x => Text(x,"id")).ToHashSet();
            var previousVehicleDoc=await db.WorkspaceDocuments.FindAsync(key);
            var previousVehicles=previousVehicleDoc is null?[]:JsonSerializer.Deserialize<JsonElement[]>(previousVehicleDoc.Json)!;
            var branchDoc=await db.WorkspaceDocuments.FindAsync("tyretrack.demo.branches");
            var branches=branchDoc is null?[]:JsonSerializer.Deserialize<JsonElement[]>(branchDoc.Json)!;
            var fleetDoc=await db.WorkspaceDocuments.FindAsync("tyretrack.demo.fleets");
            var fleets=fleetDoc is null?[]:JsonSerializer.Deserialize<JsonElement[]>(fleetDoc.Json)!;
            foreach (var item in records) {
                var oldVehicle=previousVehicles.FirstOrDefault(x=>Text(x,"id")==Text(item,"id"));
                if(oldVehicle.ValueKind==JsonValueKind.Undefined||new[]{"branchId","fleetId","technicianId"}.Any(field=>Text(item,field)!=SafeText(oldVehicle,field))) {
                    var branchId=Text(item,"branchId");var fleetId=Text(item,"fleetId");var technicianId=Text(item,"technicianId");
                    if(!branches.Any(x=>Text(x,"id")==branchId&&Text(x,"status")!="Inactive"))return "Choose an active registered branch for the vehicle.";
                    if(fleetId.Length>0&&!fleets.Any(x=>Text(x,"id")==fleetId&&Text(x,"branchId")==branchId))return "Choose a fleet in the vehicle branch.";
                    if(technicianId.Length>0&&(!Guid.TryParse(technicianId,out var techId)||!await db.StaffAccounts.AnyAsync(x=>x.Id==techId&&x.Role=="technician"&&x.BranchId==branchId)))return "Choose a technician assigned to the vehicle branch.";
                }
                if(Text(item,"id").Length>64) return "Vehicle references must be 64 characters or fewer.";
                if(Text(item,"status") is not ("Active" or "Inactive")) return "Choose Active or Inactive for the vehicle.";
                if(!item.TryGetProperty("year",out var yearValue)||!yearValue.TryGetInt32(out var year)) return "Enter a whole model year.";
                try { TyreLifecycle.Vehicles.VehicleRules.Registration(Text(item,"registration"));TyreLifecycle.Vehicles.VehicleRules.Details(Text(item,"make"),Text(item,"model"),year,0); }
                catch(ArgumentException ex){return ex.Message;}
                if (!ids.Contains(Text(item,"customerId"))) return "Choose a registered customer for the vehicle.";
                if (string.IsNullOrWhiteSpace(Text(item,"id")) || string.IsNullOrWhiteSpace(Text(item,"registration")) ||
                    string.IsNullOrWhiteSpace(Text(item,"make")) || string.IsNullOrWhiteSpace(Text(item,"model"))) return "Vehicle registration, make and model are required.";
                if (!item.TryGetProperty("mileage", out var mileage) || !mileage.TryGetInt64(out var km) || km < 0 || km>int.MaxValue) return "Mileage must be whole kilometres from 0 to 2,147,483,647.";
            }
            if(records.Select(x=>Text(x,"id")).Distinct().Count()!=records.Length) return "Vehicle references must be unique.";
            if (records.Select(x => TyreLifecycle.Vehicles.VehicleRules.RegistrationKey(Text(x,"registration"))).Distinct().Count() != records.Length) return "Vehicle registrations must be unique.";
        }
        if(key=="tyretrack.demo.jobCards") {
            var priorDoc=await db.WorkspaceDocuments.FindAsync(key);var prior=priorDoc is null?[]:JsonSerializer.Deserialize<JsonElement[]>(priorDoc.Json)!;
            foreach(var old in prior){
                var row=records.FirstOrDefault(r=>Text(r,"id")==Text(old,"id"));
                var protectedJob=Text(old,"quoteDecision")=="Approved"||Text(old,"status") is "Quote sent" or "In workshop" or "Completed";
                if(row.ValueKind==JsonValueKind.Undefined){if(Text(old,"workshopStartedAt")!="")return "Retain recorded workshop jobs and their history.";continue;}
                foreach(var field in new[]{"workshopStartedAt","workshopExpectedTyres","serviceSummary","completedAt"}){
                    if(row.TryGetProperty(field,out var next)){if(!old.TryGetProperty(field,out var previous)||!EqualJson(previous,next))return "Workshop status and handover metadata are recorded by job actions.";}else if(old.TryGetProperty(field,out _))return "Retain workshop metadata.";
                }
                if(Text(row,"status")=="Completed"&&Text(old,"status")!="Completed")return "Complete the job through the recorded-work action.";
                if(Text(old,"status")=="Completed"&&!EqualJson(old,row))return "Completed jobs and their handover are immutable.";
                if(protectedJob){
                    foreach(var field in new[]{"id","items","registration","customer","customerId","inspectionIds","vatRate","quoteNumber","branch"}){
                        if(old.TryGetProperty(field,out var previous)&&(!row.TryGetProperty(field,out var next)||!EqualJson(previous,next)))return "Revise and obtain new approval before changing quoted work.";
                    }
                    if(Text(old,"quoteDecision")=="Approved"&&(Text(row,"quoteDecision")!="Approved"||Text(row,"status") is "Draft" or "Quote sent" or "Declined"))return "Approved work cannot be reset through quotation edits.";
                }
            }
            foreach(var row in records.Where(r=>!prior.Any(o=>Text(o,"id")==Text(r,"id"))))if(new[]{"workshopStartedAt","workshopExpectedTyres","serviceSummary","completedAt"}.Any(field=>row.TryGetProperty(field,out _))||Text(row,"status")=="Completed")return "Workshop metadata requires recorded job actions.";
            foreach(var row in records.Where(r=>Text(r,"status")=="Quote sent"))if(!row.TryGetProperty("items",out var included)||!included.EnumerateArray().Any(JobWorkActions.Included))return "Include at least one work item before sending the quotation.";
            var generated=await db.WorkshopInspections.Where(x=>x.JobCardId!=null).Select(x=>x.JobCardId).ToListAsync();
            foreach(var item in records.Where(x=>generated.Contains(Text(x,"id"))&&Text(x,"status")!="Draft")) {
                if(!item.TryGetProperty("items",out var items)||items.ValueKind!=JsonValueKind.Array||items.GetArrayLength()==0||items.EnumerateArray().Any(x=>!x.TryGetProperty("unitPrice",out var p)||!p.TryGetDecimal(out var price)||price<=0||!x.TryGetProperty("quantity",out var q)||!q.TryGetDecimal(out var quantity)||quantity<=0))return "Price every recommended work item before sending the quotation.";
            }
        }
        return null;
    }
    private static string SafeText(JsonElement item,string key)=>item.ValueKind==JsonValueKind.Object?Text(item,key):"";
    private static bool EqualJson(JsonElement left, JsonElement right) => System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(left.GetRawText()), System.Text.Json.Nodes.JsonNode.Parse(right.GetRawText()));
    private async Task<StaffAccount?> CurrentStaffAsync() {
        return Guid.TryParse(User.FindFirstValue(AbpClaimTypes.UserId),out var id)?await db.StaffAccounts.FindAsync(id):null;
    }
    private Task<bool> ActiveCustomerAsync() {
        var id=User.FindFirstValue("customerId");
        var mobile=User.FindFirstValue("customerMobile");
        return db.Customers.AnyAsync(x=>x.CustomerNumber==id && x.IsActive && x.MobileNumber==mobile);
    }}
