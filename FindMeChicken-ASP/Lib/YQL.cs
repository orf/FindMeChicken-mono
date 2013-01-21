using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace FindMeChicken_ASP.Lib
{
    public class YQL : ILocationProvider
    {
        public string GetPostcodeFromLatLong(float lat, float lon)
        {
            string query = string.Format("select postal from geo.placefinder where text=\"{0},{1}\" and gflags=\"R\" LIMIT 1", lat, lon);
            dynamic result = ExecuteAndReturnJson(string.Format("http://query.yahooapis.com/v1/public/yql?q={0}&format=json",
                                                                       HttpUtility.UrlEncode(query)));
            try
            {
                return result.query.results.Result.postal;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static dynamic ExecuteAndReturnJson(string url)
        {
            HttpWebRequest req = WebRequest.Create(url) as HttpWebRequest;
            using (HttpWebResponse response = req.GetResponse() as HttpWebResponse)
            {
                StreamReader reader = new StreamReader(response.GetResponseStream());
                return JsonConvert.DeserializeObject<dynamic>(reader.ReadToEnd());
            }
        }

        // Populates a list of ChickenPlaces Location attribute in-place.
        public static void GeoLocatePlaces(ref List<ChickenPlace> places)
        {
            StringBuilder yql_parameters = new StringBuilder();
            List<string> subqueries = new List<string>();
            
            //int index = 0;
            foreach (ChickenPlace place in places)
            {
                subqueries.Add(string.Format("SELECT latitude,longitude FROM geo.placefinder WHERE text='{0}'", place.Address));
                //yql_parameters.Append(string.Format("&address_{0}={1}", index, HttpUtility.UrlEncode(place.Address)));
                //index++;
            }

            string real_query = string.Format("SELECT * FROM yql.query.multi WHERE queries=\"{0}\"", string.Join(";", subqueries));

            dynamic parsed = ExecuteAndReturnJson(string.Format("http://query.yahooapis.com/v1/public/yql?q={0}&format=json",
                                                                       HttpUtility.UrlEncode(real_query)));
            dynamic result_pairs = parsed.query.results.results;
            int index = 0;
            foreach (dynamic loc_pair in result_pairs)
            {
                dynamic result_pair = loc_pair.Result;
                if (result_pair.GetType() == typeof(Newtonsoft.Json.Linq.JArray)) result_pair = result_pair[0];

                Location loc = new Location();
                loc.Lat = result_pair.latitude;
                loc.Long = result_pair.longitude;
                places[index].Location = loc;
                index++;
            }
        }
    }
}