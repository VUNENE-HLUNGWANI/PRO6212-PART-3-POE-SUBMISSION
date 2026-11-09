using System;
using System.Collections.Generic;
using System.Linq;

namespace RaceDayMVC.Models
{
    public class ResultsEntryViewModel
    {
        public int EventId { get; set; }

        public string EventName { get; set; } = string.Empty;

        public DateTime? EventDate { get; set; }

        public List<ResultRowViewModel> Rows { get; set; } = new();

        public int RecordedCount => Rows.Count(r => r.ResultId > 0);

        public int TotalCount => Rows.Count;
    }

    public class ResultRowViewModel
    {
        public int ParticipantEventId { get; set; }

        public int ResultId { get; set; }

        public string Bib { get; set; } = string.Empty;

        public string ParticipantName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public string Gender { get; set; } = string.Empty;

        public string CategoryGender { get; set; } = string.Empty;

        public string? Time { get; set; }

        public int? Position { get; set; }

        public string Status { get; set; } = ResultStatuses.Finished;

        public string? Reason { get; set; }

        // True when the category is limited to one gender and the participant does not match.
        public bool IsGenderMismatch =>
            !string.IsNullOrWhiteSpace(CategoryGender) &&
            !string.Equals(Gender, CategoryGender, StringComparison.OrdinalIgnoreCase);
    }

    public static class ResultStatuses
    {
        public const string Finished = "Finished";

        public const string DSQ = "DSQ";

        // Suggested message for a disqualification. The organiser can change it.
        public const string GenderMismatchReason =
            "This race is gender specific and you do not qualify.";

        public static readonly string[] All =
        {
            Finished,
            "DNF",
            "DNS",
            DSQ
        };

        // Returns a valid status, or null if the value is not recognised.
        // A blank value is treated as Finished.
        public static string? Normalise(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return Finished;
            }

            return All.FirstOrDefault(s =>
                s.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static string Describe(string status)
        {
            return status switch
            {
                "Finished" => "Finished",
                "DNF" => "Did not finish (DNF)",
                "DNS" => "Did not start (DNS)",
                "DSQ" => "Disqualified (DSQ)",
                _ => status
            };
        }
    }
}