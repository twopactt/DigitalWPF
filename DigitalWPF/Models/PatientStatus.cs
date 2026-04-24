using System;
using System.Collections.Generic;

namespace DigitalWPF.Models;

public partial class PatientStatus
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Patient> Patients { get; set; } = new List<Patient>();
}
