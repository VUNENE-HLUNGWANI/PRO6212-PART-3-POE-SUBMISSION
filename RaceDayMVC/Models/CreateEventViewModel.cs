using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RaceDayMVC.Models
{
    public class CreateEventViewModel
    {
        [Required(ErrorMessage = "Event name is required.")]
        [StringLength(150)]
        [Display(Name = "Event Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Event date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Event Date")]
        public DateTime? Date { get; set; }

        [Required(ErrorMessage = "Location is required.")]
        [StringLength(150)]
        [Display(Name = "Location")]
        public string Location { get; set; } = string.Empty;

        [Required(ErrorMessage = "Start time is required.")]
        [DataType(DataType.Time)]
        [Display(Name = "Start Time")]
        public TimeSpan? StartTime { get; set; }

        [Required(ErrorMessage = "Route map information is required.")]
        [StringLength(255)]
        [Display(Name = "Route Map")]
        public string Routemap { get; set; } = string.Empty;

        [Required(ErrorMessage = "Event type is required.")]
        [StringLength(50)]
        [Display(Name = "Event Type")]
        public string EventType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Distance is required.")]
        [Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
        [Display(Name = "Distance (km)")]
        public decimal? Distance { get; set; }

        [Required(ErrorMessage = "Registration deadline is required.")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Registration Deadline")]
        public DateTime? RegistrationDeadline { get; set; }

        [Display(Name = "Banner Image (Optional)")]
        public IFormFile? BannerImage { get; set; }
    }
}