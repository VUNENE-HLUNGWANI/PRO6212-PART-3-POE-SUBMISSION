using System;

namespace RaceDayMVC.Models
{
    public class EventDto
    {
        public int EventID { get; set; }

        public int EventOrganiserID { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        public string Location { get; set; } = string.Empty;

        public TimeSpan StartTime { get; set; }

        public string Routemap { get; set; } = string.Empty;

        public string EventType { get; set; } = string.Empty;

        public decimal Distance { get; set; }

        public DateTime RegistrationDeadline { get; set; }

        public string? BannerImageUri { get; set; }
    }
}