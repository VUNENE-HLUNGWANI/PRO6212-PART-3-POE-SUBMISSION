using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Filters;
using RaceDayAPI.Services;
using System;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace RaceDayAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [SessionAuthorize]
    public class ProfileController : ControllerBase
    {
        private const long MaxPictureSizeBytes = 5 * 1024 * 1024;

        private static readonly string[] AllowedPictureTypes =
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        private readonly RaceDayDbContext _context;
        private readonly IBlobStorageService _blobStorage;

        public ProfileController(
            RaceDayDbContext context,
            IBlobStorageService blobStorage)
        {
            _context = context;
            _blobStorage = blobStorage;
        }

        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            if (userId <= 0)
                return Unauthorized(new { message = "Please log in again." });

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null)
                return NotFound(new { message = "User not found." });

            var participant =
                await _context.Participants.FindAsync(userId);

            var organiser =
                await _context.EventOrganisers.FindAsync(userId);

            return Ok(new ProfileModel
            {
                UserID = user.UserID,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                ProfilePictureUri = user.ProfilePictureUri,

                DateOfBirth = participant?.DateOfBirth,
                Gender = participant?.Gender,
                EmergencyContactName = participant?.EmergencyContactName,
                EmergencyContactNumber = participant?.EmergencyContactNumber,

                OrganisationName = organiser?.OrganisationName,
                OrganisationContact = organiser?.OrganisationContact
            });
        }

        [HttpPut]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] ProfileModel model)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            if (userId <= 0)
                return Unauthorized(new { message = "Please log in again." });

            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return NotFound(new { message = "User not found." });

            string newEmail = model.Email?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(newEmail))
                return BadRequest(new { message = "Email address is required." });

            if (!new EmailAddressAttribute().IsValid(newEmail))
                return BadRequest(new { message = "Enter a valid email address." });

            if (newEmail.Length > 100)
                return BadRequest(new { message = "Email address is too long." });

            bool emailTaken = await _context.Users.AnyAsync(
                u => u.UserID != userId &&
                     u.Email.ToLower() == newEmail.ToLower());

            if (emailTaken)
            {
                return BadRequest(new
                {
                    message = "That email address is already used by another account."
                });
            }

            if (string.IsNullOrWhiteSpace(model.FirstName) ||
                string.IsNullOrWhiteSpace(model.LastName) ||
                string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                return BadRequest(new
                {
                    message = "First name, last name and phone number are required."
                });
            }

            string role = user.Role;

            if (string.Equals(
                role, "Participant", StringComparison.OrdinalIgnoreCase))
            {
                var participant =
                    await _context.Participants.FindAsync(userId);

                if (participant == null)
                    return NotFound(new { message = "Participant profile not found." });

                if (model.DateOfBirth == null ||
                    string.IsNullOrWhiteSpace(model.Gender) ||
                    string.IsNullOrWhiteSpace(model.EmergencyContactName) ||
                    string.IsNullOrWhiteSpace(model.EmergencyContactNumber))
                {
                    return BadRequest(new
                    {
                        message = "Date of birth, gender and emergency contact details are required."
                    });
                }

                if (model.DateOfBirth.Value.Date > DateTime.Today)
                {
                    return BadRequest(new
                    {
                        message = "Date of birth cannot be in the future."
                    });
                }

                participant.DateOfBirth = model.DateOfBirth.Value;
                participant.Gender = model.Gender.Trim();
                participant.EmergencyContactName =
                    model.EmergencyContactName.Trim();
                participant.EmergencyContactNumber =
                    model.EmergencyContactNumber.Trim();
            }
            else if (string.Equals(
                role, "Organiser", StringComparison.OrdinalIgnoreCase))
            {
                var organiser =
                    await _context.EventOrganisers.FindAsync(userId);

                if (organiser == null)
                    return NotFound(new { message = "Organiser profile not found." });

                if (string.IsNullOrWhiteSpace(model.OrganisationName) ||
                    string.IsNullOrWhiteSpace(model.OrganisationContact))
                {
                    return BadRequest(new
                    {
                        message = "Organisation name and contact are required."
                    });
                }

                organiser.OrganisationName = model.OrganisationName.Trim();
                organiser.OrganisationContact = model.OrganisationContact.Trim();
            }
            else
            {
                return BadRequest(new { message = "Invalid account role." });
            }

            // Update the actual account record.
            user.FirstName = model.FirstName.Trim();
            user.LastName = model.LastName.Trim();
            user.PhoneNumber = model.PhoneNumber.Trim();
            user.Email = newEmail;

            try
            {
                await _context.SaveChangesAsync();

                // Read the saved record again from the database.
                var savedUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserID == userId);

                if (savedUser == null ||
                    !string.Equals(
                        savedUser.Email,
                        newEmail,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(500, new
                    {
                        message = "The profile update could not be verified in the database."
                    });
                }

                // Keep the API session email in sync.
                HttpContext.Session.SetString("Email", savedUser.Email);

                return Ok(new
                {
                    message = "Profile updated and saved successfully.",
                    userId = savedUser.UserID,
                    email = savedUser.Email
                });
            }
            catch (DbUpdateException)
            {
                return Conflict(new
                {
                    message = "The profile could not be saved. The email may already be in use."
                });
            }
        }

        [HttpPost("picture")]
        public async Task<IActionResult> UploadPicture(IFormFile file)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            if (userId <= 0)
                return Unauthorized(new { message = "Please log in again." });

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No image file was uploaded." });

            if (file.Length > MaxPictureSizeBytes)
                return BadRequest(new { message = "Profile picture must be 5 MB or smaller." });

            if (!AllowedPictureTypes.Contains(
                file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Profile picture must be a JPG, PNG or WebP file."
                });
            }

            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return NotFound(new { message = "User not found." });

            using var stream = file.OpenReadStream();

            user.ProfilePictureUri = await _blobStorage.UploadImageAsync(
                stream,
                file.FileName,
                file.ContentType);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                profilePictureUri = user.ProfilePictureUri
            });
        }
    }

    public class ProfileModel
    {
        public int UserID { get; set; }

        [Required, MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(100)]
        public string? Email { get; set; }

        [Required, MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Role { get; set; }

        public string? ProfilePictureUri { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [MaxLength(20)]
        public string? Gender { get; set; }

        [MaxLength(100)]
        public string? EmergencyContactName { get; set; }

        [MaxLength(20)]
        public string? EmergencyContactNumber { get; set; }

        [MaxLength(100)]
        public string? OrganisationName { get; set; }

        [MaxLength(100)]
        public string? OrganisationContact { get; set; }
    }
}