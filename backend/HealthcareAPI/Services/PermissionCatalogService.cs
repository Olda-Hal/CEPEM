using HealthcareAPI.Models;
using HealthcareAPI.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace HealthcareAPI.Services;

public interface IPermissionCatalogService
{
    List<EndpointPermissionCatalogItem> GetCatalog();
    string BuildPermissionKey(HttpContext context);
    Task<List<AccessResourceContextDto>> ExtractResourceContextsAsync(HttpContext context);
}

public class PermissionCatalogService : IPermissionCatalogService
{
    private readonly EndpointDataSource _endpointDataSource;
    private readonly IHttpClientFactory _httpClientFactory;

    public PermissionCatalogService(EndpointDataSource endpointDataSource, IHttpClientFactory httpClientFactory)
    {
        _endpointDataSource = endpointDataSource;
        _httpClientFactory = httpClientFactory;
    }

    public List<EndpointPermissionCatalogItem> GetCatalog()
    {
        var items = new List<EndpointPermissionCatalogItem>();

        foreach (var endpoint in _endpointDataSource.Endpoints.OfType<RouteEndpoint>())
        {
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
            if (methods == null || methods.Count == 0)
                continue;

            if (!endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any())
                continue;

            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null)
                continue;

            var route = NormalizeRoute(endpoint.RoutePattern.RawText ?? string.Empty);
            foreach (var method in methods)
            {
                var key = $"{method.ToUpperInvariant()}:{route}";
                InferResource(route, out var resourceType, out var resourceHint);
                var displayName = ResolveDisplayName(endpoint, method, route);
                items.Add(new EndpointPermissionCatalogItem
                {
                    PermissionKey = key,
                    DisplayName = displayName,
                    Endpoint = key,
                    Method = method.ToUpperInvariant(),
                    RouteTemplate = route,
                    ResourceType = resourceType,
                    ResourceHint = resourceHint
                });
            }
        }

