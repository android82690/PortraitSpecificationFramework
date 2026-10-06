using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SpecificPortraits
{
    public class PortraitPoolTag
    {
        public override string ToString()
        {
            var ageMinString = AgeMin == 0 ? "-∞" : AgeMin.ToString();
            var ageMaxString = AgeMax == 0 ? "∞" : AgeMax.ToString();

            return $"{Type}_{Gender}_{Race}_{CardID}_{Job}_({ageMinString} - {ageMaxString})";
        }
        public override bool Equals(object obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // If parameter cannot be cast to Point return false.
            var p = obj as PortraitPoolTag;
            if (p == null)
            {
                return false;
            }

            // Return true if the fields match:
            return (Type == p.Type)
                && (Gender == p.Gender)
                && Race == p.Race
                && CardID == p.CardID
                && Job == p.Job
                && AgeMin == p.AgeMin
                && AgeMax == p.AgeMax;
        }
        public override int GetHashCode()
        {
            return $"{ModID} {Type} {Gender} {Race} {CardID} {Job} {AgeMin} {AgeMax}".GetHashCode();
        }
        public string ModID { get; set; } = "_Elona";
        public string Type { get; set; } = "c";
        public string Gender { get; set; }
        public string Race { get; set; } = "all";
        public string CardID { get; set; } = "all";
        public string Job { get; set; } = "all";
        public int AgeMin { get; set; }
        public int AgeMax { get; set; }
        public List<string> Portraits { get; set; } = new List<string>();
    }
}
