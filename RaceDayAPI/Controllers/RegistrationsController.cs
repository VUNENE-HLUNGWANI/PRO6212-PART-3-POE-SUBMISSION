using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Filters;
using RaceDayAPI.Models;
using System.ComponentModel.DataAnnotations;

namespace RaceDayAPI.Controllers
{
    [Route("api/enrolments")]
    [ApiController]
    public class RegistrationsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public RegistrationsController(RaceDayDbContext context)
        {
            _context = context;
        }

        // GET: api/enrolments
        // Organisers can view registrations for their own events.
        [HttpGet]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> GetRegistrations()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");

            var registrations = await _context.ParticipantEvents
                .Include(pe => pe.Participant)
                .Include(pe => pe.Event)
                .Include(pe => pe.Category)
                .Where(pe => pe.Event != null &&
                             pe.Event.EventOrganiserID == userId)
                .ToListAsync();

            return Ok(registrations);
        }

        // GET: api/enrolments/mine
        // Returns the participant's active and historical registrations.
        [HttpGet("mine")]
        [SessionAuthorize("Participant")]
        public async Task<IActionResult> GetMyRegistrations()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");

            var registrations = await _context.ParticipantEvents
                .Include(pe => pe.Event)
                .Include(pe => pe.Category)
                .Where(pe => pe.ParticipantID == userId)
                .OrderBy(pe => pe.Event!.Date)
                .ToListAsync();

            return Ok(registrations);
        }

        // GET: api/enrolments/{id}
        [HttpGet("{id}")]
        [SessionAuthorize]
        public async Task<IActionResult> GetRegistration(int id)
        {
            var registration = await _context.ParticipantEvents
                .Include(pe => pe.Participant)
                .Include(pe => pe.Event)
                .Include(pe => pe.Category)
                .FirstOrDefaultAsync(
                    pe => pe.ParticipantEventID == id);

            if (registration == null)
            {
                return NotFound(new
                {
                    message = "Registration not found."
                });
            }

            int? userId = HttpContext.Session.GetInt32("UserId");
            string? role = HttpContext.Session.GetString("Role");

            if (role == "Participant" &&
                registration.ParticipantID != userId)
            {
                return Forbid();
            }

            if (role == "Organiser" &&
                registration.Event?.EventOrganiserID != userId)
            {
                return Forbid();
            }

            return Ok(registration);
        }

        // GET: api/events/{eventId}/enrolments
        // Organisers can view registrations for their own events.
        [HttpGet("/api/events/{eventId}/enrolments")]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> GetRegistrationsByEvent(int eventId)
        {
            var raceEvent = await _context.Events.FindAsync(eventId);

            if (raceEvent == null)
            {
                return NotFound(new
                {
                    message = "Event not found."
                });
            }

            if (raceEvent.EventOrganiserID !=
                HttpContext.Session.GetInt32("UserId"))
            {
                return StatusCode(403, new
                {
                    message = "You can only view registrations for your own events."
                });
            }

            var registrations = await _context.ParticipantEvents
                .Include(pe => pe.Participant)
                    .ThenInclude(p => p!.User)
                .Include(pe => pe.Category)
                .Where(pe => pe.EventID == eventId)
                .ToListAsync();

            return Ok(registrations);
        }

        // POST: api/enrolments
        // Creates a new registration.
        [HttpPost]
        [SessionAuthorize("Participant")]
        public async Task<IActionResult> CreateRegistration(
            [FromBody] EnrolmentRequest request)
        {
            int participantId =
                HttpContext.Session.GetInt32("UserId") ?? 0;

            var participant = await _context.Participants
                .FindAsync(participantId);

            if (participant == null)
            {
                return BadRequest(new
                {
                    message = "Participant profile does not exist."
                });
            }

            var raceEvent = await _context.Events
                .FindAsync(request.EventID);

            if (raceEvent == null)
            {
                return BadRequest(new
                {
                    message = "Event does not exist."
                });
            }

            if (DateTime.Now > raceEvent.RegistrationDeadline)
            {
                return BadRequest(new
                {
                    message = "The registration deadline has passed. Registration is closed."
                });
            }

            if (raceEvent.Date <= DateTime.Now)
            {
                return BadRequest(new
                {
                    message = "You cannot register for an event that has already started."
                });
            }

            var category = await _context.Categories
                .FindAsync(request.CategoryID);

            if (category == null ||
                category.EventID != request.EventID)
            {
                return BadRequest(new
                {
                    message = "Choose a valid category for this event."
                });
            }

            // A participant may only have one active registration
            // for the same event.
            bool alreadyRegistered =
                await _context.ParticipantEvents.AnyAsync(pe =>
                    pe.ParticipantID == participantId &&
                    pe.EventID == request.EventID &&
                    (pe.RegistrationStatus == "Pending" ||
                     pe.RegistrationStatus == "Confirmed"));

            if (alreadyRegistered)
            {
                return Conflict(new
                {
                    message = "You already have an active registration for this event. Change your category from your existing registration."
                });
            }

            string? genderError = GetGenderError(participant, category);

            if (genderError != null)
            {
                return BadRequest(new { message = genderError });
            }

            string? ageError = GetAgeError(participant, category);

            if (ageError != null)
            {
                return BadRequest(new { message = ageError });
            }

            int activeCategoryCount =
                await CountActiveInCategoryAsync(category.CategoryID);

            if (category.MaxParticipants > 0 &&
                activeCategoryCount >= category.MaxParticipants)
            {
                return Conflict(new
                {
                    message = "This category is full."
                });
            }

            // Assign a new bib number without reusing numbers
            // from previous or withdrawn registrations.
            string bibNumber =
                await GenerateBibNumberAsync(request.EventID);

            var registration = new ParticipantEvent
            {
                EventID = request.EventID,
                CategoryID = request.CategoryID,
                ParticipantID = participantId,
                Bib = bibNumber,
                RegistrationDate = DateTime.Now,
                RegistrationStatus = "Pending"
            };

            _context.ParticipantEvents.Add(registration);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetRegistration),
                new { id = registration.ParticipantEventID },
                new
                {
                    message = "Registration created successfully.",
                    participantEventId = registration.ParticipantEventID,
                    bib = registration.Bib
                });
        }

        // PUT: api/enrolments/{id}/category
        // Changes the category of an active registration.
        [HttpPut("{id}/category")]
        [SessionAuthorize("Participant")]
        public async Task<IActionResult> ChangeCategory(
            int id,
            [FromBody] ChangeCategoryRequest request)
        {
            int participantId =
                HttpContext.Session.GetInt32("UserId") ?? 0;

            var registration = await _context.ParticipantEvents
                .Include(pe => pe.Event)
                .FirstOrDefaultAsync(pe =>
                    pe.ParticipantEventID == id);

            if (registration == null)
            {
                return NotFound(new
                {
                    message = "Registration not found."
                });
            }

            if (registration.ParticipantID != participantId)
            {
                return Forbid();
            }

            if (!IsActiveRegistration(registration.RegistrationStatus))
            {
                return BadRequest(new
                {
                    message = "Only active registrations can be changed."
                });
            }

            if (registration.Event == null)
            {
                return NotFound(new
                {
                    message = "The event could not be found."
                });
            }

            if (DateTime.Now > registration.Event.RegistrationDeadline)
            {
                return BadRequest(new
                {
                    message = "The registration deadline has passed. You can no longer change your category."
                });
            }

            var newCategory = await _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.CategoryID == request.CategoryID &&
                    c.EventID == registration.EventID);

            if (newCategory == null)
            {
                return BadRequest(new
                {
                    message = "Choose a valid category for this event."
                });
            }

            var participant = await _context.Participants
                .FindAsync(participantId);

            if (participant == null)
            {
                return BadRequest(new
                {
                    message = "Participant profile does not exist."
                });
            }

            string? genderError = GetGenderError(participant, newCategory);

            if (genderError != null)
            {
                return BadRequest(new { message = genderError });
            }

            string? ageError = GetAgeError(participant, newCategory);

            if (ageError != null)
            {
                return BadRequest(new { message = ageError });
            }

            if (registration.CategoryID == request.CategoryID)
            {
                return Ok(new
                {
                    message = "You are already registered in this category."
                });
            }

            int activeCategoryCount =
                await CountActiveInCategoryAsync(newCategory.CategoryID);

            if (newCategory.MaxParticipants > 0 &&
                activeCategoryCount >= newCategory.MaxParticipants)
            {
                return Conflict(new
                {
                    message = "The selected category is full."
                });
            }

            registration.CategoryID = newCategory.CategoryID;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Your registration category has been changed successfully.",
                categoryID = registration.CategoryID
            });
        }

        // PUT: api/enrolments/{id}/cancel
        // Existing route retained for MVC compatibility.
        // The registration is now marked as Withdrawn.
        [HttpPut("{id}/cancel")]
        [SessionAuthorize("Participant")]
        public async Task<IActionResult> WithdrawRegistration(int id)
        {
            int participantId =
                HttpContext.Session.GetInt32("UserId") ?? 0;

            var registration = await _context.ParticipantEvents
                .Include(pe => pe.Event)
                .FirstOrDefaultAsync(pe =>
                    pe.ParticipantEventID == id);

            if (registration == null)
            {
                return NotFound(new
                {
                    message = "Registration not found."
                });
            }

            if (registration.ParticipantID != participantId)
            {
                return Forbid();
            }

            if (IsWithdrawn(registration.RegistrationStatus))
            {
                return BadRequest(new
                {
                    message = "This registration has already been withdrawn."
                });
            }

            if (!IsActiveRegistration(registration.RegistrationStatus))
            {
                return BadRequest(new
                {
                    message = "Only Pending or Confirmed registrations can be withdrawn."
                });
            }

            if (registration.Event == null)
            {
                return NotFound(new
                {
                    message = "The event could not be found."
                });
            }

            if (DateTime.Now > registration.Event.RegistrationDeadline)
            {
                return BadRequest(new
                {
                    message = "The registration deadline has passed. You can no longer withdraw from this event."
                });
            }

            registration.RegistrationStatus = "Withdrawn";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "You have successfully withdrawn from this event. Your registration has been moved to your registration history."
            });
        }

        // PUT: api/enrolments/{id}/status
        // Organisers manage registration progress for their own events.
        [HttpPut("{id}/status")]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> UpdateStatus(
            int id,
            [FromBody] EnrolmentStatusRequest request)
        {
            string[] allowedStatuses =
            {
                "Pending",
                "Confirmed",
                "Completed",
                "No-show"
            };

            string? status = allowedStatuses.FirstOrDefault(s =>
                string.Equals(
                    s,
                    request.Status,
                    StringComparison.OrdinalIgnoreCase));

            if (status == null)
            {
                return BadRequest(new
                {
                    message = "Status must be Pending, Confirmed, Completed or No-show."
                });
            }

            var registration = await _context.ParticipantEvents
                .Include(pe => pe.Event)
                .FirstOrDefaultAsync(pe =>
                    pe.ParticipantEventID == id);

            if (registration == null)
            {
                return NotFound(new
                {
                    message = "Registration not found."
                });
            }

            if (registration.Event == null ||
                registration.Event.EventOrganiserID !=
                HttpContext.Session.GetInt32("UserId"))
            {
                return StatusCode(403, new
                {
                    message = "You can only manage registrations for your own events."
                });
            }

            // Withdrawn and legacy Cancelled registrations
            // cannot be restored or changed by an organiser.
            if (IsWithdrawn(registration.RegistrationStatus))
            {
                return BadRequest(new
                {
                    message = "A withdrawn registration cannot be reactivated or changed."
                });
            }

            // Completed and No-show registrations are final.
            if (string.Equals(
                    registration.RegistrationStatus,
                    "Completed",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    registration.RegistrationStatus,
                    "No-show",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "This registration has already been finalised."
                });
            }

            registration.RegistrationStatus = status;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Registration status updated successfully.",
                status
            });
        }

        // Counts only registrations that still hold a place.
        private async Task<int> CountActiveInCategoryAsync(int categoryId)
        {
            return await _context.ParticipantEvents.CountAsync(pe =>
                pe.CategoryID == categoryId &&
                (pe.RegistrationStatus == "Pending" ||
                 pe.RegistrationStatus == "Confirmed"));
        }

        // Generates a new bib number for the event.
        // Previously used bib numbers are never reused.
        private async Task<string> GenerateBibNumberAsync(int eventId)
        {
            var existingBibs = await _context.ParticipantEvents
                .Where(pe => pe.EventID == eventId)
                .Select(pe => pe.Bib)
                .ToListAsync();

            var usedNumbers = existingBibs
                .Select(bib => int.TryParse(bib, out int number)
                    ? (int?)number
                    : null)
                .Where(number => number.HasValue)
                .Select(number => number!.Value)
                .ToHashSet();

            int nextNumber = usedNumbers.Count == 0
                ? 1
                : usedNumbers.Max() + 1;

            while (usedNumbers.Contains(nextNumber))
            {
                nextNumber++;
            }

            return nextNumber.ToString("D4");
        }

        private static bool IsActiveRegistration(string? status)
        {
            return string.Equals(
                       status,
                       "Pending",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       status,
                       "Confirmed",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWithdrawn(string? status)
        {
            // Cancelled is recognised for records created by the old system.
            return string.Equals(
                       status,
                       "Withdrawn",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       status,
                       "Cancelled",
                       StringComparison.OrdinalIgnoreCase);
        }

        // Returns a short eligibility message when the participant's age does not fit the category.
        private static string? GetAgeError(
            Participant participant,
            Category category)
        {
            // Open categories with no age limits accept everyone.
            if (!category.MinAge.HasValue && !category.MaxAge.HasValue)
            {
                return null;
            }

            if (participant.DateOfBirth == default)
            {
                return "Not eligible: your age could not be confirmed for this category.";
            }

            int age = CalculateAge(participant.DateOfBirth);

            if (category.MinAge.HasValue && age < category.MinAge.Value)
            {
                return "Not eligible: your age does not match this category.";
            }

            if (category.MaxAge.HasValue && age > category.MaxAge.Value)
            {
                return "Not eligible: your age does not match this category.";
            }

            return null;
        }

        // Blocks registration when a category is limited to one gender.
        private static string? GetGenderError(
            Participant participant,
            Category category)
        {
            if (string.IsNullOrWhiteSpace(category.Gender))
            {
                return null;
            }

            if (string.Equals(
                participant.Gender,
                category.Gender,
                StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return $"Not eligible: this is a {category.Gender.ToLowerInvariant()}-only category.";
        }

        private static int CalculateAge(DateTime dateOfBirth)
        {
            DateTime today = DateTime.Today;
            int age = today.Year - dateOfBirth.Year;

            if (dateOfBirth.Date > today.AddYears(-age))
            {
                age--;
            }

            return age;
        }
    }

    public class EnrolmentRequest
    {
        [Required]
        public int EventID { get; set; }

        [Required]
        public int CategoryID { get; set; }
    }

    public class ChangeCategoryRequest
    {
        [Required]
        public int CategoryID { get; set; }
    }

    public class EnrolmentStatusRequest
    {
        [Required]
        public string Status { get; set; } = string.Empty;
    }
}