        return items
            .DistinctBy(i => i.PermissionKey)
            .OrderBy(i => i.PermissionKey)
            .ToList();
    }

    public string BuildPermissionKey(HttpContext context)
    {
        var endpoint = context.GetEndpoint() as RouteEndpoint;
        if (endpoint == null)
            return string.Empty;

        var method = context.Request.Method.ToUpperInvariant();
        var route = NormalizeRoute(endpoint.RoutePattern.RawText ?? string.Empty);
        return $"{method}:{route}";
    }

    public async Task<List<AccessResourceContextDto>> ExtractResourceContextsAsync(HttpContext context)
    {
        var result = new List<AccessResourceContextDto>();
        var endpoint = context.GetEndpoint() as RouteEndpoint;
        var route = NormalizeRoute(endpoint?.RoutePattern.RawText ?? string.Empty);

        AddRouteValueContext(context, result, "Hospital", "hospitalId", route.Contains("hospitals") || route.Contains("/hospital/") || route.Contains("{hospitalid}"));
        AddRouteValueContext(context, result, "Hospital", "id", route.StartsWith("/api/hospitals/"));

        AddRouteValueContext(context, result, "Patient", "patientId", route.Contains("patients"));
        AddRouteValueContext(context, result, "Patient", "id", route.Contains("patients/"));

        AddRouteValueContext(context, result, "ReservationSlot", "slotId", route.Contains("reservations/slots"));
        AddRouteValueContext(context, result, "ReservationSlot", "id", route.Contains("reservations/slots/"));

        AddRouteValueContext(context, result, "Employee", "employeeId", route.Contains("employees"));

        // Best-effort query support
        AddQueryValueContext(context, result, "Hospital", "hospitalId");
        AddQueryValueContext(context, result, "Patient", "patientId");

        await AddBodyContextsAsync(context, result);
        await AddResolvedHospitalContextsAsync(context, result);
        await AddResolvedCountryContextsAsync(context, result);

        return result
            .GroupBy(x => new { x.ResourceType, x.ResourceId })
            .Select(g => g.First())
            .ToList();
    }

    private static async Task AddBodyContextsAsync(HttpContext context, List<AccessResourceContextDto> result)
    {
        if (context.Request.ContentType == null || !context.Request.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return;

        if (context.Request.Method is not ("POST" or "PUT" or "PATCH"))
            return;

        context.Request.EnableBuffering();
        context.Request.Body.Position = 0;

        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(body))
            return;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;

            TryAddBodyContext(context, root, result, "hospitalId", "Hospital");
            TryAddBodyContext(context, root, result, "patientId", "Patient");
            TryAddBodyContext(context, root, result, "personId", "Patient");
            TryAddBodyContext(context, root, result, "employeeId", "Employee");
            TryAddBodyContext(context, root, result, "doctorId", "Employee");
            TryAddBodyContext(context, root, result, "slotId", "ReservationSlot");
            TryAddBodyContext(context, root, result, "reservationSlotId", "ReservationSlot");
            TryAddBodyContext(context, root, result, "examinationRoomId", "ExaminationRoom");
            TryAddBodyContext(context, root, result, "countryScopeId", "Country");
        }
        catch
        {
            // ignore malformed JSON for ACL extraction and fall back to route/query contexts
        }
    }

    private static void TryAddBodyContext(HttpContext context, System.Text.Json.JsonElement root, List<AccessResourceContextDto> result, string propertyName, string resourceType)
    {
        if (!root.TryGetProperty(propertyName, out var prop))
            return;

        if (prop.ValueKind == System.Text.Json.JsonValueKind.Number && prop.TryGetInt32(out var id) && id > 0)
        {
            context.Items[$"acl:{propertyName}"] = id;
            result.Add(new AccessResourceContextDto { ResourceType = resourceType, ResourceId = id });
        }
    }

    private static void AddRouteValueContext(HttpContext context, List<AccessResourceContextDto> result, string resourceType, string key, bool enabled)
    {
        if (!enabled)
            return;

        if (context.Request.RouteValues.TryGetValue(key, out var value) &&
            value != null &&
            int.TryParse(value.ToString(), out var id) &&
            id > 0)
        {
            result.Add(new AccessResourceContextDto { ResourceType = resourceType, ResourceId = id });
        }
    }

    private static void AddQueryValueContext(HttpContext context, List<AccessResourceContextDto> result, string resourceType, string key)
    {
        if (context.Request.Query.TryGetValue(key, out var value) &&
            int.TryParse(value.FirstOrDefault(), out var id) &&
            id > 0)
        {
            result.Add(new AccessResourceContextDto { ResourceType = resourceType, ResourceId = id });
        }
    }

    private static void InferResource(string route, out string? resourceType, out string? hint)
    {
        resourceType = null;
        hint = null;

        if (route.Contains("/hospital/") || route.Contains("{hospitalid}") || route.StartsWith("/api/hospitals/"))
        {
            resourceType = "Hospital";
            hint = "Scope by hospitalId";
            return;
        }

        if (route.Contains("hospitals"))
        {
            resourceType = "Hospital";
            hint = "Route: hospitalId or id";
            return;
        }

        if (route.Contains("patients"))
        {
            resourceType = "Patient";
            hint = "Route: patientId or id";
            return;
        }

        if (route.Contains("reservations/slots"))
        {
            resourceType = "Hospital";
            hint = "Route: hospitalId or resolved from slotId";
            return;
        }

        if (route.Contains("examinationrooms") || route.Contains("doctorexaminationrooms"))
        {
            resourceType = "Hospital";
            hint = "Route/query hospitalId or resolved from room assignment";
            return;
        }

        if (route.Contains("employees") || route.Contains("admin/employees"))
        {
            resourceType = "Employee";
            hint = "Route: employeeId";
        }
    }

    private static string NormalizeRoute(string route)
    {
        var normalized = route.Trim();
        if (!normalized.StartsWith("/"))
            normalized = "/" + normalized;
        return normalized.ToLowerInvariant();
    }

    private async Task AddResolvedHospitalContextsAsync(HttpContext context, List<AccessResourceContextDto> result)
    {
        await TryResolveHospitalContextAsync(context, result, "slotId", "/api/access-control/context/reservation-slots/{0}/hospital");
        await TryResolveHospitalContextAsync(context, result, "roomId", "/api/access-control/context/examination-rooms/{0}/hospital");
        await TryResolveHospitalContextAsync(context, result, "assignmentId", "/api/access-control/context/doctor-room-assignments/{0}/hospital");
        await TryResolveHospitalContextAsync(context, result, "examinationRoomId", "/api/access-control/context/examination-rooms/{0}/hospital");
    }

    private async Task AddResolvedCountryContextsAsync(HttpContext context, List<AccessResourceContextDto> result)
    {
        await TryResolveCountryContextAsync(context, result, "hospitalId", "/api/access-control/context/hospitals/{0}/country");
        await TryResolveCountryContextAsync(context, result, "id", "/api/access-control/context/hospitals/{0}/country");
        await TryResolveCountryContextAsync(context, result, "slotId", "/api/access-control/context/reservation-slots/{0}/country");
        await TryResolveCountryContextAsync(context, result, "roomId", "/api/access-control/context/examination-rooms/{0}/country");
        await TryResolveCountryContextAsync(context, result, "assignmentId", "/api/access-control/context/doctor-room-assignments/{0}/country");
        await TryResolveCountryContextAsync(context, result, "examinationRoomId", "/api/access-control/context/examination-rooms/{0}/country");
    }

    private async Task TryResolveHospitalContextAsync(HttpContext context, List<AccessResourceContextDto> result, string key, string endpointFormat)
    {
        if (!TryGetIntValue(context, key, out var resourceId) || resourceId <= 0)
            return;

        var client = _httpClientFactory.CreateClient("DatabaseAPI");
        var response = await client.GetAsync(string.Format(endpointFormat, resourceId));
        if (!response.IsSuccessStatusCode)
            return;

        var json = await response.Content.ReadAsStringAsync();
        var lookup = JsonSerializer.Deserialize<AccessResourceContextDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (lookup != null && lookup.ResourceId > 0 && !string.IsNullOrWhiteSpace(lookup.ResourceType))
        {
            result.Add(lookup);
        }
    }

    private async Task TryResolveCountryContextAsync(HttpContext context, List<AccessResourceContextDto> result, string key, string endpointFormat)
    {
        if (!TryGetIntValue(context, key, out var resourceId) || resourceId <= 0)
            return;

        var client = _httpClientFactory.CreateClient("DatabaseAPI");
        var response = await client.GetAsync(string.Format(endpointFormat, resourceId));
        if (!response.IsSuccessStatusCode)
            return;

        var json = await response.Content.ReadAsStringAsync();
        var lookup = JsonSerializer.Deserialize<AccessResourceContextDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (lookup != null && lookup.ResourceId > 0 && !string.IsNullOrWhiteSpace(lookup.ResourceType))
        {
            result.Add(lookup);
        }
    }

    private static bool TryGetIntValue(HttpContext context, string key, out int value)
    {
        value = 0;

        if (context.Request.RouteValues.TryGetValue(key, out var routeValue) &&
            routeValue != null &&
            int.TryParse(routeValue.ToString(), out value) &&
            value > 0)
        {
            return true;
        }

        if (context.Request.Query.TryGetValue(key, out var queryValue) &&
            int.TryParse(queryValue.FirstOrDefault(), out value) &&
            value > 0)
        {
            return true;
        }

        if (context.Items.TryGetValue($"acl:{key}", out var itemValue) && itemValue is int itemInt && itemInt > 0)
        {
            value = itemInt;
            return true;
        }

        return false;
    }

    private static string ResolveDisplayName(RouteEndpoint endpoint, string method, string route)
    {
        var permissionKey = $"{method.ToUpperInvariant()}:{route}";

        var explicitDisplayName = endpoint.Metadata
            .GetMetadata<PermissionDisplayNameAttribute>()?
            .DisplayName;

        if (!string.IsNullOrWhiteSpace(explicitDisplayName))
            return explicitDisplayName!;

        if (PermissionCatalogDisplayNames.ByPermissionKey.TryGetValue(permissionKey, out var registeredDisplayName))
            return registeredDisplayName;

        var methodInfo = endpoint.Metadata.GetMetadata<MethodInfo>();
        if (methodInfo != null)
        {
            var label = HumanizeMethodName(methodInfo.Name);
            if (!string.IsNullOrWhiteSpace(label))
                return label;
        }

        return HumanizeRoute(method, route);
    }

    private static string HumanizeMethodName(string methodName)
    {
        var name = methodName.EndsWith("Async", StringComparison.Ordinal)
            ? methodName[..^5]
            : methodName;

        name = Regex.Replace(name, "(?<!^)([A-Z])", " $1");
        name = Regex.Replace(name, "\\s+", " ").Trim();

        if (name.StartsWith("Get All ", StringComparison.OrdinalIgnoreCase))
            return "List " + name[8..];

        if (name.StartsWith("Get ", StringComparison.OrdinalIgnoreCase))
            return "View " + name[4..];

        return name;
    }

    private static string HumanizeRoute(string method, string route)
    {
        var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.StartsWith('{') ? "item" : s)
            .Select(s => s.Replace('-', ' '))
            .Select(s => string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => char.ToUpperInvariant(x[0]) + x[1..])))
            .ToList();

        var action = method.ToUpperInvariant() switch
        {
            "GET" => "View",
            "POST" => "Create",
            "PUT" => "Update",
            "PATCH" => "Patch",
            "DELETE" => "Delete",
            _ => "Use"
        };

        if (segments.Count == 0)
            return action + " Endpoint";

        return action + " " + string.Join(" ", segments);
    }
}
