using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FindMeChicken_POCO
{
    public class ChickenSearchRequest : Location
    {
        //public float Lat { get; set; } //= 53.767154F;
        //public float Long { get; set; } //= -0.35284F;
    }

    public class Location
    {
        public float Lat { get; set; }
        public float Long { get; set; }
        public string PostCode { get; set; }
        public string FirstPostCode { get; set; }
    }

    public class ChickenMenu
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
    }

    public class ChickenPlace
    {

        public string Id { get; set; }
        public string Source { get; set; }

        public string Name { get; set; }
        public string Address { get; set; }
        public string TelephoneNumber { get; set; }
        public DateTime? OpenUntil { get; set; }
        public bool MenuAvaiable { get; set; }
        public Location Location { get; set; }
        public double Distance { get; set; }
        public bool HasChicken { get; set; }

        // Rating from 1 to 100
        public double Rating { get; set; }

    }

    public class ChickenSearchRequestResponse
    {
        public List<ChickenPlace> Result { get; set; }
        public string PostCode { get; set; }
        public TimeSpan TimeTaken { get; set; }
    }

    public class ChickenMenuRequest
    {
        public string SourceName { get; set; }
        public string Id { get; set; }
    }

    public class ChickenMenuRequestResponse
    {
        public List<ChickenMenu> Result { get; set; }
        public string MenuNotes { get; set; }
    }
}
