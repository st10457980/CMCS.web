using Microsoft.EntityFrameworkCore;
using CMCS.Web.Models;

namespace CMCS.Web.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> opts) : base(opts) { }

        public DbSet<Lecturer> Lecturers { get; set; } = null!;
        public DbSet<Approver> Approvers { get; set; } = null!;
        public DbSet<Claim> Claims { get; set; } = null!;
        public DbSet<SupportingDocument> SupportingDocuments { get; set; } = null!;
    }
}
