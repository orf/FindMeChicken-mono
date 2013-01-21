using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FindMeChicken_ASP;


namespace FindMeChicken_ASP.Sources
{
    public interface ISource
    {
        List<ChickenPlace> GetAvailablePlaces(Location loc);
        List<ChickenMenu> GetPlaceMenu(string place_id);
        bool RequiresPostcode();
        bool SupportsMenu();
    }
}
