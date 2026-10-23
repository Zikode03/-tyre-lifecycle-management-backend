using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using TyreLifecycle.Customers;
using TyreLifecycle.EntityFrameworkCore;
using TyreLifecycle.Inspections;
using TyreLifecycle.Tyres;
using TyreLifecycle.Vehicles;
using TyreLifecycle.Workspace;
using Volo.Abp.Domain.Entities.Auditing;

namespace TyreLifecycle.HttpApi.Host.Workspace;

/// <summary>Translates the existing screen models to the shared domain entities.</summary>
public static class CoreWorkspace
{
    public static readonly string[] Keys = ["tyretrack.demo.customers", "tyretrack.demo.vehicles", "tyretrack.data.tyres", "tyretrack.demo.inspections"];
    private const string Record = "WorkspaceRecord";

    public static async Task InitializeAsync(TyreLifecycleDbContext db)
    {
        const string marker = "tyretrack.system.core-imported";
        if (await db.WorkspaceDocuments.AnyAsync(x => x.Key == marker)) return;
        foreach (var key in Keys) {
            var doc = await db.WorkspaceDocuments.FindAsync(key);
            if (doc is not null) await ApplyAsync(db, key, JsonSerializer.Deserialize<JsonElement>(doc.Json), false);
        }
        db.WorkspaceDocuments.Add(new WorkspaceDocument { Key = marker, Json = "true", Version = 1, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    public static async Task ApplyAsync(TyreLifecycleDbContext db, string key, JsonElement data, bool removeMissing = true, string actor = "System")
    {
        if (!Keys.Contains(key)) return;
        var records = data.EnumerateArray().ToArray();
        var retained = new HashSet<Guid>();
        if (key == Keys[0]) {
            var existing = await db.Customers.ToListAsync();
            foreach (var item in records) {
                var clientId = Text(item, "id");
                var entity = existing.FirstOrDefault(x => x.CustomerNumber == clientId);
                var names = CustomerContact.NormalizeName(Text(item, "name")).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var first = names.FirstOrDefault() ?? ""; var last = names.Length > 1 ? names[1] : "—";
                if (entity is null) {
                    entity = new Customer(Id("customer", clientId), clientId, first, last, Text(item,"mobile"), Text(item,"email"));
                    db.Customers.Add(entity); existing.Add(entity);
                } else {
                    db.Entry(entity).Property(x => x.FirstName).CurrentValue = first;
                    db.Entry(entity).Property(x => x.LastName).CurrentValue = last;
                    entity.UpdateContact(Text(item,"mobile"), Text(item,"email"));
                }
                var stored=Stored(entity);
                var history=stored["activity"] as JsonArray ?? new JsonArray();
                var description=new List<string>();
                if(entity.CreationTime==default || db.Entry(entity).State==EntityState.Added) description.Add("Customer created");
                else {
                    // Compare the last saved screen record; names/contact have already been applied above.
                    if(stored["name"]?.GetValue<string>()!=Text(item,"name")) description.Add("Name updated");
                    if(stored["mobile"]?.GetValue<string>()!=entity.MobileNumber) description.Add("Mobile number changed after verification");
                    if((stored["email"]?.GetValue<string>()??"")!=(entity.Email??"")) description.Add("Email updated");
                    if(entity.IsActive!=(Text(item,"status")!="Inactive")) description.Add(Text(item,"status")=="Inactive"?"Account deactivated":"Account reactivated");
                    if(!JsonNode.DeepEquals(stored["preferences"],item.TryGetProperty("preferences",out var prefs)?JsonNode.Parse(prefs.GetRawText()):null)) description.Add("Communication preferences updated");
                }
                if(description.Count>0) history.Add(new JsonObject{["id"]=Guid.NewGuid().ToString(),["at"]=DateTime.UtcNow.ToString("O"),["actor"]=actor,["description"]=string.Join("; ",description)});
                var clean=JsonNode.Parse(item.GetRawText())!.AsObject();clean.Remove("mobileProof");clean["activity"]=history.DeepClone();
                entity.SetActive(Text(item,"status") != "Inactive"); entity.ExtraProperties[Record]=clean.ToJsonString(); retained.Add(entity.Id);
            }
            if (removeMissing) foreach (var entity in existing.Where(x => !retained.Contains(x.Id))) {
                if (await db.Vehicles.AnyAsync(x => x.CustomerId == entity.Id)) throw new ArgumentException("A customer with registered vehicles cannot be removed.");
                entity.IsDeleted = true;
            }
        }
        else if (key == Keys[1]) {
            var existing = await db.Vehicles.ToListAsync();
            var customers = (await db.Customers.ToListAsync()).Concat(db.Customers.Local).DistinctBy(x=>x.Id).ToList();
            foreach (var item in records) {
                var clientId = Text(item,"id"); var id = Id("vehicle",clientId);
                var entity = existing.FirstOrDefault(x => x.Id == id);
                var customer = customers.FirstOrDefault(x=>x.CustomerNumber==Text(item,"customerId")) ?? throw new ArgumentException("Choose a registered customer.");
                var km = Number(item,"mileage");
                TyreLifecycle.Vehicles.VehicleRules.Registration(Text(item,"registration"));
                TyreLifecycle.Vehicles.VehicleRules.Details(Text(item,"make"),Text(item,"model"),(int)Number(item,"year"),km);
                if (entity is null) {
                    if(!customer.IsActive) throw new ArgumentException("Choose an active customer for a new vehicle.");
                    entity = new Vehicle(id,customer.Id,Text(item,"registration"),Text(item,"make"),Text(item,"model"),(int)Number(item,"year"),km);
                    db.Vehicles.Add(entity); existing.Add(entity);
                } else {
                    if(customer.Id!=entity.CustomerId || Text(item,"registration")!=entity.RegistrationNumber) throw new ArgumentException("Ownership and registration changes require a separate transfer or correction process.");
                    if(ClientId(entity)!=clientId) throw new ArgumentException("This vehicle reference is already registered.");
                    if(km<entity.OdometerKm) throw new ArgumentException("Mileage cannot be below the saved odometer reading.");
                    entity.UpdateOdometer(km);
                    db.Entry(entity).Property(x=>x.Make).CurrentValue=Text(item,"make");
                    db.Entry(entity).Property(x=>x.Model).CurrentValue=Text(item,"model");
                    db.Entry(entity).Property(x=>x.Year).CurrentValue=(int)Number(item,"year");
                }
                entity.SetActive(Text(item,"status")!="Inactive"); Remember(entity,item); retained.Add(entity.Id);
            }
            if(removeMissing) foreach(var entity in existing.Where(x=>!retained.Contains(x.Id))) {
                if(await db.Tyres.AnyAsync(x=>x.VehicleId==entity.Id) || await db.Inspections.AnyAsync(x=>x.VehicleId==entity.Id)||await db.JobWorkRecords.AnyAsync(x=>x.VehicleId==entity.Id)||await db.CompletedWorkshopJobs.AnyAsync(x=>x.VehicleId==entity.Id)) throw new ArgumentException("A vehicle with tyre or inspection history cannot be removed.");
                entity.IsDeleted=true;
            }
        }
        else if (key == Keys[2]) {
            var existing=await db.Tyres.ToListAsync();
            var vehicles=(await db.Vehicles.ToListAsync()).Concat(db.Vehicles.Local).DistinctBy(x=>x.Id).ToList();
            foreach(var item in records) {
                var clientId=Text(item,"id"); var id=Id("tyre",clientId);
                var entity=existing.FirstOrDefault(x=>x.Id==id);
                var vehicle=vehicles.FirstOrDefault(x=>x.RegistrationNumber==Text(item,"vehicle")) ?? throw new ArgumentException("Register the tyre's vehicle first.");
                if(entity is null) {
                    entity=new Tyre(id,vehicle.Id,Text(item,"reference"),Text(item,"brand"),Text(item,"model"),Text(item,"size"),Text(item,"position"),Math.Max(0,vehicle.OdometerKm-Number(item,"mileage")));
                    db.Tyres.Add(entity); existing.Add(entity);
                } else {
                    if(ClientId(entity)!=clientId) throw new ArgumentException("This tyre reference is already registered. Use its existing workspace record.");
                    if((entity.ExtraProperties.ContainsKey("FitmentRecordId")||entity.ExtraProperties.ContainsKey("RemovalRecordId")||entity.ExtraProperties.ContainsKey("RotationRecordId")||entity.ExtraProperties.ContainsKey("JobWorkId"))&&(entity.VehicleId!=vehicle.Id||entity.TyreNumber!=Text(item,"reference")||entity.Brand!=Text(item,"brand")||entity.Model!=Text(item,"model")||entity.Size!=Text(item,"size")||entity.Position!=Text(item,"position")))throw new ArgumentException("Recorded tyre identity and position require a lifecycle action; they cannot be overwritten.");
                    db.Entry(entity).Property(x=>x.VehicleId).CurrentValue=vehicle.Id;
                    db.Entry(entity).Property(x=>x.TyreNumber).CurrentValue=Text(item,"reference");
                    db.Entry(entity).Property(x=>x.Brand).CurrentValue=Text(item,"brand");
                    db.Entry(entity).Property(x=>x.Model).CurrentValue=Text(item,"model");
                    db.Entry(entity).Property(x=>x.Size).CurrentValue=Text(item,"size");
                    entity.MoveTo(Text(item,"position"));
                }
                if(entity.ExtraProperties.ContainsKey("WorkshopInspectionId")&&(Decimal(item,"tread")!=entity.TreadDepthMm||Text(item,"status")!=entity.HealthStatus.ToString()))throw new ArgumentException("Workshop measurements require a new recorded inspection.");
                var status=Enum.TryParse<TyreHealthStatus>(Text(item,"status"),out var parsed)?parsed:TyreHealthStatus.Unknown;
                if(Decimal(item,"tread") is decimal tread&&(!item.TryGetProperty("treadRecorded",out var measured)||measured.ValueKind!=JsonValueKind.False||tread!=0)) entity.RecordTread(tread,status);
                Remember(entity,item); retained.Add(entity.Id);
            }
            if(removeMissing) foreach(var entity in existing.Where(x=>!retained.Contains(x.Id))) {
                if(entity.ExtraProperties.ContainsKey("FitmentRecordId")||entity.ExtraProperties.ContainsKey("RemovalRecordId")||entity.ExtraProperties.ContainsKey("RotationRecordId")||entity.ExtraProperties.ContainsKey("JobWorkId"))throw new ArgumentException("Recorded tyres must be removed through a lifecycle action, preserving their passport.");
                entity.IsDeleted=true;
            }
        }
        else {
            var existing=await db.Inspections.Include(x=>x.Readings).ToListAsync();
            var vehicles=(await db.Vehicles.ToListAsync()).Concat(db.Vehicles.Local).DistinctBy(x=>x.Id).ToList();
            var tyres=(await db.Tyres.ToListAsync()).Concat(db.Tyres.Local).DistinctBy(x=>x.Id).ToList();
            foreach(var item in records) {
                var clientId=Text(item,"id"); var entity=existing.FirstOrDefault(x=>x.InspectionNumber==clientId);
                var vehicle=vehicles.FirstOrDefault(x=>x.RegistrationNumber==Text(item,"registration")) ?? throw new ArgumentException("Register the inspected vehicle first.");
                if(entity is null) {
                    entity=new Inspection(Id("inspection",clientId),clientId,vehicle.Id,item.TryGetProperty("odometer",out _)?Number(item,"odometer"):null);
                    db.Inspections.Add(entity); existing.Add(entity);
                }
                if(entity.ExtraProperties.ContainsKey("WorkshopInspectionId")){retained.Add(entity.Id);continue;}
                var position=Text(item,"position");
                if(!string.IsNullOrWhiteSpace(position)) {
                    var reading=entity.Readings.FirstOrDefault(x=>x.Position.Equals(position,StringComparison.OrdinalIgnoreCase));
                    var inner=Decimal(item,"inner"); var centre=Decimal(item,"centre"); var outer=Decimal(item,"outer"); var pressure=Decimal(item,"pressure");
                    var source=Text(item,"measurementSource") switch {"Phone camera"=>MeasurementSource.PhoneCamera,"TPMS"=>MeasurementSource.Tpms,"Connected device"=>MeasurementSource.ConnectedDevice,"Drive-over scanner"=>MeasurementSource.DriveOverScanner,_=>MeasurementSource.ManualGauge};
                    var tyre=tyres.FirstOrDefault(x=>x.Id==Id("tyre",Text(item,"tyreId")));
                    if(reading is null || reading.InnerTreadMm!=inner || reading.CentreTreadMm!=centre || reading.OuterTreadMm!=outer || reading.PressurePsi!=pressure || reading.Source!=source)
                        entity.AddOrUpdateReading(Id("reading",clientId+":"+position.ToLowerInvariant()),position,source,inner,centre,outer,pressure,tyre?.Id,Text(item,"wearPattern"),item.TryGetProperty("defects",out var defects)?defects.GetRawText():null,Text(item,"recommendation"));
                }
                var desired=Text(item,"status") switch {"Completed"=>InspectionStatus.Completed,"In progress"=>InspectionStatus.InProgress,"Awaiting technician"=>InspectionStatus.AwaitingTechnician,"Cancelled"=>InspectionStatus.Cancelled,_=>InspectionStatus.Booked};
                if(desired==InspectionStatus.InProgress && entity.Status!=desired) entity.Start(DateTime.UtcNow);
                else if(desired==InspectionStatus.Completed && entity.Status!=desired) {if(entity.Status!=InspectionStatus.InProgress) entity.Start(DateTime.UtcNow); entity.Complete(DateTime.UtcNow,Text(item,"notes"));}
                else if(desired==InspectionStatus.Cancelled && entity.Status!=desired) entity.Cancel(Text(item,"notes"));
                else if(entity.Status is not (InspectionStatus.Completed or InspectionStatus.Cancelled)) db.Entry(entity).Property(x=>x.Status).CurrentValue=desired;
                db.Entry(entity).Property(x=>x.OdometerKm).CurrentValue=item.TryGetProperty("odometer",out _)?Number(item,"odometer"):null;
                Remember(entity,item); retained.Add(entity.Id);
            }
            if(removeMissing) foreach(var entity in existing.Where(x=>!retained.Contains(x.Id))) {if(entity.ExtraProperties.ContainsKey("WorkshopInspectionId"))throw new ArgumentException("Completed workshop inspections cannot be removed.");entity.IsDeleted=true;}
        }
    }

    public static async Task RefreshAsync(TyreLifecycleDbContext db)
    {
        var customers=await db.Customers.AsNoTracking().ToListAsync();
        var vehicles=await db.Vehicles.AsNoTracking().ToListAsync();
        var tyres=await db.Tyres.AsNoTracking().ToListAsync();
        var work=await db.JobWorkRecords.AsNoTracking().ToListAsync();
        var completedJobs=await db.CompletedWorkshopJobs.AsNoTracking().ToListAsync();
        var rotations=await db.TyreRotations.AsNoTracking().ToListAsync();
        var removals=await db.TyreRemovals.AsNoTracking().ToListAsync();
        var fitments=await db.TyreFitments.AsNoTracking().ToListAsync();
        var inspections=await db.Inspections.AsNoTracking().Include(x=>x.Readings).ToListAsync();
        var payloads=new Dictionary<string,JsonArray>();
        payloads[Keys[0]]=new JsonArray(customers.Select(c=> {
            var o=Stored(c);o["id"]=c.CustomerNumber;o["name"]=(c.FirstName+" "+c.LastName).Trim().TrimEnd('—').Trim();o["mobile"]=c.MobileNumber;o["email"]=c.Email??"";o["status"]=c.IsActive?"Active":"Inactive";return (JsonNode)o;
        }).ToArray());
        payloads[Keys[1]]=new JsonArray(vehicles.Select(v=> {
            var o=Stored(v);o["id"]=ClientId(v);o["customerId"]=customers.FirstOrDefault(c=>c.Id==v.CustomerId)?.CustomerNumber??v.CustomerId.ToString();o["registration"]=v.RegistrationNumber;o["make"]=v.Make;o["model"]=v.Model;o["year"]=v.Year;o["mileage"]=v.OdometerKm;o["status"]=v.IsActive?"Active":"Inactive";o["mileageHistory"]=v.ExtraProperties.TryGetValue("MileageHistory",out var history) && history is not null ? JsonNode.Parse(history.ToString()!) : new JsonArray();return (JsonNode)o;
        }).ToArray());
        payloads[Keys[2]]=new JsonArray(tyres.Select(t=> {
            var o=Stored(t);var vehicle=vehicles.FirstOrDefault(v=>v.Id==t.VehicleId);o["id"]=ClientId(t);o["reference"]=t.TyreNumber;o["brand"]=t.Brand;o["model"]=t.Model;o["size"]=t.Size;o["position"]=t.Position;o["vehicle"]=vehicle?.RegistrationNumber??"";o["mileage"]=Math.Max(0,(removals.FirstOrDefault(r=>r.TyreId==t.Id)?.OdometerKm??vehicle?.OdometerKm??t.FitmentOdometerKm)-t.FitmentOdometerKm);o["tread"]=t.TreadDepthMm??0;o["treadRecorded"]=t.TreadDepthMm.HasValue;o["isActive"]=t.IsActive;o["fitmentOdometer"]=t.FitmentOdometerKm;o["status"]=t.HealthStatus.ToString();o["fittedOn"]=fitments.Where(f=>f.TyreId==t.Id).OrderByDescending(f=>f.RecordedAt).FirstOrDefault()?.FitmentDate.ToString("yyyy-MM-dd")??o["fittedOn"]?.GetValue<string>()??t.CreationTime.ToString("yyyy-MM-dd");return (JsonNode)o;
        }).ToArray());
        payloads[Keys[3]]=new JsonArray(inspections.Select(i=> {
            var o=Stored(i);var vehicle=vehicles.FirstOrDefault(v=>v.Id==i.VehicleId);o["id"]=i.InspectionNumber;o["registration"]=vehicle?.RegistrationNumber??"";var customer=customers.FirstOrDefault(c=>c.Id==vehicle?.CustomerId);o["customer"]=customer is null?"":(customer.FirstName+" "+customer.LastName).Trim();o["status"]=i.Status switch{InspectionStatus.InProgress=>"In progress",InspectionStatus.AwaitingTechnician=>"Awaiting technician",_=>i.Status.ToString()};o["recordedAt"]=(i.CompletedAt??i.CreationTime).ToString("O");o["technician"]??="Unassigned";if(i.OdometerKm is not null)o["odometer"]=i.OdometerKm;if(i.Notes is not null)o["notes"]=i.Notes;
            var reading=i.Readings.FirstOrDefault();if(reading is not null){o["position"]=reading.Position;o["inner"]=reading.InnerTreadMm;o["centre"]=reading.CentreTreadMm;o["outer"]=reading.OuterTreadMm;o["pressure"]=reading.PressurePsi;o["wearPattern"]=reading.WearPattern;o["recommendation"]=reading.Recommendation;var tyre=tyres.FirstOrDefault(t=>t.Id==reading.TyreId);if(tyre is not null)o["tyreId"]=ClientId(tyre);o["measurementSource"]=reading.Source switch{MeasurementSource.PhoneCamera=>"Phone camera",MeasurementSource.Tpms=>"TPMS",MeasurementSource.ConnectedDevice=>"Connected device",MeasurementSource.DriveOverScanner=>"Drive-over scanner",_=>"Manual gauge"};}
            return (JsonNode)o;
        }).ToArray());
        // Preserve old client-written fitment records as explicitly unverified legacy data.
        var oldFitmentDoc=await db.WorkspaceDocuments.FindAsync("tyretrack.demo.fitments");
        var oldFitmentRows=oldFitmentDoc is null?new JsonArray():JsonNode.Parse(oldFitmentDoc.Json) as JsonArray??new JsonArray();
        var fitmentRows=new JsonArray(oldFitmentRows.Where(x=>x is JsonObject&&x["source"]?.GetValue<string>()!="Recorded fitment").Select(x=>{var row=x!.DeepClone();row["source"]="Legacy record";return row;}).ToArray());
        foreach(var fitment in fitments) {
            var vehicle=vehicles.FirstOrDefault(v=>v.Id==fitment.VehicleId);
            var tyre=tyres.FirstOrDefault(t=>t.Id==fitment.TyreId);
            if(vehicle is null||tyre is null)continue;
            fitmentRows.Add(new JsonObject{["id"]=fitment.Id.ToString(),["tyreId"]=ClientId(tyre),["vehicleId"]=ClientId(vehicle),["registration"]=vehicle.RegistrationNumber,["reference"]=tyre.TyreNumber,["position"]=fitment.Position,["fitmentDate"]=fitment.FitmentDate.ToString("yyyy-MM-dd"),["odometer"]=fitment.OdometerKm,["initialTread"]=fitment.InitialTreadMm,["sku"]=fitment.StockSku,["recordedAt"]=fitment.RecordedAt.ToString("O"),["recordedBy"]=fitment.ActorName,["notes"]=fitment.Notes,["source"]="Recorded fitment"});
        }
        payloads["tyretrack.demo.fitments"]=fitmentRows;
        payloads["tyretrack.demo.removals"]=new JsonArray(removals.Select(r=> {
            var tyre=tyres.FirstOrDefault(t=>t.Id==r.TyreId);var vehicle=vehicles.FirstOrDefault(v=>v.Id==r.VehicleId);
            return (JsonNode)new JsonObject{["id"]=r.Id.ToString(),["tyreId"]=tyre is null?r.TyreId.ToString():ClientId(tyre),["vehicleId"]=vehicle is null?r.VehicleId.ToString():ClientId(vehicle),["registration"]=vehicle?.RegistrationNumber,["reference"]=tyre?.TyreNumber,["position"]=r.Position,["replacementTyreId"]=r.ReplacementTyreId?.ToString(),["removalDate"]=r.RemovalDate.ToString("yyyy-MM-dd"),["odometer"]=r.OdometerKm,["reason"]=r.Reason,["destination"]=r.Destination,["notes"]=r.Notes,["recordedAt"]=r.RecordedAt.ToString("O"),["recordedBy"]=r.ActorName};
        }).ToArray());
        payloads["tyretrack.demo.rotations"]=new JsonArray(rotations.Select(r=> {
            var vehicle=vehicles.FirstOrDefault(v=>v.Id==r.VehicleId);
            return (JsonNode)new JsonObject{["id"]=r.Id.ToString(),["vehicleId"]=vehicle is null?r.VehicleId.ToString():ClientId(vehicle),["registration"]=vehicle?.RegistrationNumber,["rotationDate"]=r.RotationDate.ToString("yyyy-MM-dd"),["odometer"]=r.OdometerKm,["notes"]=r.Notes,["recordedAt"]=DateTime.SpecifyKind(r.RecordedAt,DateTimeKind.Utc).ToString("O"),["recordedBy"]=r.ActorName,["moves"]=JsonNode.Parse(r.MovesJson)};
        }).ToArray());
        payloads["tyretrack.demo.jobWork"]=new JsonArray(work.Select(w=> {
            var vehicle=vehicles.FirstOrDefault(v=>v.Id==w.VehicleId);
            return (JsonNode)new JsonObject{["id"]=w.Id.ToString(),["jobCardId"]=w.JobCardId,["itemId"]=w.ItemId,["registration"]=vehicle?.RegistrationNumber,["vehicleId"]=vehicle is null?w.VehicleId.ToString():ClientId(vehicle),["item"]=JsonNode.Parse(w.ItemJson),["kind"]=w.Kind,["fitmentId"]=w.FitmentId?.ToString(),["rotationId"]=w.RotationId?.ToString(),["tyres"]=JsonNode.Parse(w.TyresJson),["performedDate"]=w.PerformedDate.ToString("yyyy-MM-dd"),["odometer"]=w.OdometerKm,["technician"]=w.ActorName,["notes"]=w.Notes,["recordedAt"]=DateTime.SpecifyKind(w.RecordedAt,DateTimeKind.Utc).ToString("O")};
        }).ToArray());
        foreach(var (key,data) in payloads) {
            var doc=await db.WorkspaceDocuments.FindAsync(key);
            if(doc is null){doc=new WorkspaceDocument{Key=key};db.WorkspaceDocuments.Add(doc);}
            // Keep the screen's record ordering stable when the entity values have not changed.
            var previous=JsonNode.Parse(doc.Json) as JsonArray;
            var order=previous?.Select((x,index)=>(id:x?["id"]?.GetValue<string>()??"",index)).ToDictionary(x=>x.id,x=>x.index)??new Dictionary<string,int>();
            var sorted=new JsonArray(data.OrderBy(x=>order.GetValueOrDefault(x?["id"]?.GetValue<string>()??"",-1)).Select(x=>x!.DeepClone()).ToArray());
            if(JsonNode.DeepEquals(JsonNode.Parse(doc.Json),sorted)) continue;
            doc.Json=sorted.ToJsonString();doc.Version++;doc.UpdatedAt=DateTime.UtcNow;
        }
        var jobDocument=await db.WorkspaceDocuments.FindAsync("tyretrack.demo.jobCards");
        if(jobDocument is not null&&JsonNode.Parse(jobDocument.Json) is JsonArray jobRows){
            var before=jobRows.ToJsonString();foreach(var completion in completedJobs){var row=jobRows.OfType<JsonObject>().FirstOrDefault(j=>j["id"]?.GetValue<string>()==completion.JobCardId);if(row is not null){row["status"]="Completed";row["completedAt"]=DateTime.SpecifyKind(completion.CompletedAt,DateTimeKind.Utc).ToString("O");row["serviceSummary"]=JsonNode.Parse(completion.SummaryJson);}}
            if(before!=jobRows.ToJsonString()){jobDocument.Json=jobRows.ToJsonString();jobDocument.Version++;jobDocument.UpdatedAt=DateTime.UtcNow;}
        }
        // Keep service records attached to the vehicle owner when their display name changes.
        foreach(var key in new[]{"tyretrack.demo.bookings","tyretrack.demo.jobCards"}) {
            var document=await db.WorkspaceDocuments.FindAsync(key);
            if(document is null || JsonNode.Parse(document.Json) is not JsonArray records) continue;
            var before=records.DeepClone();
            foreach(var node in records) {
                if(node is not JsonObject record) continue;
                var vehicle=vehicles.FirstOrDefault(v=>VehicleRules.RegistrationKey(v.RegistrationNumber)==VehicleRules.RegistrationKey(record["registration"]?.GetValue<string>()??""));
                var owner=customers.FirstOrDefault(c=>c.Id==vehicle?.CustomerId);
                if(owner is null) continue;
                record["registration"]=vehicle!.RegistrationNumber;
                record["customerId"]=owner.CustomerNumber;
                record["customer"]=(owner.FirstName+" "+owner.LastName).Trim().TrimEnd('—').Trim();
            }
            if(!JsonNode.DeepEquals(before,records)){document.Json=records.ToJsonString();document.Version++;document.UpdatedAt=DateTime.UtcNow;}
        }
        await db.SaveChangesAsync();
    }

    public static Guid EntityId(string kind,string value)=>Id(kind,value);
    private static Guid Id(string kind,string value) => Guid.TryParse(value,out var id)?id:new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("tyretrack:"+kind+":"+value)).AsSpan(0,16));
    private static string Text(JsonElement item,string key)=>item.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()!:"";
    private static long Number(JsonElement item,string key)=>item.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.Number&&value.TryGetInt64(out var number)?number:0;
    private static decimal? Decimal(JsonElement item,string key)=>item.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.Number&&value.TryGetDecimal(out var number)?number:null;
    private static void Remember(FullAuditedAggregateRoot<Guid> entity,JsonElement item)=>entity.ExtraProperties[Record]=item.GetRawText();
    private static JsonObject Stored(FullAuditedAggregateRoot<Guid> entity)=>entity.ExtraProperties.TryGetValue(Record,out var stored) && stored is not null ? JsonNode.Parse(stored.ToString()!) as JsonObject??new JsonObject():new JsonObject();
    private static string ClientId(FullAuditedAggregateRoot<Guid> entity)=>Stored(entity)["id"]?.GetValue<string>()??entity.Id.ToString();
}
