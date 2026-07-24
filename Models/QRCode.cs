using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DBPQRPermanent.Models
{
    public class QRCode
    {
        [Key] public int Id { get; set; }
        [Required] public string EmpId { get; set; } = "";

        // Random token — this is what goes in the QR URL, not the emp ID
        // e.g. /contact/a7f3k9x2m4q8b1p5 — unguessable
        [Required] public string Token { get; set; } = "";

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        [ForeignKey("EmpId")] public Employee? Employee { get; set; }
    }
}
