namespace RaceDayMVC.Models
{
    public class EditEventViewModel : CreateEventViewModel
    {
        public int EventId { get; set; }

        public string? CurrentBannerUri { get; set; }
    }
}