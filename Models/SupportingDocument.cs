namespace CMCS.Web.Models
{
    public class SupportingDocument
    {
        public int SupportingDocumentId { get; set; }
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public int ClaimId { get; set; }
        public Claim? Claim { get; set; }
    }
}
