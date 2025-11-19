using CMCS.Web.Models;

namespace CMCS.Web.Services
{
    // Simple rule engine for demo. Replace with more rules as needed.
    public class ClaimAutomationService
    {
        // Example thresholds - tune these or load from config
        private readonly decimal _autoApproveAmountLimit = 1000m;
        private readonly decimal _autoApproveHoursLimit = 20m;

        public bool ShouldAutoApprove(Claim claim)
        {
            // Calculate safety: if hours and amount are below thresholds => auto-approve
            if (claim.HoursWorked <= _autoApproveHoursLimit && claim.Amount <= _autoApproveAmountLimit)
                return true;

            return false;
        }

        // Additional automated checks (example)
        public bool ValidateClaim(Claim claim, out string? validationMessage)
        {
            if (claim.HoursWorked <= 0) { validationMessage = "Hours worked must be greater than zero."; return false; }
            if (claim.HourlyRate < 0) { validationMessage = "Hourly rate must be zero or positive."; return false; }

            validationMessage = null;
            return true;
        }
    }
}
