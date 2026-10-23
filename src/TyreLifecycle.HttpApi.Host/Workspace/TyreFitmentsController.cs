using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TyreLifecycle.EntityFrameworkCore;
using TyreLifecycle.Tyres;
using TyreLifecycle.Workspace;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace TyreLifecycle.HttpApi.Host.Workspace;

[ApiController,Route("api/v1/tyre-fitments"),Authorize,IgnoreAntiforgeryToken,UnitOfWork(IsDisabled=true)]
public class TyreFitmentsController(TyreLifecycleDbContext db):ControllerBase
{
    public record FitInput(Guid RequestId,string VehicleId,string Position,string Reference,DateOnly FitmentDate,long Odometer,decimal? InitialTread,string? Sku=null,string? Brand=null,string? Model=null,string? Size=null,string? Notes=null,string? RemoveTyreId=null,string? RemovalReason=null,string? Destination=null,string? JobCardId=null,string? ItemId=null);
    public record RemoveInput(Guid RequestId,string VehicleId,string TyreId,string Position,DateOnly RemovalDate,long Odometer,string Reason,string Destination,string? Notes=null);
    [HttpPost("remove")]
    public Task<IActionResult> Remove(RemoveInput input)=>Execute(new FitInput(input.RequestId,input.VehicleId,input.Position,"",input.RemovalDate,input.Odometer,null,Notes:input.Notes,RemoveTyreId:input.TyreId,RemovalReason:input.Reason,Destination:input.Destination),true);
    private static readonly string[] Destinations=["Storage","Recycling","Retread","Warranty return","Returned to customer","Scrapped"];
    private static readonly string[] Positions=["Front Left","Front Right","Rear Left","Rear Right","Spare"];
    private static readonly string[] UpdateKeys=["tyretrack.data.tyres","tyretrack.demo.vehicles","tyretrack.demo.fitments","tyretrack.demo.removals","tyretrack.demo.rotations","tyretrack.demo.stock","tyretrack.lifecycle.audit","tyretrack.demo.jobCards","tyretrack.demo.jobWork"];
    private static string Text(JsonElement row,string key)=>row.ValueKind==JsonValueKind.Object&&row.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()!:"";
    private static bool IsDatabaseConflict(Exception ex)=>ex is SqlException sql&&sql.Number is 1205 or 2601 or 2627||ex.InnerException is not null&&IsDatabaseConflict(ex.InnerException);
    private static string Clean(string? value)=>Regex.Replace(value?.Trim()??"",@"\s+"," ");
    private static string PositionKey(string value)=>Regex.Replace(value.ToUpperInvariant(),@"[\s-]","");
    private static string ReferenceKey(string value)=>value.Replace(" ","").ToUpperInvariant();
    private async Task<JsonElement[]> Rows(string key) {
        var document=await db.WorkspaceDocuments.FindAsync(key);
        return document is null?[]:JsonSerializer.Deserialize<JsonElement[]>(document.Json)!;
    }
    private async Task<object> UpdatesAsync(StaffAccount actor) {
        var documents=await db.WorkspaceDocuments.AsNoTracking().ToListAsync();
        var scope=new LinkedWorkspaceAccess(actor,documents);
        var updates=documents.Where(x=>UpdateKeys.Contains(x.Key)&&(actor.Role=="system-admin"||scope.Readable(x.Key))).ToDictionary(x=>x.Key,x=>new{data=actor.Role=="system-admin"?JsonSerializer.Deserialize<JsonElement>(x.Json):JsonSerializer.SerializeToElement(scope.Filter(x.Key,x.Json)),x.Version});
        return updates;
    }
    private async Task<IActionResult> ResponseAsync(TyreFitment fitment,StaffAccount actor,bool replayed,string? clientId=null) {
        return Ok(new{tyreId=clientId??fitment.TyreId.ToString(),fitmentId=fitment.Id,replayed,updates=await UpdatesAsync(actor)});
    }
    public record RotationMove(string TyreId,string FromPosition,string ToPosition);
    public record RotationInput(Guid RequestId,string VehicleId,DateOnly RotationDate,long Odometer,List<RotationMove>? Moves,string? Notes=null,string? JobCardId=null,string? ItemId=null);
    [HttpPost("rotate")]
    public async Task<IActionResult> Rotate(RotationInput input) {
        if(User.FindFirstValue("kind")!="staff"||!Guid.TryParse(User.FindFirstValue(AbpClaimTypes.UserId),out var actorId))return Forbid();
        var today=DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2)).DateTime);var notes=Clean(input.Notes);
        if(input.RequestId==Guid.Empty||string.IsNullOrWhiteSpace(input.VehicleId)||input.VehicleId.Length>64||input.RotationDate<new DateOnly(1900,1,1)||input.RotationDate>today||input.Odometer<0||input.Odometer>int.MaxValue||notes.Length>500)return BadRequest(new{message="Enter a valid vehicle, date, whole odometer and notes of at most 500 characters."});
        if(input.Moves is null||input.Moves.Count is <2 or >5||input.Moves.Any(m=>m is null||string.IsNullOrWhiteSpace(m.TyreId)||m.TyreId.Length>64||!Positions.Contains(m.FromPosition)||!Positions.Contains(m.ToPosition)))return BadRequest(new{message="Include each fitted tyre and choose supported wheel positions."});
        var moves=input.Moves.OrderBy(m=>m.TyreId,StringComparer.Ordinal).ToArray();
        if(moves.Select(m=>m.TyreId).Distinct().Count()!=moves.Length||moves.Select(m=>m.ToPosition).Distinct().Count()!=moves.Length||moves.Count(m=>m.FromPosition!=m.ToPosition)<2)return BadRequest(new{message="Choose unique destination positions and move at least two tyres."});
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{input.JobCardId,input.ItemId,input.VehicleId,input.RotationDate,input.Odometer,moves,notes}))));
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try {
            var actor=await db.StaffAccounts.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==actorId);
            if(actor is null||actor.Role is not ("system-admin" or "branch-manager" or "service-advisor" or "technician")||!User.IsInRole(actor.Role))return Forbid();
            var vehicle=await db.Vehicles.SingleOrDefaultAsync(v=>v.Id==CoreWorkspace.EntityId("vehicle",input.VehicleId));
            if(vehicle is null)return NotFound(new{message="Vehicle not found."});
            var stored=vehicle.ExtraProperties.TryGetValue("WorkspaceRecord",out var raw)&&raw is not null?JsonSerializer.Deserialize<JsonElement>(raw.ToString()!):default;
            if(Text(stored,"id")!=input.VehicleId||!new VehicleWorkspaceAccess(actor).Owns(stored))return Forbid();
            var existing=await db.TyreRotations.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==input.RequestId);
            if(existing is not null) {
                if(existing.ActorId!=actor.Id||existing.RequestHash!=hash)return Conflict(new{message="This request key was used for different rotation details."});
                await CoreWorkspace.RefreshAsync(db);var replay=await RotationResponse(existing.Id,actor,true);await transaction.CommitAsync();return replay;
            }
            if(await db.TyreFitments.AnyAsync(f=>f.Id==input.RequestId)||await db.TyreRemovals.AnyAsync(r=>r.Id==input.RequestId))return Conflict(new{message="This request key was already used for another action."});
            if(!vehicle.IsActive||input.Odometer<vehicle.OdometerKm)return BadRequest(new{message="Use an active vehicle and an odometer at least equal to its saved mileage."});
            var branches=await Rows("tyretrack.demo.branches");if(!branches.Any(b=>Text(b,"id")==Text(stored,"branchId")&&Text(b,"status")!="Inactive"))return BadRequest(new{message="Assign the vehicle to an active branch first."});
            var lastWorkshopInspection=await db.WorkshopInspections.Where(w=>w.VehicleId==vehicle.Id).MaxAsync(w=>(DateOnly?)w.InspectionDate);
            var lastRotation=await db.TyreRotations.Where(r=>r.VehicleId==vehicle.Id).MaxAsync(r=>(DateOnly?)r.RotationDate);
            var lastFit=await db.TyreFitments.Where(f=>f.VehicleId==vehicle.Id).MaxAsync(f=>(DateOnly?)f.FitmentDate);
            var lastRemoval=await db.TyreRemovals.Where(r=>r.VehicleId==vehicle.Id).MaxAsync(r=>(DateOnly?)r.RemovalDate);
            if(input.RotationDate<lastWorkshopInspection||input.RotationDate<lastRotation||input.RotationDate<lastFit||input.RotationDate<lastRemoval)return BadRequest(new{message="Rotation cannot be dated before an already recorded tyre action on this vehicle."});
            var fitted=await db.Tyres.Where(t=>t.VehicleId==vehicle.Id&&t.IsActive).ToListAsync();
            if(fitted.Count!=moves.Length)return Conflict(new{message="The fitted tyres changed. Refresh the vehicle before rotating."});
            var snapshots=new JsonArray();
            foreach(var move in moves) {
                var tyre=fitted.FirstOrDefault(t=>t.Id==CoreWorkspace.EntityId("tyre",move.TyreId));
                var record=tyre is not null&&tyre.ExtraProperties.TryGetValue("WorkspaceRecord",out var saved)&&saved is not null?JsonSerializer.Deserialize<JsonElement>(saved.ToString()!):default;
                var clientId=Text(record,"id");if(clientId.Length==0)clientId=tyre?.Id.ToString()??"";
                if(tyre is null||clientId!=move.TyreId||PositionKey(tyre.Position)!=PositionKey(move.FromPosition))return Conflict(new{message="A tyre or its current position changed. Refresh before rotating."});
                if(DateOnly.TryParse(Text(record,"fittedOn"),System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var originalFit)&&input.RotationDate<originalFit)return BadRequest(new{message="Rotation cannot be dated before a tyre's saved fitment date."});
                if(move.FromPosition!=move.ToPosition)snapshots.Add(new JsonObject{["tyreId"]=clientId,["reference"]=tyre.TyreNumber,["fromPosition"]=tyre.Position,["toPosition"]=move.ToPosition});
            }
            JsonElement? jobItem=null;
            if(!string.IsNullOrEmpty(input.JobCardId)||!string.IsNullOrEmpty(input.ItemId)){
                jobItem=await JobWorkActions.Item(db,input.JobCardId??"",input.ItemId??"",vehicle.RegistrationNumber,"Rotation",performedDate:input.RotationDate);
            }
            // Release every position inside this transaction, then assign the final unique layout.
            foreach(var tyre in fitted)db.Entry(tyre).Property(t=>t.IsActive).CurrentValue=false;
            await db.SaveChangesAsync();
            foreach(var move in moves) {
                var tyre=fitted.Single(t=>t.Id==CoreWorkspace.EntityId("tyre",move.TyreId));tyre.MoveTo(move.ToPosition);
                db.Entry(tyre).Property(t=>t.IsActive).CurrentValue=true;tyre.ExtraProperties["RotationRecordId"]=input.RequestId.ToString();
            }
            vehicle.UpdateOdometer(input.Odometer);
            var rotation=new TyreRotation{Id=input.RequestId,VehicleId=vehicle.Id,ActorId=actor.Id,ActorName=actor.Name,RequestHash=hash,MovesJson=snapshots.ToJsonString(),RotationDate=input.RotationDate,OdometerKm=input.Odometer,Notes=notes.Length>0?notes:null,RecordedAt=DateTime.UtcNow};db.TyreRotations.Add(rotation);
            if(jobItem is not null){var work=JobWorkActions.Record(input.RequestId,input.JobCardId!,input.ItemId!,vehicle.Id,jobItem.Value,"Rotation",actor.Id,actor.Name,input.RotationDate,input.Odometer,notes,snapshots.ToJsonString(),hash);work.RotationId=rotation.Id;db.JobWorkRecords.Add(work);}
            var audit=await db.WorkspaceDocuments.FindAsync("tyretrack.lifecycle.audit");if(audit is null){audit=new WorkspaceDocument{Key="tyretrack.lifecycle.audit",Json="[]"};db.WorkspaceDocuments.Add(audit);}
            var entries=JsonNode.Parse(audit.Json)!.AsArray();entries.Insert(0,new JsonObject{["id"]="AUD-ROT-"+input.RequestId.ToString("N"),["at"]=rotation.RecordedAt.ToString("O"),["actor"]=actor.Name,["actorId"]=actor.Id.ToString(),["action"]="Tyres rotated",["entityType"]="Vehicle",["entityId"]=input.VehicleId,["registration"]=vehicle.RegistrationNumber,["details"]=$"{snapshots.Count} tyres rotated at {input.Odometer} km.",["rotationId"]=rotation.Id.ToString()});audit.Json=entries.ToJsonString();audit.Version++;audit.UpdatedAt=DateTime.UtcNow;
            await db.SaveChangesAsync();await CoreWorkspace.RefreshAsync(db);var result=await RotationResponse(rotation.Id,actor,false);await transaction.CommitAsync();return result;
        }
        catch(Exception ex) when(ex is DbUpdateConcurrencyException||IsDatabaseConflict(ex)){await transaction.RollbackAsync();db.ChangeTracker.Clear();return Conflict(new{message="Another tyre operation saved first. Refresh and retry with the same request key."});}
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or JsonException){await transaction.RollbackAsync();db.ChangeTracker.Clear();return BadRequest(new{message="Check the rotation layout and saved vehicle records."});}
    }
    private async Task<IActionResult> RotationResponse(Guid id,StaffAccount actor,bool replayed) {
        return Ok(new{rotationId=id,replayed,updates=await UpdatesAsync(actor)});
    }
    [HttpPost]
    public Task<IActionResult> Fit(FitInput input)=>Execute(input,false);
    private async Task<IActionResult> Execute(FitInput input,bool removalOnly) {
        if(User.FindFirstValue("kind")!="staff"||!Guid.TryParse(User.FindFirstValue(AbpClaimTypes.UserId),out var actorId))return Forbid();
        var reference=Clean(input.Reference).ToUpperInvariant();
        var position=Positions.FirstOrDefault(x=>PositionKey(x)==PositionKey(input.Position??""));
        var sku=Clean(input.Sku);var brand=Clean(input.Brand);var model=Clean(input.Model);var size=Clean(input.Size);var notes=Clean(input.Notes);
        var removeTyreId=Clean(input.RemoveTyreId);var reason=Clean(input.RemovalReason);var destination=Clean(input.Destination);
        if(removalOnly&&removeTyreId.Length==0)return BadRequest(new{message="Choose the fitted tyre to remove."});
        if(removeTyreId.Length>0&&(removeTyreId.Length>64||reason.Length is <1 or >200||!Destinations.Contains(destination)))return BadRequest(new{message="Enter a removal reason and choose a valid destination."});
        var today=DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2)).DateTime);
        if(input.RequestId==Guid.Empty||string.IsNullOrWhiteSpace(input.VehicleId)||input.VehicleId.Length>64)return BadRequest(new{message="A request key and registered vehicle are required."});
        if(position is null)return BadRequest(new{message="Choose a supported wheel position, including Spare."});
        if(!removalOnly&&(reference.Length is <1 or >64||!Regex.IsMatch(reference,@"^[A-Z0-9][A-Z0-9 ._/-]*$")))return BadRequest(new{message="Enter a tyre serial/reference of up to 64 letters, digits or common separators."});
        if(input.FitmentDate<new DateOnly(1900,1,1)||input.FitmentDate>today)return BadRequest(new{message="Enter a valid fitment date that is not in the future."});
        if(input.Odometer<0||input.Odometer>int.MaxValue)return BadRequest(new{message="Enter whole odometer kilometres from 0 to 2,147,483,647."});
        if(input.InitialTread is decimal tread&&(tread<=0||tread>40||decimal.Round(tread,1)!=tread))return BadRequest(new{message="Initial tread must be above 0 and at most 40 mm, with at most one decimal place, or left empty."});
        if(sku.Length>64||notes.Length>500)return BadRequest(new{message="Stock SKU must be at most 64 characters and notes at most 500 characters."});
        if(!removalOnly&&sku.Length==0&&(brand.Length is <1 or >100||model.Length is <1 or >100||size.Length is <1 or >32))return BadRequest(new{message="Enter brand, model and size for an externally supplied tyre."});
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{input.JobCardId,input.ItemId,removalOnly,removeTyreId,reason,destination,input.VehicleId,position,reference,input.FitmentDate,input.Odometer,input.InitialTread,sku=sku.ToUpperInvariant(),brand=sku.Length==0?brand:null,model=sku.Length==0?model:null,size=sku.Length==0?size:null,notes}))));
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try {
            var actor=await db.StaffAccounts.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==actorId);
            if(actor is null||actor.Role is not ("system-admin" or "branch-manager" or "service-advisor" or "technician")||!User.IsInRole(actor.Role))return Forbid();
            var vehicleId=CoreWorkspace.EntityId("vehicle",input.VehicleId);
            var vehicle=await db.Vehicles.SingleOrDefaultAsync(x=>x.Id==vehicleId);
            if(vehicle is null)return NotFound(new{message="Vehicle not found."});
            var stored=vehicle.ExtraProperties.TryGetValue("WorkspaceRecord",out var raw)&&raw is not null?JsonSerializer.Deserialize<JsonElement>(raw.ToString()!):JsonSerializer.SerializeToElement(new{id=vehicle.Id.ToString()});
            if(Text(stored,"id")!=input.VehicleId||!new VehicleWorkspaceAccess(actor).Owns(stored))return Forbid();
            if(await db.TyreRotations.AnyAsync(r=>r.Id==input.RequestId))return Conflict(new{message="This request key was already used for a rotation."});
            var previousRemoval=await db.TyreRemovals.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==input.RequestId);
            if(previousRemoval is not null) {
                if(previousRemoval.ActorId!=actor.Id||previousRemoval.RequestHash!=hash)return Conflict(new{message="This request key was used for different details."});
                var passport=await db.Tyres.AsNoTracking().SingleAsync(t=>t.Id==(previousRemoval.ReplacementTyreId??previousRemoval.TyreId));
                var passportId=passport.ExtraProperties.TryGetValue("WorkspaceRecord",out var savedRecord)&&savedRecord is not null?Text(JsonSerializer.Deserialize<JsonElement>(savedRecord.ToString()!),"id"):passport.Id.ToString();
                await CoreWorkspace.RefreshAsync(db);var replay=await ResponseAsync(new TyreFitment{TyreId=passport.Id,Id=previousRemoval.Id},actor,true,passportId);await transaction.CommitAsync();return replay;
            }
            var existing=await db.TyreFitments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==input.RequestId);
            if(existing is not null) {
                if(existing.ActorId!=actor.Id||existing.RequestHash!=hash)return Conflict(new{message="This request key was already used for a different fitment. Refresh before starting another."});
                await CoreWorkspace.RefreshAsync(db);var replay=await ResponseAsync(existing,actor,true);await transaction.CommitAsync();
                return replay;
            }
            if(!removalOnly&&!vehicle.IsActive)return BadRequest(new{message="Reactivate the vehicle before fitting a tyre."});
            if(input.Odometer<vehicle.OdometerKm)return BadRequest(new{message="Fitment odometer cannot be below the vehicle's current saved mileage."});
            var branchId=Text(stored,"branchId");var branches=await Rows("tyretrack.demo.branches");
            if(!branches.Any(x=>Text(x,"id")==branchId&&Text(x,"status")!="Inactive"))return BadRequest(new{message="Assign the vehicle to an active registered branch before fitment."});
            var latestWorkshopInspection=await db.WorkshopInspections.Where(w=>w.VehicleId==vehicle.Id).MaxAsync(w=>(DateOnly?)w.InspectionDate);
            var latestRotation=await db.TyreRotations.Where(r=>r.VehicleId==vehicle.Id).MaxAsync(r=>(DateOnly?)r.RotationDate);
            if(input.FitmentDate<latestWorkshopInspection||input.FitmentDate<latestRotation)return BadRequest(new{message="This action cannot be dated before the vehicle's latest rotation."});
            Tyre? removedTyre=null;
            if(removeTyreId.Length>0) {
                removedTyre=await db.Tyres.SingleOrDefaultAsync(t=>t.Id==CoreWorkspace.EntityId("tyre",removeTyreId));
                var oldRecord=removedTyre is not null&&removedTyre.ExtraProperties.TryGetValue("WorkspaceRecord",out var oldRaw)&&oldRaw is not null?JsonSerializer.Deserialize<JsonElement>(oldRaw.ToString()!):removedTyre is null?default:JsonSerializer.SerializeToElement(new{id=removedTyre.Id.ToString()});
                if(removedTyre is null||removedTyre.VehicleId!=vehicle.Id||Text(oldRecord,"id")!=removeTyreId)return NotFound(new{message="That tyre does not belong to this vehicle."});
                if(!removedTyre.IsActive||PositionKey(removedTyre.Position)!=PositionKey(position))return Conflict(new{message="The tyre is no longer fitted at this position. Refresh the vehicle."});
                var fittedDate=await db.TyreFitments.Where(f=>f.TyreId==removedTyre.Id).Select(f=>(DateOnly?)f.FitmentDate).FirstOrDefaultAsync();
                if(fittedDate is null&&DateOnly.TryParseExact(Text(oldRecord,"fittedOn"),"yyyy-MM-dd",out var legacyDate))fittedDate=legacyDate;
                if(fittedDate is not null&&input.FitmentDate<fittedDate)return BadRequest(new{message="Removal cannot be dated before the tyre's fitment."});
            }
            JsonElement? jobItem=null;
            if(!string.IsNullOrEmpty(input.JobCardId)||!string.IsNullOrEmpty(input.ItemId)){
                if(removalOnly)throw new ArgumentException("A replacement job item requires both removal and new fitment.");
                jobItem=await JobWorkActions.Item(db,input.JobCardId??"",input.ItemId??"",vehicle.RegistrationNumber,"Tyre replacement",removeTyreId,position,sku,input.FitmentDate);
            }
            var positionKey=PositionKey(position);var referenceKey=ReferenceKey(reference);
            if(!removalOnly&&await db.Tyres.AnyAsync(x=>x.VehicleId==vehicle.Id&&x.IsActive&&(removedTyre==null||x.Id!=removedTyre.Id)&&EF.Property<string>(x,"PositionKey")==positionKey))return Conflict(new{message="That wheel position is occupied. Record the current tyre's removal before fitting another."});
            if(!removalOnly&&await db.Tyres.IgnoreQueryFilters().AnyAsync(x=>EF.Property<string>(x,"ReferenceKey")==referenceKey))return Conflict(new{message="This tyre reference already exists. Open its existing passport."});
            if(!removalOnly&&sku.Length>0) {
                var stockDocument=await db.WorkspaceDocuments.FindAsync("tyretrack.demo.stock");
                var stock=stockDocument is null?new JsonArray():JsonNode.Parse(stockDocument.Json) as JsonArray??new JsonArray();
                var selected=stock.OfType<JsonObject>().Where(row=>{
                    var item=JsonSerializer.SerializeToElement(row);
                    var id=Text(item,"branchId");
                    if(id.Length==0) {
                        var matches=branches.Where(x=>Text(x,"name")==Text(item,"branch")).ToArray();
                        id=matches.Length==1?Text(matches[0],"id"):"";
                    }
                    return Text(item,"sku").Equals(sku,StringComparison.OrdinalIgnoreCase)&&id==branchId;
                }).ToArray();
                if(selected.Length!=1)return BadRequest(new{message="Choose one stock item from the vehicle's branch."});
                var item=selected[0];var quantity=item["quantity"]?.GetValue<int>()??0;
                if(quantity<=0)return Conflict(new{message="This stock item is no longer available. Refresh stock before retrying."});
                brand=Clean(item["brand"]?.GetValue<string>());model=Clean(item["model"]?.GetValue<string>());size=Clean(item["size"]?.GetValue<string>());
                if(brand.Length is <1 or >100||model.Length is <1 or >100||size.Length is <1 or >32)return BadRequest(new{message="The stock item's brand, model or size needs correction before fitment."});
                sku=item["sku"]!.GetValue<string>();item["quantity"]=quantity-1;item["updatedAt"]=DateTime.UtcNow.ToString("O");
                stockDocument!.Json=stock.ToJsonString();stockDocument.Version++;stockDocument.UpdatedAt=DateTime.UtcNow;
            }
            if(removedTyre is not null) {
                removedTyre.Retire();removedTyre.ExtraProperties["RemovalRecordId"]=input.RequestId.ToString();
                // Release the filtered unique position before inserting its replacement, within this transaction.
                await db.SaveChangesAsync();
                db.TyreRemovals.Add(new TyreRemoval{Id=input.RequestId,TyreId=removedTyre.Id,VehicleId=vehicle.Id,ActorId=actor.Id,ActorName=actor.Name,RequestHash=hash,Position=removedTyre.Position,Reason=reason,Destination=destination,Notes=notes.Length>0?notes:null,RemovalDate=input.FitmentDate,OdometerKm=input.Odometer,RecordedAt=DateTime.UtcNow});
            }
            var tyreId=removalOnly?removedTyre!.Id:Guid.NewGuid();
            var tyre=removalOnly?removedTyre!:new Tyre(tyreId,vehicle.Id,reference,brand,model,size,position,input.Odometer);
            if(!removalOnly&&input.InitialTread is decimal initial)tyre.RecordTread(initial,TyreHealthStatus.Unknown);
            if(!removalOnly) {
            tyre.ExtraProperties["FitmentRecordId"]=input.RequestId.ToString();
            tyre.ExtraProperties["WorkspaceRecord"]=new JsonObject{["id"]=tyreId.ToString(),["fittedOn"]=input.FitmentDate.ToString("yyyy-MM-dd"),["fitmentRecordId"]=input.RequestId.ToString()}.ToJsonString();
            }
            var fitment=new TyreFitment{Id=input.RequestId,TyreId=tyreId,VehicleId=vehicle.Id,ActorId=actor.Id,ActorName=actor.Name,RequestHash=hash,BranchId=branchId,Position=position,FitmentDate=input.FitmentDate,OdometerKm=input.Odometer,InitialTreadMm=input.InitialTread,StockSku=sku.Length>0?sku:null,Notes=notes.Length>0?notes:null,RecordedAt=DateTime.UtcNow};
            if(!removalOnly) {
                db.Tyres.Add(tyre);db.TyreFitments.Add(fitment);
                if(removedTyre is not null)db.TyreRemovals.Local.Single(r=>r.Id==input.RequestId).ReplacementTyreId=tyreId;
            }
            if(jobItem is not null){
                var oldRecord=removedTyre!.ExtraProperties.TryGetValue("WorkspaceRecord",out var oldRaw)&&oldRaw is not null?JsonSerializer.Deserialize<JsonElement>(oldRaw.ToString()!):default;
                var snapshot=new JsonArray(new JsonObject{["tyreId"]=removeTyreId,["reference"]=removedTyre.TyreNumber,["position"]=position,["role"]="Removed"},new JsonObject{["tyreId"]=tyreId.ToString(),["reference"]=reference,["position"]=position,["role"]="Fitted"});
                var work=JobWorkActions.Record(input.RequestId,input.JobCardId!,input.ItemId!,vehicle.Id,jobItem.Value,"Tyre replacement",actor.Id,actor.Name,input.FitmentDate,input.Odometer,notes,snapshot.ToJsonString(),hash);work.FitmentId=fitment.Id;db.JobWorkRecords.Add(work);
            }
            vehicle.UpdateOdometer(input.Odometer);
            var audit=await db.WorkspaceDocuments.FindAsync("tyretrack.lifecycle.audit");
            if(audit is null){audit=new WorkspaceDocument{Key="tyretrack.lifecycle.audit",Json="[]"};db.WorkspaceDocuments.Add(audit);}
            var entries=JsonNode.Parse(audit.Json) as JsonArray??new JsonArray();
            entries.Insert(0,new JsonObject{["id"]="AUD-FIT-"+input.RequestId.ToString("N"),["at"]=fitment.RecordedAt.ToString("O"),["actor"]=actor.Name,["actorId"]=actor.Id.ToString(),["action"]=removalOnly?"Tyre removed":removedTyre is null?"Tyre fitted":"Tyre replaced",["entityType"]="Tyre",["entityId"]=removalOnly?removeTyreId:tyreId.ToString(),["registration"]=vehicle.RegistrationNumber,["details"]=removedTyre is null?$"{reference} fitted at {position}, {input.Odometer} km.":$"{removedTyre.TyreNumber} removed at {position}, {input.Odometer} km. {reason}; destination: {destination}.", ["fitmentId"]=input.RequestId.ToString()});
            audit.Json=entries.ToJsonString();audit.Version++;audit.UpdatedAt=DateTime.UtcNow;
            await db.SaveChangesAsync();await CoreWorkspace.RefreshAsync(db);var result=await ResponseAsync(fitment,actor,false,removalOnly?removeTyreId:null);await transaction.CommitAsync();
            return result;
        }
        catch(DbUpdateConcurrencyException){await transaction.RollbackAsync();db.ChangeTracker.Clear();return Conflict(new{message="Another session saved first. Retry this fitment with the same request key."});}
        catch(DbUpdateException ex) when(ex.InnerException is SqlException sql&&sql.Number is 2601 or 2627 or 1205){await transaction.RollbackAsync();db.ChangeTracker.Clear();return Conflict(new{message="The tyre reference, wheel position or stock changed in another session. Refresh and retry."});}
        catch(SqlException ex) when(ex.Number==1205){await transaction.RollbackAsync();db.ChangeTracker.Clear();return Conflict(new{message="Another fitment was saved at the same time. Retry with the same request key."});}
        catch(Exception ex) when(IsDatabaseConflict(ex)){await transaction.RollbackAsync();db.ChangeTracker.Clear();return Conflict(new{message="Another operation changed this tyre or stock. Refresh and retry with the same request key."});}
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or JsonException){HttpContext.RequestServices.GetRequiredService<ILogger<TyreFitmentsController>>().LogWarning(ex,"Fitment action rejected: {Reason}",ex.Message);await transaction.RollbackAsync();db.ChangeTracker.Clear();return BadRequest(new{message="Check the fitment details and stock configuration before retrying."});}
    }
}
