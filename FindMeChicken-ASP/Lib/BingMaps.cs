using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using Newtonsoft.Json;

namespace FindMeChicken_ASP.Lib
{
    public class BingMaps : ILocationProvider
    {
        // dont hack me bro
        const string KEY = "AlOMBBxYJrJ1EUPNTYcw6mXG0wAZwVgP8r7cWAFEvwra6p3HK8hoKQgtZGXbr1rW";

        public string GetPostcodeFromLatLong(float lat, float lon)
        {
            WebClient client = new WebClient();
            string ApiResponse = client.DownloadString(string.Format("http://dev.virtualearth.net/REST/v1/Locations/{0},{1}?key={2}", lat, lon, KEY));
            dynamic response = JsonConvert.DeserializeObject(ApiResponse);
            if (response.resourceSets.Count == 0)
            {
                return null;
            }

            try
            {
                dynamic resource_set = response.resourceSets[0].resources[0];
                return resource_set.address.postalCode;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}