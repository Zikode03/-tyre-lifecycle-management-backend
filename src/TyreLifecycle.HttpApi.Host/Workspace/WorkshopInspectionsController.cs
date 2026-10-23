using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TyreLifecycle.EntityFrameworkCore;
using TyreLifecycle.Inspections;
using TyreLifecycle.Tyres;
using TyreLifecycle.Workspace;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
namespace TyreLifecycle.HttpApi.Host.Workspace;

[ApiController,Route("api/v1/workshop-inspections"),Authorize,IgnoreAntiforgeryToken,UnitOfWork(IsDisabled=true)]
public class WorkshopInspectionsController(TyreLifecycleDbContext db):ControllerBase
{
 public record ReadingInput(string TyreId,string Position,[property:JsonRequired] decimal Inner,[property:JsonRequired] decimal Centre,[property:JsonRequired] decimal Outer,[property:JsonRequired] decimal Pressure,string WearPattern,string[]? Defects,string Assessment,string Recommendation,string? Notes,string[]? Photos);
 public record CompleteInput(Guid RequestId,string VehicleId,DateOnly InspectionDate,long Odometer,List<ReadingInput>? Readings,bool CreateQuote=false);
 private static readonly string[] Wear=["Even","Inner-edge wear","Outer-edge wear","Centre wear","Cupping"];
 private static readonly string[] Defects=["Puncture","Sidewall cut","Bulge","Cracking","Exposed cords"];
 private static readonly string[] Actions=["Monitor","Investigate/repair","Alignment check","Replacement assessment"];
 private static readonly string[] Keys=["tyretrack.demo.inspections","tyretrack.data.tyres","tyretrack.demo.vehicles","tyretrack.demo.jobCards","tyretrack.lifecycle.audit"];
 private static string Text(JsonElement item,string key)=>item.ValueKind==JsonValueKind.Object&&item.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()!:"";
 private static string Id(Tyre tyre)=>tyre.ExtraProperties.TryGetValue("WorkspaceRecord",out var raw)&&raw is not null?Text(JsonSerializer.Deserialize<JsonElement>(raw.ToString()!),"id"):tyre.Id.ToString();
 private static string PositionKey(string value)=>value.Replace(" ","").Replace("-","").ToUpperInvariant();
 private static bool ConflictError(Exception ex)=>ex is DbUpdateConcurrencyException||ex is SqlException sql&&sql.Number is 1205 or 2601 or 2627||ex.InnerException is not null&&ConflictError(ex.InnerException);
 private static int Rank(string value)=>value switch{"Good"=>0,"Attention"=>1,_=>2};
 private static string Assessment(ReadingInput r){var min=Math.Min(r.Inner,Math.Min(r.Centre,r.Outer));var floor=min<=2.2m||r.Defects!.Any(x=>x is "Bulge" or "Sidewall cut" or "Exposed cords")?2:min<=3.5m||r.WearPattern!="Even"||r.Defects!.Length>0?1:0;return new[]{"Good","Attention","Critical"}[Math.Max(floor,Rank(r.Assessment))];}
 private async Task<IActionResult> SavedResponse(WorkshopInspection receipt,StaffAccount actor,bool replayed){
  var docs=await db.WorkspaceDocuments.AsNoTracking().ToListAsync();var access=new LinkedWorkspaceAccess(actor,docs);
  return Ok(new{visitId=receipt.Id,inspectionIds=JsonSerializer.Deserialize<string[]>(receipt.InspectionIdsJson),jobCardId=receipt.JobCardId,replayed,updates=docs.Where(d=>Keys.Contains(d.Key)&&(actor.Role=="system-admin"||access.Readable(d.Key))).ToDictionary(d=>d.Key,d=>new{data=actor.Role=="system-admin"?JsonSerializer.Deserialize<JsonElement>(d.Json):JsonSerializer.SerializeToElement(access.Filter(d.Key,d.Json)),d.Version})});
 }
 [HttpPost,RequestSizeLimit(2_000_000)]
 public async Task<IActionResult> Complete(CompleteInput input){
  if(User.FindFirstValue("kind")!="staff"||!Guid.TryParse(User.FindFirstValue(AbpClaimTypes.UserId),out var actorId))return Forbid();
  if(input.RequestId==Guid.Empty||string.IsNullOrWhiteSpace(input.VehicleId)||input.VehicleId.Length>64||input.Odometer<0||input.Odometer>int.MaxValue||input.InspectionDate<new DateOnly(1900,1,1)||input.InspectionDate>DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2)))return BadRequest(new{message="Choose a registered vehicle, valid inspection date and whole odometer kilometres."});
  if(input.Readings is null||input.Readings.Count is <1 or >5||input.Readings.Any(r=>r is null||string.IsNullOrWhiteSpace(r.TyreId)||r.TyreId.Length>64||string.IsNullOrWhiteSpace(r.Position)||r.Position.Length>32))return BadRequest(new{message="Inspect each currently fitted tyre, including its identity and position."});
  var rows=input.Readings.OrderBy(r=>r.TyreId,StringComparer.Ordinal).ToArray();var photoBytes=0;
  if(rows.Select(r=>r.TyreId).Distinct().Count()!=rows.Length)return BadRequest(new{message="Each tyre can appear once per inspection."});
  foreach(var r in rows){
   if(new[]{r.Inner,r.Centre,r.Outer}.Any(v=>v<0||v>40||decimal.Round(v,1)!=v)||r.Pressure<0||r.Pressure>200||decimal.Round(r.Pressure,1)!=r.Pressure)return BadRequest(new{message="Tread must be 0–40 mm and pressure 0–200 PSI, with at most one decimal place. Zero readings are allowed."});
   if(!Wear.Contains(r.WearPattern)||r.Defects is null||r.Defects.Length>5||r.Defects.Distinct().Count()!=r.Defects.Length||r.Defects.Any(d=>!Defects.Contains(d))||r.Assessment is not ("Good" or "Attention" or "Critical")||!Actions.Contains(r.Recommendation)||(r.Notes?.Length??0)>1000)return BadRequest(new{message="Choose wear pattern, defects, technician assessment and recommended action; notes may be at most 1000 characters."});
   if(Assessment(r)=="Critical"&&r.Recommendation!="Replacement assessment")return BadRequest(new{message="Critical findings require a replacement assessment recommendation."});
   if(r.Photos is null||r.Photos.Length>2)return BadRequest(new{message="Attach at most two JPEG or PNG photos per tyre."});
   foreach(var photo in r.Photos){
    if(photo is null||photo.Length>200_100||!(photo.StartsWith("data:image/jpeg;base64,")||photo.StartsWith("data:image/png;base64,")))return BadRequest(new{message="Evidence must be a JPEG or PNG image under 150 KB."});
    byte[] bytes;try{bytes=Convert.FromBase64String(photo[(photo.IndexOf(',')+1)..]);}catch(FormatException){return BadRequest(new{message="Photo data is invalid."});}
    if(bytes.Length<8||bytes.Length>150_000||(photo.StartsWith("data:image/jpeg")?!(bytes[0]==255&&bytes[1]==216&&bytes[2]==255):!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})))return BadRequest(new{message="Photo format or size is invalid."});photoBytes+=bytes.Length;
   }
  }
  if(photoBytes>1_000_000)return BadRequest(new{message="Combined photo evidence must be under 1 MB per visit."});
  var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{input.VehicleId,input.InspectionDate,input.Odometer,input.CreateQuote,rows}))));
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  try{
   var actor=await db.StaffAccounts.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==actorId);if(actor is null||actor.Role is not ("system-admin" or "branch-manager" or "service-advisor" or "technician")||!User.IsInRole(actor.Role))return Forbid();
   var vehicle=await db.Vehicles.SingleOrDefaultAsync(v=>v.Id==CoreWorkspace.EntityId("vehicle",input.VehicleId));if(vehicle is null)return NotFound();
   var stored=vehicle.ExtraProperties.TryGetValue("WorkspaceRecord",out var raw)&&raw is not null?JsonSerializer.Deserialize<JsonElement>(raw.ToString()!):default;
   if(Text(stored,"id")!=input.VehicleId||!new VehicleWorkspaceAccess(actor).Owns(stored))return Forbid();
   var existing=await db.WorkshopInspections.SingleOrDefaultAsync(x=>x.Id==input.RequestId);if(existing is not null){if(existing.ActorId!=actor.Id||existing.RequestHash!=hash)return Conflict(new{message="This request key was used for different inspection details."});await CoreWorkspace.RefreshAsync(db);var replay=await SavedResponse(existing,actor,true);await tx.CommitAsync();return replay;}
   if(!vehicle.IsActive||input.Odometer<vehicle.OdometerKm)return BadRequest(new{message="Inspect an active vehicle and use mileage at least equal to the saved odometer."});
   var branchDoc=await db.WorkspaceDocuments.FindAsync("tyretrack.demo.branches");var branches=branchDoc is null?[]:JsonSerializer.Deserialize<JsonElement[]>(branchDoc.Json)!;
   if(!branches.Any(b=>Text(b,"id")==Text(stored,"branchId")&&Text(b,"status")!="Inactive"))return BadRequest(new{message="Assign an active registered branch to the vehicle first."});
   var lastFit=await db.TyreFitments.Where(f=>f.VehicleId==vehicle.Id).MaxAsync(f=>(DateOnly?)f.FitmentDate);var lastRotation=await db.TyreRotations.Where(f=>f.VehicleId==vehicle.Id).MaxAsync(f=>(DateOnly?)f.RotationDate);var lastRemoval=await db.TyreRemovals.Where(f=>f.VehicleId==vehicle.Id).MaxAsync(f=>(DateOnly?)f.RemovalDate);var lastInspection=await db.WorkshopInspections.Where(f=>f.VehicleId==vehicle.Id).MaxAsync(f=>(DateOnly?)f.InspectionDate);
   if(input.InspectionDate<lastFit||input.InspectionDate<lastRotation||input.InspectionDate<lastRemoval||input.InspectionDate<lastInspection)return BadRequest(new{message="Inspection cannot be dated before a recorded tyre action on this vehicle."});
   var fitted=await db.Tyres.Where(t=>t.VehicleId==vehicle.Id&&t.IsActive).ToListAsync();if(fitted.Count!=rows.Length)return Conflict(new{message="The fitted tyres changed. Refresh before completing the inspection."});
   var owner=await db.Customers.SingleAsync(c=>c.Id==vehicle.CustomerId);var customerName=(owner.FirstName+" "+owner.LastName).Trim().TrimEnd('—').Trim();var ids=new List<string>();var items=new JsonArray();
   foreach(var r in rows){
    var tyre=fitted.FirstOrDefault(t=>Id(t)==r.TyreId);if(tyre is null||PositionKey(tyre.Position)!=PositionKey(r.Position))return Conflict(new{message="A tyre or its wheel position changed. Refresh before saving."});
    var tyreRecord=tyre.ExtraProperties.TryGetValue("WorkspaceRecord",out var saved)&&saved is not null?JsonSerializer.Deserialize<JsonElement>(saved.ToString()!):default;
    if(DateOnly.TryParse(Text(tyreRecord,"fittedOn"),System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var originalFit)&&input.InspectionDate<originalFit)return BadRequest(new{message="Inspection cannot be dated before the tyre's saved fitment."});
    var inspectionId="WI-"+Guid.NewGuid().ToString("N")[..28];var inspection=new Inspection(Guid.NewGuid(),inspectionId,vehicle.Id,input.Odometer);inspection.AssignTechnician(actor.Id);inspection.Start(DateTime.UtcNow);
    inspection.AddOrUpdateReading(Guid.NewGuid(),tyre.Position,MeasurementSource.ManualGauge,r.Inner,r.Centre,r.Outer,r.Pressure,tyre.Id,r.WearPattern,JsonSerializer.Serialize(r.Defects),r.Recommendation);inspection.Complete(DateTime.UtcNow,r.Notes);
    var status=Assessment(r);var record=new JsonObject{["id"]=inspectionId,["registration"]=vehicle.RegistrationNumber,["customer"]=customerName,["technician"]=actor.Name,["status"]="Completed",["tyreId"]=Id(tyre),["inspectionDate"]=input.InspectionDate.ToString("yyyy-MM-dd"),["position"]=tyre.Position,["inner"]=r.Inner,["centre"]=r.Centre,["outer"]=r.Outer,["pressure"]=r.Pressure,["wearPattern"]=r.WearPattern,["defects"]=JsonSerializer.SerializeToNode(r.Defects),["assessment"]=status,["technicianAssessment"]=r.Assessment,["recommendation"]=r.Recommendation,["measurementSource"]="Manual gauge",["notes"]=r.Notes,["photos"]=JsonSerializer.SerializeToNode(r.Photos),["workshopVisitId"]=input.RequestId.ToString()};
    inspection.ExtraProperties["WorkspaceRecord"]=record.ToJsonString();inspection.ExtraProperties["WorkshopInspectionId"]=input.RequestId.ToString();db.Inspections.Add(inspection);ids.Add(inspectionId);
    tyre.ExtraProperties["WorkshopInspectionId"]=inspectionId;
    tyre.RecordTread(Math.Min(r.Inner,Math.Min(r.Centre,r.Outer)),Enum.Parse<TyreHealthStatus>(status));
    if(r.Recommendation!="Monitor")items.Add(new JsonObject{["id"]="REC-"+inspectionId,["type"]=r.Recommendation=="Replacement assessment"?"Tyre replacement":r.Recommendation=="Alignment check"?"Alignment":"Inspection",["description"]=$"{r.Recommendation}: {tyre.Position} — {tyre.Brand} {tyre.Model} {tyre.Size}",["position"]=tyre.Position,["tyreId"]=Id(tyre),["quantity"]=1,["unitPrice"]=0,["approved"]=false});
   }
   var receipt=new WorkshopInspection{Id=input.RequestId,VehicleId=vehicle.Id,ActorId=actor.Id,RequestHash=hash,InspectionIdsJson=JsonSerializer.Serialize(ids),InspectionDate=input.InspectionDate,RecordedAt=DateTime.UtcNow};
   if(input.CreateQuote&&items.Count>0){
    receipt.JobCardId="WJOB-"+input.RequestId.ToString("N");var jobDoc=await db.WorkspaceDocuments.FindAsync("tyretrack.demo.jobCards");if(jobDoc is null){jobDoc=new WorkspaceDocument{Key="tyretrack.demo.jobCards",Json="[]"};db.WorkspaceDocuments.Add(jobDoc);}var cards=JsonNode.Parse(jobDoc.Json)!.AsArray();
    cards.Insert(0,new JsonObject{["id"]=receipt.JobCardId,["registration"]=vehicle.RegistrationNumber,["customer"]=customerName,["customerId"]=owner.CustomerNumber,["branch"]=Text(stored,"branchId"),["inspectionIds"]=JsonSerializer.SerializeToNode(ids),["createdAt"]=DateTime.UtcNow.ToString("O"),["status"]="Draft",["quoteNumber"]="WQ-"+input.RequestId.ToString("N"),["quoteDecision"]="Pending",["items"]=items,["vatRate"]=15,["workshopVisitId"]=receipt.Id.ToString()});jobDoc.Json=cards.ToJsonString();jobDoc.Version++;jobDoc.UpdatedAt=DateTime.UtcNow;
   }
   db.WorkshopInspections.Add(receipt);vehicle.UpdateOdometer(input.Odometer);
   var audit=await db.WorkspaceDocuments.FindAsync("tyretrack.lifecycle.audit");if(audit is null){audit=new WorkspaceDocument{Key="tyretrack.lifecycle.audit",Json="[]"};db.WorkspaceDocuments.Add(audit);}var entries=JsonNode.Parse(audit.Json)!.AsArray();entries.Insert(0,new JsonObject{["id"]="AUD-WI-"+receipt.Id.ToString("N"),["at"]=receipt.RecordedAt.ToString("O"),["actor"]=actor.Name,["actorId"]=actor.Id.ToString(),["action"]="Workshop inspection completed",["entityType"]="Vehicle",["entityId"]=input.VehicleId,["registration"]=vehicle.RegistrationNumber,["details"]=$"{rows.Length} fitted tyres inspected manually at {input.Odometer} km.",["workshopVisitId"]=receipt.Id.ToString()});audit.Json=entries.ToJsonString();audit.Version++;audit.UpdatedAt=DateTime.UtcNow;
   await db.SaveChangesAsync();await CoreWorkspace.RefreshAsync(db);var result=await SavedResponse(receipt,actor,false);await tx.CommitAsync();return result;
  }catch(Exception ex) when(ConflictError(ex)){await tx.RollbackAsync();db.ChangeTracker.Clear();return Conflict(new{message="Another operation saved first. Refresh and retry this inspection with the same request key."});}
  catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or JsonException){await tx.RollbackAsync();db.ChangeTracker.Clear();return BadRequest(new{message="Check the inspection details and saved vehicle records."});}
 }
}
