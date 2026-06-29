using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHealthMonitoring.Models;

public partial class PaymentDetail
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int PaymentId { get; set; }

    [Required]
    public int ServiceId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PriceAtTime { get; set; }

    public virtual Payment Payment { get; set; } = null!;
    public virtual Service Service { get; set; } = null!;
}
