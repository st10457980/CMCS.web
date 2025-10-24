namespace CMCS.Web.Models
{
    public class Approver
    {
        public int ApproverId { get; set; }
        public string FullName { get; set; } = "";
        public string Role { get; set; } = ""; // Coordinator / Manager
        public string Email { get; set; } = "";
    }
}
