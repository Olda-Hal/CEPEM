using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;

namespace DatabaseAPI.Utils;

/// <summary>
/// Row-level patient scope for administrative roles. A Country Admin only
/// sees patients from administered countries; a Center Admin only patients
/// whose home center they administer. SysAdmin/Admin, doctors and regular
/// staff keep the clinic-wide view.
/// </summary>
public static class PatientScope
{
    public static IQueryable<Patient> Apply(IQueryable<Patient> query, ActorScope actor)
    {
        if (!actor.IsScopedAdmin)
        {
            return query;
        }

        if (actor.IsCountryAdmin)
        {
            var countryCodes = CountryCodes(actor);
            return countryCodes.Count == 0
                ? query.Where(patient => patient.Id == -1)
                : query.Where(patient =>
                    patient.CountryCode != null && countryCodes.Contains(patient.CountryCode));
        }

        var hospitalIds = actor.HospitalIds;
        // Legacy records without a home center are not assigned to any center,
        // so a Center Admin must not see them; Country/Full admins still can.
        return query.Where(patient =>
            patient.HomeHospitalId != null &&
            hospitalIds.Contains(patient.HomeHospitalId.Value));
    }

    public static IQueryable<Visit> ApplyToVisits(IQueryable<Visit> query, ActorScope actor)
    {
        if (!actor.IsScopedAdmin)
        {
            return query;
        }

        if (actor.IsCountryAdmin)
        {
            var countryCodes = CountryCodes(actor);
            return countryCodes.Count == 0
                ? query.Where(visit => visit.Id == -1)
                : query.Where(visit =>
                    visit.Patient.CountryCode != null &&
                    countryCodes.Contains(visit.Patient.CountryCode));
        }

        var hospitalIds = actor.HospitalIds;
        return query.Where(visit =>
            visit.Patient.HomeHospitalId != null &&
            hospitalIds.Contains(visit.Patient.HomeHospitalId.Value));
    }

    /// <summary>True when the patient is inside the actor's administrative scope.</summary>
    public static bool CanAccess(Patient patient, ActorScope actor)
    {
        if (!actor.IsScopedAdmin)
        {
            return true;
        }

        if (actor.IsCountryAdmin)
        {
            return patient.CountryCode != null &&
                   CountryCodes(actor).Contains(patient.CountryCode);
        }

        return patient.HomeHospitalId != null &&
               actor.HospitalIds.Contains(patient.HomeHospitalId.Value);
    }

    private static List<string> CountryCodes(ActorScope actor) =>
        actor.AdministeredCountryScopeIds
            .Select(CountryScopeMapper.ToCountryCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
