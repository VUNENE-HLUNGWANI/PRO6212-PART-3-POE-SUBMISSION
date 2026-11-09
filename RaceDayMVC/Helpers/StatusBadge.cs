namespace RaceDayMVC.Helpers;

public static class StatusBadge
{
    // Maps an enrolment status to its colour-coded badge class.
    public static string CssClass(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "confirmed" => "rd-badge-success",
            "pending" => "rd-badge-pending",
            "cancelled" or "canceled" or "withdrawn" => "rd-badge-danger",
            "completed" => "rd-badge-success",
            "no-show" => "rd-badge-warn",
            _ => "rd-badge-soft"
        };
}

