using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using RaceDayAPI.Filters;

namespace RaceDayAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ResultsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public ResultsController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves all results with their enrolment details. Organiser only.
        /// </summary>
        [HttpGet]
        [SessionAuthorize("Organiser")]
        public async Task<ActionResult<IEnumerable<Result>>> GetResults()
        {
            return await _context.Results
                .Include(r => r.ParticipantEvent)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves the logged-in Participant's race history.
        /// </summary>
        [HttpGet("mine")]
        [SessionAuthorize("Participant")]
        public async Task<IActionResult> GetMyResults()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");

            var results = await _context.Results
                .Where(r => r.ParticipantEvent!.ParticipantID == userId)
                .Select(r => new
                {
                    r.ResultID,
                    EventName = r.ParticipantEvent!.Event!.Name,
                    EventDate = r.ParticipantEvent!.Event!.Date,
                    CategoryName = r.ParticipantEvent!.Category!.CategoryName,
                    r.Time,
                    r.Position,
                    r.Status,
                    r.Reason,
                    TotalFinishers = _context.Results.Count(x =>
                        x.ParticipantEvent!.EventID == r.ParticipantEvent!.EventID &&
                        x.Position != null &&
                        x.Status == "Finished")
                })
                .OrderByDescending(x => x.EventDate)
                .ToListAsync();

            return Ok(results);
        }

        /// <summary>
        /// Retrieves all captured results for one of the Organiser's own events.
        /// </summary>
        [HttpGet("event/{eventId}")]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> GetResultsByEvent(int eventId)
        {
            var raceEvent = await _context.Events.FindAsync(eventId);

            if (raceEvent == null)
                return NotFound(new { message = "Event not found." });

            if (raceEvent.EventOrganiserID != HttpContext.Session.GetInt32("UserId"))
                return StatusCode(403, new
                {
                    message = "You can only view results for your own events."
                });

            var results = await _context.Results
                .Where(r => r.ParticipantEvent!.EventID == eventId)
                .ToListAsync();

            return Ok(results);
        }

        /// <summary>
        /// Retrieves one result by ID.
        /// </summary>
        [HttpGet("{id}")]
        [SessionAuthorize("Organiser")]
        public async Task<ActionResult<Result>> GetResult(int id)
        {
            var result = await _context.Results
                .Include(r => r.ParticipantEvent)
                .FirstOrDefaultAsync(r => r.ResultID == id);

            if (result == null)
            {
                return NotFound(new
                {
                    message = $"Result with ID {id} not found."
                });
            }

            return result;
        }

        /// <summary>
        /// Retrieves results recorded for one enrolment.
        /// </summary>
        [HttpGet("participant-event/{participantEventId}")]
        [SessionAuthorize("Organiser")]
        public async Task<ActionResult<IEnumerable<Result>>> GetResultsByParticipantEvent(
            int participantEventId)
        {
            var results = await _context.Results
                .Include(r => r.ParticipantEvent)
                .Where(r => r.ParticipantEventID == participantEventId)
                .ToListAsync();

            if (results.Count == 0)
            {
                return NotFound(new
                {
                    message = $"No results found for ParticipantEvent ID {participantEventId}."
                });
            }

            return Ok(results);
        }

        /// <summary>
        /// Captures a finish time, position and status for an enrolment.
        /// </summary>
        [HttpPost]
        [SessionAuthorize("Organiser")]
        public async Task<ActionResult<Result>> PostResult(Result result)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var enrolment = await _context.ParticipantEvents
                .Include(pe => pe.Event)
                .Include(pe => pe.Category)
                .Include(pe => pe.Participant)
                .FirstOrDefaultAsync(pe =>
                    pe.ParticipantEventID == result.ParticipantEventID);

            if (enrolment == null)
            {
                return BadRequest(new
                {
                    message = "Enrolment does not exist."
                });
            }

            if (enrolment.Event!.EventOrganiserID !=
                HttpContext.Session.GetInt32("UserId"))
            {
                return StatusCode(403, new
                {
                    message = "You can only capture results for your own events."
                });
            }

            if (await _context.Results.AnyAsync(r =>
                r.ParticipantEventID == result.ParticipantEventID))
            {
                return Conflict(new
                {
                    message = "A result already exists for this enrolment."
                });
            }

            ApplyGenderRule(enrolment, result);

            result.RecordedDate = DateTime.Now;

            _context.Results.Add(result);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetResult),
                new { id = result.ResultID },
                result);
        }

        /// <summary>
        /// Updates an existing result.
        /// </summary>
        [HttpPut("{id}")]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> PutResult(int id, Result result)
        {
            if (id != result.ResultID)
            {
                return BadRequest(new
                {
                    message = "ID mismatch between route and payload."
                });
            }

            var enrolment = await _context.ParticipantEvents
                .Include(pe => pe.Event)
                .Include(pe => pe.Category)
                .Include(pe => pe.Participant)
                .FirstOrDefaultAsync(pe =>
                    pe.ParticipantEventID == result.ParticipantEventID);

            if (enrolment == null)
            {
                return BadRequest(new
                {
                    message = "Enrolment does not exist."
                });
            }

            if (enrolment.Event!.EventOrganiserID !=
                HttpContext.Session.GetInt32("UserId"))
            {
                return StatusCode(403, new
                {
                    message = "You can only edit results for your own events."
                });
            }

            ApplyGenderRule(enrolment, result);

            result.RecordedDate = DateTime.Now;

            _context.Entry(result).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ResultExists(id))
                {
                    return NotFound(new
                    {
                        message = $"Result with ID {id} not found."
                    });
                }

                throw;
            }

            return NoContent();
        }

        /// <summary>
        /// Deletes a result.
        /// </summary>
        [HttpDelete("{id}")]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> DeleteResult(int id)
        {
            var result = await _context.Results.FindAsync(id);

            if (result == null)
            {
                return NotFound(new
                {
                    message = $"Result with ID {id} not found."
                });
            }

            _context.Results.Remove(result);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Gender is checked when the result is recorded, not when the participant enrols.
        // A participant whose gender does not match a gender-limited category is disqualified.
        private static void ApplyGenderRule(
            ParticipantEvent enrolment,
            Result result)
        {
            string? requiredGender = enrolment.Category?.Gender;

            if (string.IsNullOrWhiteSpace(requiredGender))
            {
                return;
            }

            if (string.Equals(
                enrolment.Participant?.Gender,
                requiredGender,
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // DSQ has no finish time or position.
            result.Status = "DSQ";
            result.Time = null;
            result.Position = null;

            // Keep the organiser's message. Only use the default when none was given.
            if (string.IsNullOrWhiteSpace(result.Reason))
            {
                result.Reason =
                    "This race is gender specific and you do not qualify.";
            }
        }

        private bool ResultExists(int id)
        {
            return _context.Results.Any(e => e.ResultID == id);
        }
    }
}