using System;
using System.Collections.Generic;

namespace DigitalWPF.Models;

public partial class PrescriptionStatus
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
}
