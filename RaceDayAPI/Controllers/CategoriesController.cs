using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Filters;
using RaceDayAPI.Models;

namespace RaceDayAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private static readonly string[] AllowedLevels =
            { "All", "Beginner", "Intermediate", "Advanced", "Experienced" };

        private readonly RaceDayDbContext _context;

        public CategoriesController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves all categories across all events, each with its parent event. Returns 200 with a list.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories
                .Include(c => c.Event)
                .ToListAsync();

            return Ok(categories);
        }

        /// <summary>
        /// Retrieves one category by ID with its parent event. Returns 200, or 404 if the category does not exist.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategory(int id)
        {
            var category = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (category == null)
            {
                return NotFound(new { message = "Category not found." });
            }

            return Ok(category);
        }

        /// <summary>
        /// Adds a category to one of the Organiser's own events. Level must be All, Beginner, Intermediate, Advanced or Experienced. Age limits are optional and are ignored for the All level. Returns 201, 400 for invalid input, or 403 if the event belongs to another Organiser.
        /// </summary>
        [HttpPost]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> CreateCategory([FromBody] Category categoryDto)
        {
            try
            {
                // Verify if the parent event exists
                var raceEvent = await _context.Events
                    .FindAsync(categoryDto.EventID);

                if (raceEvent == null)
                {
                    return BadRequest(new { message = "Invalid EventID. Event does not exist." });
                }

                if (raceEvent.EventOrganiserID != HttpContext.Session.GetInt32("UserId"))
                {
                    return StatusCode(403, new { message = "You can only add categories to your own events." });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var level = NormaliseLevel(categoryDto.CategoryLevel);
                if (level == null)
                {
                    return BadRequest(new { message = "Category level must be All, Beginner, Intermediate, Advanced or Experienced." });
                }

                categoryDto.CategoryLevel = level;

                // The All level has no age limit.
                if (level == "All")
                {
                    categoryDto.MinAge = null;
                    categoryDto.MaxAge = null;
                }

                if (categoryDto.MinAge.HasValue && (categoryDto.MinAge < 1 || categoryDto.MinAge > 120))
                {
                    return BadRequest(new { message = "Minimum age must be between 1 and 120." });
                }

                if (categoryDto.MaxAge.HasValue && (categoryDto.MaxAge < 1 || categoryDto.MaxAge > 120))
                {
                    return BadRequest(new { message = "Maximum age must be between 1 and 120." });
                }

                if (categoryDto.MinAge.HasValue && categoryDto.MaxAge.HasValue
                    && categoryDto.MinAge > categoryDto.MaxAge)
                {
                    return BadRequest(new { message = "Minimum age cannot be greater than maximum age." });
                }

                _context.Categories.Add(categoryDto);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCategory), new { id = categoryDto.CategoryID }, new { message = "Category created successfully", categoryId = categoryDto.CategoryID });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        /// <summary>
        /// Updates a category belonging to one of the Organiser's own events.
        /// </summary>
        [HttpPut("{id}")]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> UpdateCategory(
            int id,
            [FromBody] Category updatedCategory)
        {
            var category = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (category == null)
                return NotFound(new { message = "Category not found." });

            if (category.Event == null ||
                category.Event.EventOrganiserID != HttpContext.Session.GetInt32("UserId"))
            {
                return StatusCode(403, new
                {
                    message = "You can only edit categories on your own events."
                });
            }

            if (string.IsNullOrWhiteSpace(updatedCategory.CategoryName))
                return BadRequest(new { message = "Enter a category name." });

            if (updatedCategory.CategoryName.Length > 50)
                return BadRequest(new { message = "Category name cannot exceed 50 characters." });

            if (updatedCategory.Distance <= 0)
                return BadRequest(new { message = "Distance must be greater than zero." });

            if (updatedCategory.EntryFee < 0)
                return BadRequest(new { message = "Entry fee cannot be negative." });

            if (updatedCategory.MaxParticipants < 1)
                return BadRequest(new { message = "Maximum participants must be at least 1." });

            var level = NormaliseLevel(updatedCategory.CategoryLevel);

            if (level == null)
                return BadRequest(new
                {
                    message = "Category level must be All, Beginner, Intermediate, Advanced or Experienced."
                });

            category.CategoryName = updatedCategory.CategoryName.Trim();
            category.Distance = updatedCategory.Distance;
            category.CategoryLevel = level;
            category.EntryFee = updatedCategory.EntryFee;
            category.MaxParticipants = updatedCategory.MaxParticipants;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Category updated successfully." });
        }

        /// <summary>
        /// Deletes a category from one of the Organiser's own events. Returns 200, 403 for another Organiser's event, 404 if not found, or 409 if the category has enrolments.
        /// </summary>
        [HttpDelete("{id}")]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (category == null)
                return NotFound(new { message = "Category not found." });

            if (category.Event!.EventOrganiserID != HttpContext.Session.GetInt32("UserId"))
                return StatusCode(403, new { message = "You can only delete categories on your own events." });

            try
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { message = "This category has enrolments and cannot be deleted." });
            }

            return Ok(new { message = "Category deleted successfully." });
        }

        // Returns All, Beginner, Intermediate, Advanced or Experienced, or null if the level is not recognised.
        private static string? NormaliseLevel(string? level)
        {
            if (string.IsNullOrWhiteSpace(level))
                return null;

            var trimmed = level.Trim();

            if (trimmed.Equals("All Levels", StringComparison.OrdinalIgnoreCase))
                return "All";

            return AllowedLevels.FirstOrDefault(
                l => l.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        }
    }
}