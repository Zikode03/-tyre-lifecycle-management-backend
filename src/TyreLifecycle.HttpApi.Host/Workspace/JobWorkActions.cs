using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using TyreLifecycle.EntityFrameworkCore;
using TyreLifecycle.Workspace;
namespace TyreLifecycle.HttpApi.Host.Workspace;
public static class JobWorkActions
{
 public static string Text(JsonElement row,string key)=>row.ValueKind==JsonValueKind.Object&&row.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()!:"";
 public static bool Included(JsonElement row)=>row.TryGetProperty("approved",out var v)&&v.ValueKind==JsonValueKind.True;
 public static string Position(string value)=>value.Replace(" ","").Replace("-","").ToUpperInvariant();
 public static async Task<JsonElement> Item(TyreLifecycleDbContext db,string jobId,string itemId,string registration,string kind,string? oldTyreId=null,string? position=null,string? sku=null,DateOnly? performedDate=null){
  if(string.IsNullOrWhiteSpace(jobId)||jobId.Length>64||string.IsNullOrWhiteSpace(itemId)||itemId.Length>64)throw new ArgumentException("Choose a valid job and item.");
  var doc=await db.WorkspaceDocuments.FindAsync("tyretrack.demo.jobCards");var jobs=doc is null?[]:JsonSerializer.Deserialize<JsonElement[]>(doc.Json)!;var job=jobs.FirstOrDefault(j=>Text(j,"id")==jobId);
  if(job.ValueKind==JsonValueKind.Undefined||Text(job,"registration")!=registration)throw new ArgumentException("Job does not belong to this vehicle.");
  if(Text(job,"quoteDecision")!="Approved"||Text(job,"status")!="In workshop"||Text(job,"workshopStartedAt")=="")throw new ArgumentException("Start the customer-approved job before recording work.");
  var item=job.GetProperty("items").EnumerateArray().FirstOrDefault(i=>Text(i,"id")==itemId);
  if(item.ValueKind==JsonValueKind.Undefined||!Included(item)||Text(item,"type")!=kind)throw new ArgumentException("This item is not included in the approved work.");
  if(await db.JobWorkRecords.AnyAsync(w=>w.JobCardId==jobId&&w.ItemId==itemId))throw new DbUpdateConcurrencyException("Work is already recorded for this item.");
  if(performedDate is not null&&DateOnly.FromDateTime(DateTime.Parse(Text(job,"workshopStartedAt")).ToUniversalTime().AddHours(2))>performedDate)throw new ArgumentException("Record work on or after the job's start date.");
  if(kind=="Tyre replacement"){
   if(!item.TryGetProperty("quantity",out var q)||!q.TryGetDecimal(out var quantity)||quantity!=1)throw new ArgumentException("Each tyre replacement item must identify one tyre. Split multiple tyres into separate quoted items.");
   if(position is null||Position(Text(item,"position"))!=Position(position))throw new ArgumentException("Replacement position differs from the approved item.");
   var expected=job.GetProperty("workshopExpectedTyres").TryGetProperty(itemId,out var expectedId)?expectedId.GetString():null;
   if(string.IsNullOrEmpty(oldTyreId)||expected!=oldTyreId)throw new ArgumentException("Replace the original tyre assigned to this approved item.");
   if(Text(item,"sku").Length>0&&!Text(item,"sku").Equals(sku,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Use the stock SKU included in the approved item.");
  }
  return item;
 }
 public static JobWorkRecord Record(Guid id,string jobId,string itemId,Guid vehicleId,JsonElement item,string kind,Guid actorId,string actorName,DateOnly date,long odometer,string notes,string tyresJson,string hash)=>new(){Id=id,JobCardId=jobId,ItemId=itemId,VehicleId=vehicleId,ItemJson=item.GetRawText(),Kind=kind,ActorId=actorId,ActorName=actorName,PerformedDate=date,OdometerKm=odometer,Notes=notes,TyresJson=tyresJson,RequestHash=hash,RecordedAt=DateTime.UtcNow};
}
