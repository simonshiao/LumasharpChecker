using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LumasharpChecker.model
{
    public class SharpnessRecord
    {
        public int Id { get; set; }
        public string ProfileName { get; set; }
        public double Sharpness { get; set; }
        public DateTime Timestamp { get; set; }
        //public bool EnableDynamicThreshold { get; set; }
        //public int DynamicThresholdCount { get; set; }
    }
}
