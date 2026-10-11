using System.Collections.Generic;
using Newtonsoft.Json;

namespace SpecificPortraits
{
    /// <summary>
    /// One portrait pool: a filter (type, gender, race, card id, job, age range)
    /// plus the portrait ids that may be picked when a character matches it.
    /// </summary>
    public class PortraitPoolTag
    {
        public string ModID { get; set; } = "_Elona";
        public string Type { get; set; } = "c";
        public string Gender { get; set; } = "all";
        public string Race { get; set; } = "all";
        public string CardID { get; set; } = "all";
        public string Job { get; set; } = "all";
        public int AgeMin { get; set; }
        public int AgeMax { get; set; }
        public List<string> Portraits { get; set; } = new List<string>();

        /// <summary>Identity of the pool. Two pools with the same key merge.</summary>
        [JsonIgnore]
        public string Key => $"{ModID}|{Type}|{Gender}|{Race}|{CardID}|{Job}|{AgeMin}|{AgeMax}";

        /// <summary>Label shown in the pool list of the editor (mod-independent).</summary>
        public override string ToString()
        {
            var ageMinString = AgeMin == 0 ? "-∞" : AgeMin.ToString();
            var ageMaxString = AgeMax == 0 ? "∞" : AgeMax.ToString();
            return $"{Type}_{Gender}_{Race}_{CardID}_{Job}_({ageMinString} - {ageMaxString})";
        }

        public override bool Equals(object obj)
        {
            return obj is PortraitPoolTag other && other.Key == Key;
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
    }
}
