using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;


namespace FindMeChicken_ASP.Sources.KFC
{

    public class KFCSource : ISource
    {
        const string SOURCE_NAME = "KFC";
        const string BASE_URL = "http://www.kfc.co.uk/our-restaurants/search?latitude={0}&longitude={1}&radius=10&storeTypes=";

        public bool SupportsMenu()
        {
            return false;
        }

        public bool RequiresPostcode()
        {
            return false;
        }

        public List<ChickenMenu> GetPlaceMenu(string id)
        {
            return null;
        }

        public List<ChickenPlace> GetAvailablePlaces(Location loc)
        {
            List<ChickenPlace> returner = new List<ChickenPlace>();

            var client = new WebClient();
            string data = client.DownloadString(string.Format(BASE_URL, loc.Lat, loc.Long));

            List<dynamic> converted_data = JsonConvert.DeserializeObject<List<dynamic>>(data);

            foreach (dynamic entry in converted_data)
            {
                // So ugly
                StringBuilder Address = new StringBuilder();

                string[] possible_addresses = new string[] { entry.address1.ToString(), entry.address2.ToString(),
                                                    entry.address3.ToString(), entry.postcode.ToString() };

                string Addresses = string.Join("\n", (from address in possible_addresses
                                                      where !string.IsNullOrWhiteSpace(address.ToString())
                                                      select address.ToString()).ToList());

                ChickenPlace place = new ChickenPlace()
                {
                    Id = entry.id.Value.ToString(),
                    Source = SOURCE_NAME,
                    Name = entry.storeName,
                    Address = Addresses,
                    TelephoneNumber = entry.telno,
                    OpenUntil=null,
                    MenuAvaiable = false,
                    Location = new Location() { Lat = entry.latitude, Long = entry.longitude },
                };
                returner.Add(place);
            }

            return returner;

        }
    }
}