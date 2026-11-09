using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDayAPI.Models
{
    public class Category
    {
        [Key]
        public int CategoryID { get; set; }

        public int EventID { get; set; }

        [Required]
        [MaxLength(50)]
        public string CategoryName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(6,2)")]
        public decimal Distance { get; set; }

        // All levels, Beginner, Intermediate, Advanced or Experienced.
        [Required]
        [MaxLength(50)]
        public string CategoryLevel { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal EntryFee { get; set; }

        public int MaxParticipants { get; set; }

        // Optional age limits. Null means no limit.
        public int? MinAge { get; set; }

        public int? MaxAge { get; set; }

        // Optional gender limit: "Male" or "Female". Null or empty means open to everyone.
        [MaxLength(10)]
        public string? Gender { get; set; }

        // Navigation property
        [ForeignKey("EventID")]
        public Event? Event { get; set; }
    }
}