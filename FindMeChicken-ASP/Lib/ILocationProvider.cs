using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FindMeChicken_ASP.Lib
{
    public interface ILocationProvider
    {
        string GetPostcodeFromLatLong(float lat, float lon);
    }
}
