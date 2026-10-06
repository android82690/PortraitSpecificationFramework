using System.Collections.Generic;

namespace SpecificPortraits
{
    public class PortraitPoolFile
    {
        public int Version { get; set; }
        public Dictionary<string, List<string>> PoolsByPortrait { get; set; } = new Dictionary<string, List<string>>();
        public List<PortraitPoolTag> Tags { get; set; } = new List<PortraitPoolTag>();
    }
}
