
using System;
using System.ComponentModel.DataAnnotations;

namespace RaceDayMVC.Models
{
    public class ProfileViewModel
    {
        public int UserID { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(100)]
        [Display(Name = "Email Address")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Role { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(20)]
        [Display(Name = "Gender")]
        public string? Gender { get; set; }

        [StringLength(100)]
        [Display(Name = "Emergency Contact Name")]
        public string? EmergencyContactName { get; set; }

        [StringLength(20)]
        [Display(Name = "Emergency Contact Number")]
        public string? EmergencyContactNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Organisation Name")]
        public string? OrganisationName { get; set; }

        [StringLength(100)]
        [Display(Name = "Organisation Contact")]
        public string? OrganisationContact { get; set; }

        public string? ProfilePictureUri { get; set; }
    }
}
