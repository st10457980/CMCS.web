using System.ComponentModel.DataAnnotations;

namespace CMCS.Web.Models
{
    public enum ClaimStatus { Pending, Approved, Rejected }

    public class Claim
    {
        public int ClaimId { get; set; }

        [Required]
        public int LecturerId { get; set; }
        public Lecturer? Lecturer { get; set; }

        [Required]
        public DateTime ClaimDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Range(0.1, 1000)]
        public decimal HoursWorked { get; set; }

        [Required]
        [Range(0.0, 100000)]
        public decimal HourlyRate { get; set; }

        public string? Notes { get; set; }

        public ClaimStatus Status { get; set; } = ClaimStatus.Pending;

        public List<SupportingDocument> Documents { get; set; } = new();
    }
}
