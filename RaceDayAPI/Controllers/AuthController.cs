using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using RaceDayAPI.Services;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace RaceDayAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(
            RaceDayDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (model.Role != "Organiser" && model.Role != "Participant")
            {
                return BadRequest(new
                {
                    message = "Role must be Organiser or Participant."
                });
            }

            string email = model.Email.Trim();

            if (model.Role == "Organiser")
            {
                string? requiredCode =
                    _configuration["Registration:OrganiserCode"];

                string submittedCode =
                    model.OrganiserCode?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(requiredCode))
                {
                    return StatusCode(500, new
                    {
                        message = "Organiser registration is not configured."
                    });
                }

                if (!string.Equals(
                    submittedCode,
                    requiredCode.Trim(),
                    StringComparison.Ordinal))
                {
                    return StatusCode(403, new
                    {
                        message = "The organiser registration code is incorrect."
                    });
                }

                if (string.IsNullOrWhiteSpace(model.OrganisationName) ||
                    string.IsNullOrWhiteSpace(model.OrganisationContact))
                {
                    return BadRequest(new
                    {
                        message = "Organisation name and contact are required."
                    });
                }
            }
            else
            {
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
            }

            bool emailExists = await _context.Users.AnyAsync(
                u => u.Email.ToLower() == email.ToLower());

            if (emailExists)
            {
                return BadRequest(new
                {
                    message = "Email already exists."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var user = new User
                {
                    FirstName = model.FirstName.Trim(),
                    LastName = model.LastName.Trim(),
                    Email = email,
                    Password = PasswordHasher.Hash(model.Password),
                    PhoneNumber = model.PhoneNumber.Trim(),
                    Role = model.Role,
                    RegistrationDate = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                if (model.Role == "Organiser")
                {
                    _context.EventOrganisers.Add(new EventOrganiser
                    {
                        EventOrganiserID = user.UserID,
                        OrganisationName = model.OrganisationName!.Trim(),
                        OrganisationContact = model.OrganisationContact!.Trim()
                    });
                }
                else
                {
                    _context.Participants.Add(new Participant
                    {
                        ParticipantID = user.UserID,
                        DateOfBirth = model.DateOfBirth!.Value,
                        Gender = model.Gender!.Trim(),
                        EmergencyContactName =
                            model.EmergencyContactName!.Trim(),
                        EmergencyContactNumber =
                            model.EmergencyContactNumber!.Trim()
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return StatusCode(201, new
                {
                    message = "User registered successfully.",
                    userId = user.UserID,
                    email = user.Email
                });
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                return Conflict(new
                {
                    message = "Registration could not be completed because of a database error."
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            string email = model.Email.Trim();

            var user = await _context.Users.FirstOrDefaultAsync(
                u => u.Email.ToLower() == email.ToLower());

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            bool validPassword;

            if (PasswordHasher.IsHashed(user.Password))
            {
                validPassword = PasswordHasher.Verify(
                    model.Password,
                    user.Password);
            }
            else
            {
                validPassword = user.Password == model.Password;

                if (validPassword)
                {
                    user.Password = PasswordHasher.Hash(model.Password);
                    await _context.SaveChangesAsync();
                }
            }

            if (!validPassword)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            HttpContext.Session.SetInt32("UserId", user.UserID);
            HttpContext.Session.SetString("Email", user.Email);
            HttpContext.Session.SetString("Role", user.Role);

            return Ok(new
            {
                message = "Login successful.",
                userId = user.UserID,
                email = user.Email,
                role = user.Role,
                firstName = user.FirstName,
                lastName = user.LastName
            });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            return Ok(new
            {
                message = "Logged out successfully."
            });
        }
    }

    public class LoginModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterModel
    {
        [Required, MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(8), MaxLength(100)]
        public string Password { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Role { get; set; } = string.Empty;

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

        public string? OrganiserCode { get; set; }
    }
}