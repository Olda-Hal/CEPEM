namespace DatabaseAPI.DatabaseModels;

/// <summary>
/// Reservation slot lifecycle. Stored as a MySQL enum; previously free-text
/// strings compared case-insensitively in code.
/// </summary>
public enum SlotStatus
{
    Available,
    Blocked,
    Reserved,
    Unavailable
}

/// <summary>
/// Legacy (pre-slot) reservation status.
/// </summary>
public enum ReservationStatus
{
    Scheduled,
    Cancelled
}
