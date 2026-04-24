using System;
using System.Collections.Generic;

namespace DigitalWPF.Models;

public partial class Trend
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<TwinState> TwinStates { get; set; } = new List<TwinState>();
}
