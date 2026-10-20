using System.Security.Claims;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace HealthcareAPI.Proxy;

/// <summary>
/// YARP pass-through configuration for routes that HealthcareAPI used to
/// proxy by hand (copy-paste controller actions). Permission checks are
/// enforced via authorization policies named "perm:{code}".
/// </summary>
public static class ProxyConfig
{
    private const string ClusterId = "databaseApi";

    public static RouteConfig[] Routes { get; } = BuildRoutes().ToArray();

    public static ClusterConfig[] GetClusters(string databaseApiUrl)
    {
        return new[]
        {
            new ClusterConfig
            {
                ClusterId = ClusterId,
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    { "default", new DestinationConfig { Address = databaseApiUrl } }
                }
            }
        };
    }

    private static IEnumerable<RouteConfig> BuildRoutes()
    {
        // Patients
        yield return Route("patients-get", "/api/patients/{**rest}", "GET", "patients.read");
        yield return Route("patients-post", "/api/patients", "POST", "patients.write");
        yield return Route("patients-post-photo", "/api/patients/{id}/photo", "POST", "patients.photo.write");
        yield return Route("patients-post-documents", "/api/patients/{id}/documents", "POST", "patients.documents.write");
        yield return Route("patients-put", "/api/patients/{**rest}", "PUT", "patients.write");
        yield return Route("patients-patch", "/api/patients/{**rest}", "PATCH", "patients.write");
        yield return Route("patients-delete-photo", "/api/patients/{id}/photo", "DELETE", "patients.photo.write");
        yield return Route("patients-delete-documents", "/api/patients/{patientId}/documents/{documentId}", "DELETE", "patients.documents.write");

        // Events (token-based intake form links stay anonymous)
        yield return Route("events-options-get", "/api/events/options", "GET", "events.read");
        yield return Route("events-post", "/api/events/{**rest}", "POST", "events.write");
        yield return Route("events-link-get", "/api/events/intake-form-links/{token}");
        yield return Route("events-link-submit", "/api/events/intake-form-links/{token}/submit", "POST");

        // Hospitals
        yield return Route("hospitals-get", "/api/hospitals/{**rest}", "GET", "hospitals.read");
        yield return Route("hospitals-get-exam-types", "/api/hospitals/{hospitalId}/examination-types", "GET", "examinationtypes.read");
        yield return Route("hospitals-post", "/api/hospitals", "POST", "hospitals.write");
        yield return Route("hospitals-put", "/api/hospitals/{hospitalId}", "PUT", "hospitals.write");
        yield return Route("hospitals-put-exam-types", "/api/hospitals/{hospitalId}/examination-types", "PUT", "examinationtypes.write");
        yield return Route("hospitals-delete", "/api/hospitals/{hospitalId}", "DELETE", "hospitals.write");

        // Examination types
        yield return Route("examinationtypes-get", "/api/examinationtypes/{**rest}", "GET", "examinationtypes.read");
        yield return Route("examinationtypes-post", "/api/examinationtypes", "POST", "examinationtypes.manage");
        yield return Route("examinationtypes-put", "/api/examinationtypes/{**rest}", "PUT", "examinationtypes.manage");
        yield return Route("examinationtypes-delete", "/api/examinationtypes/{**rest}", "DELETE", "examinationtypes.manage");

        // Examination rooms
        yield return Route("examinationrooms-get", "/api/examinationrooms/{**rest}", "GET", "examinationrooms.read");
        yield return Route("examinationrooms-post", "/api/examinationrooms", "POST", "examinationrooms.write");
        yield return Route("examinationrooms-put", "/api/examinationrooms/{**rest}", "PUT", "examinationrooms.write");
        yield return Route("examinationrooms-delete", "/api/examinationrooms/{**rest}", "DELETE", "examinationrooms.write");

        // Doctor examination rooms
        yield return Route("doctorrooms-get", "/api/doctorexaminationrooms/{**rest}", "GET", "doctorexaminationrooms.read");
        yield return Route("doctorrooms-post", "/api/doctorexaminationrooms", "POST", "doctorexaminationrooms.write");
        yield return Route("doctorrooms-delete", "/api/doctorexaminationrooms/{**rest}", "DELETE", "doctorexaminationrooms.write");

        // Examinations + their documents
        yield return Route("examinations-get", "/api/examinations", "GET", "examinations.read");
        yield return Route("examinations-export", "/api/examinations/export", "GET", "examinations.export");
        yield return Route("examination-documents-get", "/api/examinations/{examinationId}/documents/{**rest}", "GET", "examinations.documents.read");
        yield return Route("examination-documents-post", "/api/examinations/{examinationId}/documents", "POST", "examinations.documents.write");
        yield return Route("examination-documents-delete", "/api/examinations/{examinationId}/documents/{**rest}", "DELETE", "examinations.documents.write");

        // Reservation slots (confirm stays in ReservationSlotsController - business logic)
        yield return Route("slots-get", "/api/reservations/slots/{**rest}", "GET", "reservations.read");
        yield return Route("slots-post", "/api/reservations/slots", "POST", "reservations.write");
        yield return Route("slots-copy-day", "/api/reservations/slots/hospital/{hospitalId}/copy-day", "POST", "reservations.write");
        yield return Route("slots-put", "/api/reservations/slots/{**rest}", "PUT", "reservations.write");
        yield return Route("slots-release", "/api/reservations/slots/{slotId}/release", "POST", "reservations.write");
        yield return Route("slots-block", "/api/reservations/slots/{slotId}/block", "POST", "reservations.write");
        yield return Route("slots-reject", "/api/reservations/slots/{slotId}/reject", "POST", "reservations.write");
        yield return Route("slots-delete", "/api/reservations/slots/{**rest}", "DELETE", "reservations.write");

        // Settings
        yield return Route("settings-get", "/api/settings/{**rest}", "GET", "settings.read");
        yield return Route("settings-put", "/api/settings/{**rest}", "PUT", "settings.write");
    }

    private static RouteConfig Route(string id, string path, string? method = null, string? permissionCode = null)
    {
        return new RouteConfig
        {
            RouteId = id,
            ClusterId = ClusterId,
            AuthorizationPolicy = permissionCode == null ? null : $"perm:{permissionCode}",
            Match = new RouteMatch
            {
                Path = path,
                Methods = method == null ? null : new[] { method }
            }
        };
    }

    /// <summary>
    /// Adds the acting employee's identity to downstream requests via
    /// X-Actor headers, mirroring what AuthenticationHandler does for the
    /// named "DatabaseAPI" HttpClient used by the remaining MVC controllers.
    /// Incoming X-Actor headers are always stripped to prevent spoofing.
    /// </summary>
    public static void AddClaimsTransform(IReverseProxyBuilder builder)
    {
        builder.AddTransforms(context =>
        {
            context.AddRequestTransform(requestContext =>
            {
                var request = requestContext.ProxyRequest;
                request.Headers.Remove("X-Actor-Employee-Id");
                request.Headers.Remove("X-Actor-Country-Code");

                var user = requestContext.HttpContext.User;
                if (user.Identity?.IsAuthenticated != true)
                {
                    return ValueTask.CompletedTask;
                }

                var employeeId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (employeeId != null)
                {
                    request.Headers.Add("X-Actor-Employee-Id", employeeId);
                }

                var countryCode = user.FindFirst("country_code")?.Value;
                if (!string.IsNullOrEmpty(countryCode))
                {
                    request.Headers.Add("X-Actor-Country-Code", countryCode);
                }

                return ValueTask.CompletedTask;
            });
        });
    }
}
