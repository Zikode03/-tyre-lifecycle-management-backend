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
using TyreLifecycle.Vehicles;
using TyreLifecycle.Workspace;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
namespace TyreLifecycle.HttpApi.Host.Workspace;
[ApiController,Route("api/v1/job-work"),Authorize,IgnoreAntiforgeryToken,UnitOfWork(IsDisabled=true)]
public class JobWorkController(TyreLifecycleDbContext db):ControllerBase
{
 public record JobInput(string JobCardId);
 public record CompleteInput(Guid RequestId,string JobCardId);
 public record ServiceInput(Guid RequestId,string JobCardId,string ItemId,DateOnly PerformedDate,[property:JsonRequired] long Odometer,string Notes);
 private static string Text(JsonElement e,string key)=>JobWorkActions.Text(e,key);
 private static bool ConflictError(Exception ex)=>ex is DbUpdateConcurrencyException||ex is SqlException sql&&sql.Number is 1205 or 2601 or 2627||ex.InnerException is not null&&ConflictError(ex.InnerException);
 private async Task<(StaffAccount Actor,Vehicle Vehicle,WorkspaceDocument Document,JsonArray Rows,JsonObject Job)> Context(string id){
  if(User.FindFirstValue("kind")!="staff"||!Guid.TryParse(User.FindFirstValue(AbpClaimTypes.UserId),out var actorId))throw new UnauthorizedAccessException();
  var actor=await db.StaffAccounts.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==actorId);if(actor is null||actor.Role is not ("system-admin" or "branch-manager" or "service-advisor" or "technician")||!User.IsInRole(actor.Role))throw new UnauthorizedAccessException();
  if(string.IsNullOrWhiteSpace(id)||id.Length>64)throw new ArgumentException("Choose a job card.");
  var doc=await db.WorkspaceDocuments.FindAsync("tyretrack.demo.jobCards")??throw new ArgumentException("Job unavailable.");var rows=JsonNode.Parse(doc.Json)!.AsArray();var job=rows.OfType<JsonObject>().FirstOrDefault(j=>j["id"]?.GetValue<string>()==id)??throw new ArgumentException("Job unavailable.");var registration=job["registration"]?.GetValue<string>();
  var vehicle=await db.Vehicles.SingleOrDefaultAsync(v=>v.RegistrationNumber==registration)??throw new ArgumentException("Job vehicle unavailable.");
  var record=vehicle.ExtraProperties.TryGetValue("WorkspaceRecord",out var raw)&&raw is not null?JsonSerializer.Deserialize<JsonElement>(raw.ToString()!):default;
  if(!new VehicleWorkspaceAccess(actor).Owns(record))throw new UnauthorizedAccessException();return(actor,vehicle,doc,rows,job);
 }
 private async Task<IActionResult> Saved(string jobId,StaffAccount actor){await CoreWorkspace.RefreshAsync(db);var docs=await db.WorkspaceDocuments.AsNoTracking().ToListAsync();var scope=new LinkedWorkspaceAccess(actor,docs);return Ok(new{jobCardId=jobId,updates=docs.Where(d=>new[]{"tyretrack.demo.jobCards","tyretrack.demo.jobWork","tyretrack.demo.vehicles","tyretrack.lifecycle.audit"}.Contains(d.Key)&&(actor.Role=="system-admin"||scope.Readable(d.Key))).ToDictionary(d=>d.Key,d=>new{data=actor.Role=="system-admin"?JsonSerializer.Deserialize<JsonElement>(d.Json):JsonSerializer.SerializeToElement(scope.Filter(d.Key,d.Json)),d.Version})});}
 private static void SaveDocument(WorkspaceDocument doc,JsonArray rows){doc.Json=rows.ToJsonString();doc.Version++;doc.UpdatedAt=DateTime.UtcNow;}
 private async Task Audit(string jobId,string registration,StaffAccount actor,string action,string details){var doc=await db.WorkspaceDocuments.FindAsync("tyretrack.lifecycle.audit");if(doc is null){doc=new WorkspaceDocument{Key="tyretrack.lifecycle.audit",Json="[]"};db.WorkspaceDocuments.Add(doc);}var entries=JsonNode.Parse(doc.Json)!.AsArray();entries.Insert(0,new JsonObject{["id"]="AUD-WORK-"+Guid.NewGuid().ToString("N"),["at"]=DateTime.UtcNow.ToString("O"),["actor"]=actor.Name,["actorId"]=actor.Id.ToString(),["action"]=action,["entityType"]="Job card",["entityId"]=jobId,["registration"]=registration,["details"]=details});SaveDocument(doc,entries);}
 private async Task<IActionResult> Run(Func<Task<IActionResult>> work){await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);try{var result=await work();await tx.CommitAsync();return result;}catch(UnauthorizedAccessException){await tx.RollbackAsync();db.ChangeTracker.Clear();return Forbid();}catch(Exception ex) when(ConflictError(ex)||ex is InvalidOperationException){await tx.RollbackAsync();db.ChangeTracker.Clear();return Conflict(new{message="This work was already recorded or changed in another session. Refresh before retrying."});}catch(ArgumentException ex){await tx.RollbackAsync();db.ChangeTracker.Clear();return BadRequest(new{message=ex.Message});}}
 [HttpPost("start")]
 public Task<IActionResult> Start(JobInput input)=>Run(async()=>{
  var (actor,vehicle,doc,rows,job)=await Context(input.JobCardId);var data=JsonSerializer.SerializeToElement(job);
  if(Text(data,"workshopStartedAt")!="")return await Saved(input.JobCardId,actor);
  if(!vehicle.IsActive||Text(data,"quoteDecision")!="Approved"||Text(data,"status") is not ("Approved" or "In workshop"))throw new ArgumentException("Customer approval and an active vehicle are required before work starts.");
  var items=data.GetProperty("items").EnumerateArray().Where(JobWorkActions.Included).ToArray();if(items.Length==0)throw new ArgumentException("The approved quotation has no included work items.");
  if(items.Any(i=>string.IsNullOrWhiteSpace(Text(i,"id"))||Text(i,"id").Length>64||!i.TryGetProperty("quantity",out var q)||!q.TryGetDecimal(out var quantity)||quantity<=0)||items.Select(i=>Text(i,"id")).Distinct().Count()!=items.Length)throw new ArgumentException("Work items need unique references and positive quantities.");
  var tyres=await db.Tyres.Where(t=>t.VehicleId==vehicle.Id&&t.IsActive).ToListAsync();var expected=new JsonObject();
  foreach(var item in items.Where(i=>Text(i,"type")=="Tyre replacement")){
   if(!item.TryGetProperty("quantity",out var q)||!q.TryGetDecimal(out var quantity)||quantity!=1)throw new ArgumentException("Split replacements into one quoted tyre per item before customer approval.");
   var tyre=tyres.SingleOrDefault(t=>JobWorkActions.Position(t.Position)==JobWorkActions.Position(Text(item,"position")))??throw new ArgumentException("The quoted wheel position has no fitted tyre.");
   var saved=tyre.ExtraProperties.TryGetValue("WorkspaceRecord",out var raw)&&raw is not null?JsonSerializer.Deserialize<JsonElement>(raw.ToString()!):default;var id=Text(saved,"id");if(id.Length==0)id=tyre.Id.ToString();
   if(Text(item,"tyreId").Length>0&&Text(item,"tyreId")!=id)throw new ArgumentException("The inspected tyre changed. Revise the quotation before starting.");expected[Text(item,"id")]=id;
  }
  if(items.Where(i=>Text(i,"type")=="Tyre replacement").Select(i=>JobWorkActions.Position(Text(i,"position"))).Distinct().Count()!=expected.Count)throw new ArgumentException("A wheel position cannot appear in two replacement items.");
  job["status"]="In workshop";job["workshopStartedAt"]=DateTime.UtcNow.ToString("O");job["workshopExpectedTyres"]=expected;SaveDocument(doc,rows);await Audit(input.JobCardId,vehicle.RegistrationNumber,actor,"Workshop work started","Customer-approved tasks opened.");await db.SaveChangesAsync();return await Saved(input.JobCardId,actor);
 });
 [HttpPost("service")]
 public Task<IActionResult> Service(ServiceInput input)=>Run(async()=>{
  var (actor,vehicle,_,_,job)=await Context(input.JobCardId);var notes=input.Notes?.Trim()??"";
  if(input.RequestId==Guid.Empty||notes.Length is <1 or >1000||input.PerformedDate<new DateOnly(1900,1,1)||input.PerformedDate>DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2))||input.Odometer<0||input.Odometer>int.MaxValue)throw new ArgumentException("Enter a valid request, date, whole odometer and completion note (1–1000 characters).");
  var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{input.JobCardId,input.ItemId,input.PerformedDate,input.Odometer,notes}))));var existing=await db.JobWorkRecords.SingleOrDefaultAsync(w=>w.Id==input.RequestId);
  if(existing is not null){if(existing.ActorId!=actor.Id||existing.RequestHash!=hash)throw new InvalidOperationException();return await Saved(input.JobCardId,actor);}
  var item=job["items"]!.AsArray().OfType<JsonObject>().FirstOrDefault(i=>i["id"]?.GetValue<string>()==input.ItemId)??throw new ArgumentException("Work item unavailable.");var kind=item["type"]?.GetValue<string>()??"";
  if(kind is not ("Alignment" or "Balancing" or "Puncture repair" or "Inspection"))throw new ArgumentException("Record replacements and rotations using their tyre actions.");
  var approved=await JobWorkActions.Item(db,input.JobCardId,input.ItemId,vehicle.RegistrationNumber,kind);
  if(!vehicle.IsActive||input.Odometer<vehicle.OdometerKm||DateOnly.FromDateTime(DateTime.Parse(job["workshopStartedAt"]!.GetValue<string>()).ToUniversalTime().AddHours(2))>input.PerformedDate)throw new ArgumentException("Use an active vehicle, current odometer and a date after work started.");
  var tyres=await db.Tyres.Where(t=>t.VehicleId==vehicle.Id&&t.IsActive).ToListAsync();var position=Text(approved,"position");if(position.Length>0)tyres=tyres.Where(t=>JobWorkActions.Position(t.Position)==JobWorkActions.Position(position)).ToList();if(tyres.Count==0)throw new ArgumentException("No fitted tyres match this service item.");
  foreach(var tyre in tyres)tyre.ExtraProperties["JobWorkId"]=input.RequestId.ToString();
  var snapshot=new JsonArray(tyres.Select(t=>{var saved=t.ExtraProperties.TryGetValue("WorkspaceRecord",out var raw)&&raw is not null?JsonSerializer.Deserialize<JsonElement>(raw.ToString()!):default;return (JsonNode)new JsonObject{["tyreId"]=Text(saved,"id").Length>0?Text(saved,"id"):t.Id.ToString(),["reference"]=t.TyreNumber,["position"]=t.Position};}).ToArray());
  db.JobWorkRecords.Add(JobWorkActions.Record(input.RequestId,input.JobCardId,input.ItemId,vehicle.Id,approved,kind,actor.Id,actor.Name,input.PerformedDate,input.Odometer,notes,snapshot.ToJsonString(),hash));vehicle.UpdateOdometer(input.Odometer);await Audit(input.JobCardId,vehicle.RegistrationNumber,actor,"Service work recorded",kind+": "+notes);await db.SaveChangesAsync();return await Saved(input.JobCardId,actor);
 });
 [HttpPost("complete")]
 public Task<IActionResult> Complete(CompleteInput input)=>Run(async()=>{
  var (actor,vehicle,doc,rows,job)=await Context(input.JobCardId);if(input.RequestId==Guid.Empty)throw new ArgumentException("A completion request key is required.");
  if(await db.CompletedWorkshopJobs.AnyAsync(c=>c.JobCardId==input.JobCardId))return await Saved(input.JobCardId,actor);
  if(job["quoteDecision"]?.GetValue<string>()!="Approved"||job["status"]?.GetValue<string>()!="In workshop"||job["workshopStartedAt"] is null)throw new ArgumentException("Start the customer-approved job first.");
  var items=job["items"]!.AsArray().Select(i=>JsonSerializer.SerializeToElement(i)).Where(JobWorkActions.Included).ToArray();var records=await db.JobWorkRecords.Where(w=>w.JobCardId==input.JobCardId).ToListAsync();
  if(items.Length==0||items.Any(i=>!records.Any(w=>w.ItemId==Text(i,"id"))))throw new ArgumentException("Record every included work item before completing the job.");
  var completedAt=DateTime.UtcNow;var summary=new JsonObject{["jobCardId"]=input.JobCardId,["registration"]=vehicle.RegistrationNumber,["customer"]=job["customer"]?.DeepClone(),["quoteNumber"]=job["quoteNumber"]?.DeepClone(),["inspectionIds"]=job["inspectionIds"]?.DeepClone(),["completedAt"]=completedAt.ToString("O"),["completedBy"]=actor.Name,["odometer"]=vehicle.OdometerKm,["work"]=new JsonArray(records.Select(w=>(JsonNode)new JsonObject{["id"]=w.Id.ToString(),["itemId"]=w.ItemId,["item"]=JsonNode.Parse(w.ItemJson),["kind"]=w.Kind,["tyres"]=JsonNode.Parse(w.TyresJson),["technician"]=w.ActorName,["performedDate"]=w.PerformedDate.ToString("yyyy-MM-dd"),["odometer"]=w.OdometerKm,["notes"]=w.Notes,["fitmentId"]=w.FitmentId?.ToString(),["rotationId"]=w.RotationId?.ToString()}).ToArray())};
  db.CompletedWorkshopJobs.Add(new CompletedWorkshopJob{Id=input.RequestId,JobCardId=input.JobCardId,VehicleId=vehicle.Id,SummaryJson=summary.ToJsonString(),CompletedAt=completedAt});job["status"]="Completed";job["completedAt"]=completedAt.ToString("O");job["serviceSummary"]=summary;SaveDocument(doc,rows);await Audit(input.JobCardId,vehicle.RegistrationNumber,actor,"Workshop job completed","All included tasks recorded; handover summary retained.");await db.SaveChangesAsync();return await Saved(input.JobCardId,actor);
 });
}
