namespace HealthcareAPI.Services;

public static class PermissionCatalogDisplayNames
{
    public static readonly IReadOnlyDictionary<string, string> ByPermissionKey =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["POST:/api/auth/change-password"] = "Change Own Password",
            ["POST:/api/auth/create-employee"] = "Create Employee Account",
            ["GET:/api/auth/next-uid"] = "Get Next Employee UID",

            ["GET:/api/admin/employees"] = "List Employees",
            ["GET:/api/admin/employees/{employeeid}"] = "View Employee Detail",
            ["PUT:/api/admin/employees/{employeeid}"] = "Update Employee",
            ["PATCH:/api/admin/employees/{employeeid}/deactivate"] = "Deactivate Employee",
            ["GET:/api/admin/roles"] = "List Roles",
            ["POST:/api/admin/roles"] = "Create Role",
            ["GET:/api/admin/access/permissions"] = "List Permission Catalog",
            ["GET:/api/admin/access/employees/{employeeid}/rules"] = "View Employee Access Rules",
            ["PUT:/api/admin/access/employees/{employeeid}/rules"] = "Update Employee Access Rules",
            ["GET:/api/admin/access/roles/{roleid}/rules"] = "View Role Access Rules",
            ["PUT:/api/admin/access/roles/{roleid}/rules"] = "Update Role Access Rules",

            ["GET:/api/employees/me"] = "View Own Employee Profile",
            ["GET:/api/employees/dashboard-stats"] = "View Dashboard Stats",

            ["GET:/api/events/options"] = "List Event Options",
            ["POST:/api/events"] = "Create Event",
            ["POST:/api/events/examination-types"] = "Create Event Examination Type",
            ["POST:/api/events/symptoms"] = "Create Event Symptom",
            ["POST:/api/events/injury-types"] = "Create Event Injury Type",
            ["POST:/api/events/vaccine-types"] = "Create Event Vaccine Type",
            ["POST:/api/events/group"] = "Create Event Group",
            ["POST:/api/events/drugs"] = "Create Drug",
            ["POST:/api/events/drug-categories"] = "Create Drug Category",
            ["POST:/api/events/intake-form"] = "Create Intake Form Event",
            ["POST:/api/events/intake-form-links"] = "Create Intake Form Link",

            ["GET:/api/hospitals"] = "List Hospitals",
            ["GET:/api/hospitals/{hospitalid}/examination-types"] = "List Hospital Examination Types",
            ["PUT:/api/hospitals/{hospitalid}/examination-types"] = "Set Hospital Examination Types",
            ["POST:/api/hospitals"] = "Create Hospital",
            ["PUT:/api/hospitals/{hospitalid}"] = "Update Hospital",
            ["DELETE:/api/hospitals/{hospitalid}"] = "Deactivate Hospital",

            ["GET:/api/examinationtypes"] = "List Examination Types",
            ["GET:/api/examinationtypes/{id}"] = "View Examination Type",
            ["POST:/api/examinationtypes"] = "Create Examination Type",
            ["PUT:/api/examinationtypes/{id}"] = "Update Examination Type",
            ["DELETE:/api/examinationtypes/{id}"] = "Delete Examination Type",

            ["GET:/api/examinationrooms/hospital/{hospitalid}"] = "List Hospital Examination Rooms",
            ["POST:/api/examinationrooms"] = "Create Examination Room",
            ["PUT:/api/examinationrooms/{roomid}"] = "Update Examination Room",
            ["DELETE:/api/examinationrooms/{roomid}"] = "Deactivate Examination Room",

            ["GET:/api/doctorexaminationrooms/doctor/{doctorid}"] = "List Doctor Room Assignments",
            ["GET:/api/doctorexaminationrooms/doctor/{doctorid}/hospitals"] = "List Doctor Hospitals",
            ["GET:/api/doctorexaminationrooms/doctor/{doctorid}/rooms"] = "List Doctor Rooms",
            ["GET:/api/doctorexaminationrooms/room/{roomid}/doctors"] = "List Room Doctors",
            ["POST:/api/doctorexaminationrooms"] = "Assign Doctor To Room",
            ["DELETE:/api/doctorexaminationrooms/{assignmentid}"] = "Remove Doctor Room Assignment",

            ["GET:/api/examinations/{examinationid}/documents/{documentid}"] = "Download Examination Document",
            ["POST:/api/examinations/{examinationid}/documents"] = "Upload Examination Document",
            ["GET:/api/examinations"] = "List Examinations",
            ["GET:/api/examinations/export"] = "Export Examinations",

            ["GET:/api/patients/search"] = "Search Patients",
            ["POST:/api/patients"] = "Create Patient",
            ["GET:/api/patients/{id}"] = "View Patient",
            ["GET:/api/patients/{id}/detail"] = "View Patient Detail",
            ["PUT:/api/patients/{id}"] = "Update Patient",
            ["POST:/api/patients/{id}/photo"] = "Upload Patient Photo",
            ["DELETE:/api/patients/{id}/photo"] = "Delete Patient Photo",
            ["POST:/api/patients/{id}/documents"] = "Upload Patient Document",
            ["GET:/api/patients/{id}/documents"] = "List Patient Documents",
            ["DELETE:/api/patients/{patientid}/documents/{documentid}"] = "Delete Patient Document",
            ["PATCH:/api/patients/{id}/comment"] = "Update Patient Comment",

            ["GET:/api/reservations/slots/hospital/{hospitalid}"] = "List Reservation Slots",
            ["POST:/api/reservations/slots"] = "Create Reservation Slots",
            ["POST:/api/reservations/slots/hospital/{hospitalid}/copy-day"] = "Copy Reservation Slots Day",
            ["PUT:/api/reservations/slots/{slotid}"] = "Update Reservation Slot",
            ["POST:/api/reservations/slots/{slotid}/release"] = "Release Reservation Slot",
            ["DELETE:/api/reservations/slots/{slotid}"] = "Delete Reservation Slot",
            ["POST:/api/reservations/slots/{slotid}/block"] = "Block Reservation Slot",
            ["POST:/api/reservations/slots/{slotid}/confirm"] = "Confirm Reservation Slot",
            ["POST:/api/reservations/slots/{slotid}/reject"] = "Reject Reservation Slot",

            ["GET:/api/settings/quick-preview"] = "View Quick Preview Settings",
            ["PUT:/api/settings/quick-preview"] = "Update Quick Preview Settings"
        };
}
