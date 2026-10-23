using System.Text.Json;
using System.Text.Json.Nodes;
using TyreLifecycle.Vehicles;
using TyreLifecycle.Workspace;

namespace TyreLifecycle.HttpApi.Host.Workspace;

/// <summary>Filters linked workspace documents and merges scoped collections without dropping hidden rows.</summary>
public sealed class LinkedWorkspaceAccess
{
    private readonly StaffAccount account;
    private readonly VehicleWorkspaceAccess vehicles;
    private readonly Dictionary<string,JsonElement[]> rows=new();
    private readonly HashSet<string> plates,customerIds,tyreIds,inspectionIds,jobIds;
    private static readonly HashSet<string> Supported=new() {
        "tyretrack.demo.vehicles","tyretrack.demo.customers","tyretrack.data.tyres",
        "tyretrack.demo.inspections","tyretrack.demo.bookings","tyretrack.demo.jobCards",
        "tyretrack.demo.tyreScans","tyretrack.demo.tpms","tyretrack.demo.warranty",
        "tyretrack.demo.branches","tyretrack.demo.fleets","tyretrack.demo.devices","tyretrack.demo.stock",
        "tyretrack.lifecycle.events","tyretrack.lifecycle.audit","tyretrack.lifecycle.offline",
        "tyretrack.data.mileageReadings","tyretrack.demo.fitments","tyretrack.demo.removals","tyretrack.demo.rotations","tyretrack.demo.jobWork"
    };
    public LinkedWorkspaceAccess(StaffAccount user,IEnumerable<WorkspaceDocument> documents) {
        account=user;vehicles=new VehicleWorkspaceAccess(user);
        foreach(var doc in documents) {
            var root=JsonSerializer.Deserialize<JsonElement>(doc.Json);
            if(root.ValueKind==JsonValueKind.Array)rows[doc.Key]=root.EnumerateArray().Select(x=>x.Clone()).ToArray();
        }
        var visible=Rows("tyretrack.demo.vehicles").Where(vehicles.Owns).ToArray();
        plates=visible.Select(x=>Key(Text(x,"registration"))).ToHashSet();
        customerIds=visible.Select(x=>Text(x,"customerId")).ToHashSet();
        tyreIds=Rows("tyretrack.data.tyres").Where(x=>Plate(Text(x,"vehicle"))).Select(x=>Text(x,"id")).ToHashSet();
        inspectionIds=Rows("tyretrack.demo.inspections").Where(LinkedPlate).Select(x=>Text(x,"id")).ToHashSet();
        jobIds=Rows("tyretrack.demo.jobCards").Where(LinkedPlate).Select(x=>Text(x,"id")).ToHashSet();
    }
    private bool Manager=>account.Role is "branch-manager" or "service-advisor";
    private bool Technician=>account.Role=="technician";
    private JsonElement[] Rows(string key)=>rows.GetValueOrDefault(key)??[];
    private static string Key(string plate)=>VehicleRules.RegistrationKey(plate);
    private bool Plate(string plate)=>plate.Length>0&&plates.Contains(Key(plate));
    private string Branch=>account.Role=="fleet-manager"?Text(Rows("tyretrack.demo.fleets").FirstOrDefault(x=>Text(x,"id")==account.FleetId),"branchId"):account.BranchId??"";
    private bool Evidence(string key) {
        const string prefix="tyretrack.demo.evidence.";
        return key.StartsWith(prefix,StringComparison.Ordinal)&&inspectionIds.Contains(key[prefix.Length..]);
    }
    public bool Readable(string key)=>(account.Role is "branch-manager" or "service-advisor" or "technician" or "fleet-manager")&&(Supported.Contains(key)||Evidence(key));
    private bool LinkedPlate(JsonElement item) {
        var plate=Text(item,"registration");if(plate.Length==0)plate=Text(item,"vehicle");
        if(!Plate(plate))return false;
        var vehicleId=Text(item,"vehicleId");
        if(vehicleId.Length>0) {
            var vehicle=Rows("tyretrack.demo.vehicles").FirstOrDefault(x=>Text(x,"id")==vehicleId);
            if(vehicle.ValueKind!=JsonValueKind.Undefined&&Key(Text(vehicle,"registration"))!=Key(plate))return false;
        }
        var inspectionId=Text(item,"inspectionId");
        if(inspectionId.Length>0) {
            var inspection=Rows("tyretrack.demo.inspections").FirstOrDefault(x=>Text(x,"id")==inspectionId);
            if(inspection.ValueKind!=JsonValueKind.Undefined&&Key(Text(inspection,"registration"))!=Key(plate))return false;
        }
        var tyreId=Text(item,"tyreId");
        if(tyreId.Length>0) {
            var tyre=Rows("tyretrack.data.tyres").FirstOrDefault(x=>Text(x,"id")==tyreId);
            if(tyre.ValueKind!=JsonValueKind.Undefined&&Key(Text(tyre,"vehicle"))!=Key(plate))return false;
        }
        var jobId=Text(item,"jobCardId");
        if(jobId.Length>0) {
            var job=Rows("tyretrack.demo.jobCards").FirstOrDefault(x=>Text(x,"id")==jobId);
            if(job.ValueKind!=JsonValueKind.Undefined&&Key(Text(job,"registration"))!=Key(plate))return false;
        }
        if(item.TryGetProperty("inspectionIds",out var references)) {
            if(references.ValueKind!=JsonValueKind.Array)return false;
            foreach(var reference in references.EnumerateArray()) {
                if(reference.ValueKind!=JsonValueKind.String)return false;
                var inspection=Rows("tyretrack.demo.inspections").FirstOrDefault(x=>Text(x,"id")==reference.GetString());
                if(inspection.ValueKind==JsonValueKind.Undefined||Key(Text(inspection,"registration"))!=Key(plate))return false;
            }
        }
        return true;
    }
    private bool AuditReference(JsonElement item) {
        var entity=Text(item,"entityId");
        return Text(item,"entityType") switch {
            "Vehicle"=>Plate(entity)||Rows("tyretrack.demo.vehicles").Any(x=>Text(x,"id")==entity&&vehicles.Owns(x)),
            "Tyre"=>tyreIds.Contains(entity),
            "Inspection"=>inspectionIds.Contains(entity),
            "Job card"=>Rows("tyretrack.demo.jobCards").Any(x=>Text(x,"id")==entity)?jobIds.Contains(entity):Plate(Text(item,"registration")),
            "Inspection draft"=>Rows("tyretrack.lifecycle.offline").Any(x=>Text(x,"id")==entity&&LinkedPlate(x)),
            _=>false
        };
    }
    public bool Owns(string key,JsonElement item) {
        if(item.ValueKind!=JsonValueKind.Object)return false;
        return key switch {
            "tyretrack.demo.vehicles"=>vehicles.Owns(item),
            "tyretrack.demo.customers"=>customerIds.Contains(Text(item,"id")) || (Manager&&!string.IsNullOrEmpty(account.BranchId)&&Text(item,"branchId")==account.BranchId),
            "tyretrack.data.tyres"=>Plate(Text(item,"vehicle")),
            "tyretrack.demo.bookings" or "tyretrack.demo.inspections" or "tyretrack.demo.jobCards" or "tyretrack.demo.tyreScans" or "tyretrack.demo.tpms" or "tyretrack.lifecycle.events" or "tyretrack.lifecycle.offline" or "tyretrack.data.mileageReadings" or "tyretrack.demo.fitments" or "tyretrack.demo.removals" or "tyretrack.demo.rotations" or "tyretrack.demo.jobWork"=>LinkedPlate(item),
            "tyretrack.demo.warranty"=>tyreIds.Contains(Text(item,"tyreId")),
            "tyretrack.demo.branches"=>Branch.Length>0&&Text(item,"id")==Branch,
            "tyretrack.demo.fleets"=>account.Role=="fleet-manager"?!string.IsNullOrEmpty(account.FleetId)&&Text(item,"id")==account.FleetId:Branch.Length>0&&Text(item,"branchId")==Branch,
            "tyretrack.demo.devices"=>account.Role!="fleet-manager"&&Branch.Length>0&&Text(item,"branchId")==Branch,
            "tyretrack.demo.stock"=>account.Role!="fleet-manager"&&Branch.Length>0&&(Text(item,"branchId")==Branch || Text(item,"branchId")==""&&Rows("tyretrack.demo.branches").Count(x=>Text(x,"name")==Text(item,"branch"))==1&&Rows("tyretrack.demo.branches").Any(x=>Text(x,"id")==Branch&&Text(x,"name")==Text(item,"branch"))),
            "tyretrack.lifecycle.audit"=>AuditReference(item),
            _=>false
        };
    }
    public JsonElement[] Filter(string key,string json) {
        if(!Readable(key))return [];
        var data=JsonSerializer.Deserialize<JsonElement[]>(json)!;
        return Evidence(key)?data:data.Where(item=>Owns(key,item)).ToArray();
    }
    public JsonElement Merge(string key,JsonElement data,string? previous) {
        if(key=="tyretrack.demo.vehicles") {
            if(data.ValueKind!=JsonValueKind.Array)throw new ArgumentException("Records must be an array.");
            foreach(var item in data.EnumerateArray()) {
                if(item.ValueKind!=JsonValueKind.Object)throw new ArgumentException("Each record must be an object.");
                if(!Rows(key).Any(x=>Text(x,"id")==Text(item,"id"))&&!Rows("tyretrack.demo.customers").Any(x=>Text(x,"id")==Text(item,"customerId")&&Owns("tyretrack.demo.customers",x)))throw new UnauthorizedAccessException("Choose a customer already in your saved scope.");
            }
            return vehicles.Merge(data,previous);
        }
        if(account.Role=="fleet-manager"||!Manager&&!Technician)throw new UnauthorizedAccessException();
        if(data.ValueKind!=JsonValueKind.Array)throw new ArgumentException("Records must be an array.");
        if(Evidence(key))return data;
        var allowed=key switch {
            "tyretrack.demo.customers" or "tyretrack.demo.bookings"=>Manager,
            "tyretrack.demo.warranty"=>account.Role=="branch-manager",
            "tyretrack.demo.inspections" or "tyretrack.demo.jobCards" or "tyretrack.data.tyres" or "tyretrack.demo.tyreScans" or "tyretrack.demo.tpms" or "tyretrack.lifecycle.events" or "tyretrack.lifecycle.audit" or "tyretrack.lifecycle.offline" or "tyretrack.demo.fitments" or "tyretrack.demo.removals" or "tyretrack.demo.rotations" or "tyretrack.demo.jobWork"=>Manager||Technician,
            _=>false
        };
        if(!allowed)throw new UnauthorizedAccessException();
        var before=previous is null?[]:JsonSerializer.Deserialize<JsonElement[]>(previous)!;
        var proposed=JsonNode.Parse(data.GetRawText())!.AsArray();
        foreach(var node in proposed) {
            if(node is not JsonObject item)throw new ArgumentException("Each record must be an object.");
            var id=item["id"]?.GetValue<string>()??"";
            var old=before.FirstOrDefault(x=>Text(x,"id")==id);
            if(key=="tyretrack.demo.customers"&&old.ValueKind==JsonValueKind.Undefined) {
                if(string.IsNullOrEmpty(account.BranchId))throw new UnauthorizedAccessException();
                if(item["branchId"] is not null&&item["branchId"]!.GetValue<string>()!=account.BranchId)throw new UnauthorizedAccessException();
                item["branchId"]=account.BranchId;
            }
            if(key=="tyretrack.lifecycle.audit"&&old.ValueKind==JsonValueKind.Undefined) {item["actor"]=account.Name;item["at"]=DateTime.UtcNow.ToString("O");item.Remove("customerId");}
            if(key=="tyretrack.demo.inspections"&&Technician&&(old.ValueKind==JsonValueKind.Undefined||!Equal(old,JsonSerializer.SerializeToElement(item)))) item["technician"]=account.Name;
        }
        var after=JsonSerializer.Deserialize<JsonElement[]>(proposed.ToJsonString())!;
        if(after.Any(x=>string.IsNullOrWhiteSpace(Text(x,"id")))||after.Select(x=>Text(x,"id")).Distinct().Count()!=after.Length)throw new ArgumentException("Record references must be present and unique.");
        foreach(var item in after) {
            if(!Owns(key,item))throw new UnauthorizedAccessException("Proposed record is outside the saved scope.");
            var old=before.FirstOrDefault(x=>Text(x,"id")==Text(item,"id"));
            if(old.ValueKind!=JsonValueKind.Undefined) {
                if(!Owns(key,old))throw new UnauthorizedAccessException("Existing record is outside the saved scope.");
                if(key=="tyretrack.demo.customers"&&!Only(old,item,["name","mobile","email","status","preferences","mobileProof"]))throw new UnauthorizedAccessException("Customer record contains disallowed changes.");
                if(key=="tyretrack.lifecycle.audit"&&!Equal(old,item))throw new UnauthorizedAccessException();
                if(key is "tyretrack.data.tyres" or "tyretrack.demo.inspections" or "tyretrack.demo.jobCards") {
                    var field=key=="tyretrack.data.tyres"?"vehicle":"registration";
                    if(Text(old,field)!=Text(item,field))throw new UnauthorizedAccessException();
                }
                if(key=="tyretrack.demo.jobCards"&&Technician) {
                    if(!Only(old,item,["status","technician","workshopBay","completedAt"]))throw new UnauthorizedAccessException();
                    var oldStatus=Text(old,"status");var status=Text(item,"status");
                    if(status!=oldStatus&&!(oldStatus=="Approved"&&Text(old,"quoteDecision")=="Approved"&&status=="In workshop")&&!(oldStatus=="In workshop"&&status=="Completed"))throw new UnauthorizedAccessException();
                }
                if(key=="tyretrack.demo.jobCards"&&Manager&&Text(old,"quoteDecision")!=Text(item,"quoteDecision")&&!(Text(item,"status")=="Quote sent"&&Text(item,"quoteDecision")=="Pending"))throw new UnauthorizedAccessException();
            } else if(key=="tyretrack.data.tyres") {
                throw new UnauthorizedAccessException("Register tyres through the fitment action.");
            } else if(key=="tyretrack.demo.jobCards") {
                if(Text(item,"status")!="Draft"||Text(item,"quoteDecision")!="Pending"||Text(item,"quoteSentAt").Length>0||Text(item,"decisionAt").Length>0)throw new UnauthorizedAccessException();
                if(Technician&&(!item.TryGetProperty("inspectionIds",out var refs)||refs.ValueKind!=JsonValueKind.Array||refs.GetArrayLength()==0))throw new UnauthorizedAccessException();
            }
        }
        foreach(var old in before.Where(x=>Owns(key,x)))if(!after.Any(x=>Text(x,"id")==Text(old,"id")))throw new UnauthorizedAccessException("A visible record is missing from the proposed collection.");
        return JsonSerializer.SerializeToElement(before.Where(x=>!Owns(key,x)).Concat(after));
    }
    private static bool Only(JsonElement before,JsonElement after,string[] allowed) {
        foreach(var property in before.EnumerateObject())if(!allowed.Contains(property.Name)&&(!after.TryGetProperty(property.Name,out var value)||!Equal(property.Value,value)))return false;
        return after.EnumerateObject().All(p=>before.TryGetProperty(p.Name,out _)||allowed.Contains(p.Name));
    }
    private static bool Equal(JsonElement a,JsonElement b)=>JsonNode.DeepEquals(JsonNode.Parse(a.GetRawText()),JsonNode.Parse(b.GetRawText()));
    private static string Text(JsonElement item,string key)=>item.ValueKind==JsonValueKind.Object&&item.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()!:"";
}
