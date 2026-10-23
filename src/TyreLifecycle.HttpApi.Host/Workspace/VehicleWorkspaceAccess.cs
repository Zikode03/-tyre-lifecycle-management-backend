using System.Text.Json;
using System.Text.Json.Nodes;
using TyreLifecycle.Workspace;

namespace TyreLifecycle.HttpApi.Host.Workspace;

/// <summary>Vehicle-only access rules. Linked record permissions are handled separately.</summary>
public sealed class VehicleWorkspaceAccess(StaffAccount account)
{
    public bool Owns(JsonElement item)=>account.Role switch {
        "system-admin"=>true,
        "branch-manager" or "service-advisor"=>!string.IsNullOrEmpty(account.BranchId)&&Text(item,"branchId")==account.BranchId,
        "technician"=>!string.IsNullOrEmpty(account.BranchId)&&Text(item,"branchId")==account.BranchId&&Text(item,"technicianId")==account.Id.ToString(),
        "fleet-manager"=>!string.IsNullOrEmpty(account.FleetId)&&Text(item,"fleetId")==account.FleetId,
        _=>false
    };
    public JsonElement[] Filter(string json)=>JsonSerializer.Deserialize<JsonElement[]>(json)!.Where(Owns).ToArray();
    public JsonElement Merge(JsonElement proposed,string? previous) {
        if(account.Role is not ("branch-manager" or "service-advisor" or "technician")||proposed.ValueKind!=JsonValueKind.Array)throw new UnauthorizedAccessException();
        var before=previous is null?[]:JsonSerializer.Deserialize<JsonElement[]>(previous)!;
        var after=JsonNode.Parse(proposed.GetRawText())!.AsArray();
        foreach(var node in after) {
            if(node is not JsonObject record)throw new UnauthorizedAccessException();
            var id=record["id"]?.GetValue<string>()??"";
            if(!before.Any(x=>Text(x,"id")==id)) {
                if(account.Role=="technician"||string.IsNullOrEmpty(account.BranchId))throw new UnauthorizedAccessException();
                if(record["branchId"] is not null&&record["branchId"]!.GetValue<string>()!=account.BranchId)throw new UnauthorizedAccessException();
                record["branchId"]=account.BranchId;
            }
        }
        var input=JsonSerializer.Deserialize<JsonElement[]>(after.ToJsonString())!;
        if(input.Any(x=>!Owns(x)))throw new UnauthorizedAccessException();
        foreach(var item in input) {
            var old=before.FirstOrDefault(x=>Text(x,"id")==Text(item,"id"));
            if(old.ValueKind==JsonValueKind.Undefined)continue;
            if(!Owns(old))throw new UnauthorizedAccessException();
            var allowed=account.Role=="technician"?new[]{"mileage"}:new[]{"make","model","year","mileage","status","fleetId","technicianId"};
            foreach(var p in old.EnumerateObject())if(!allowed.Contains(p.Name)&&(!item.TryGetProperty(p.Name,out var value)||!Equal(p.Value,value)))throw new UnauthorizedAccessException();
            if(item.EnumerateObject().Any(p=>!old.TryGetProperty(p.Name,out _)&&!allowed.Contains(p.Name)))throw new UnauthorizedAccessException();
        }
        // Scoped clients send only their visible collection. Preserve hidden rows and disallow deletion.
        foreach(var old in before.Where(Owns))if(!input.Any(x=>Text(x,"id")==Text(old,"id")))throw new UnauthorizedAccessException();
        return JsonSerializer.SerializeToElement(before.Where(x=>!Owns(x)).Concat(input));
    }
    private static bool Equal(JsonElement a,JsonElement b)=>JsonNode.DeepEquals(JsonNode.Parse(a.GetRawText()),JsonNode.Parse(b.GetRawText()));
    private static string Text(JsonElement item,string key)=>item.ValueKind==JsonValueKind.Object&&item.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()!:"";
}
