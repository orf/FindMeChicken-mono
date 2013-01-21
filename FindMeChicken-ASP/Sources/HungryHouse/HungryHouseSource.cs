using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using System.Text.RegularExpressions;
using FindMeChicken_ASP.Sources;
using HtmlAgilityPack;

namespace FindMeChicken_ASP.Sources.HungryHouse
{
    public class HungryHouseSource : ISource
    {
        const string SOURCE_NAME = "HungryHouse";
        const string FETCH_URL = "http://search.hungryhouse.co.uk/restaurants/{0}/Burgers-_-Chicken/0-40?q=%22Fried+Chicken%22";
        const string MENU_URL = "http://hungryhouse.co.uk/ajax{0}/menu?q=\"fried%20chicken\"";
        const string CHROME_USER_AGENT = "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/537.11 (KHTML, like Gecko) Chrome/23.0.1271.64 Safari/537.11";

        Regex UriExtractor = new Regex(@"\((.*?)\)",RegexOptions.Compiled);

        public bool RequiresPostcode()
        {
            return true;
        }

        public bool SupportsMenu()
        {
            return true;
        }

        public List<ChickenMenu> GetPlaceMenu(string id)
        {
            var returner = new List<ChickenMenu>();

            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = CHROME_USER_AGENT;
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(client.DownloadString(string.Format(MENU_URL, id)));
            foreach (var item_node in doc.DocumentNode.SelectNodes(".//tr[contains(@class, 'menuItem')]"))
            {
                string item_name = item_node.SelectSingleNode(".//div[@class='menuItemName']/a").InnerText;
                decimal price = Convert.ToDecimal(item_node.SelectSingleNode(".//div[@class='menuItemPrice']").InnerText.Replace("£", string.Empty));
                returner.Add(new ChickenMenu() { Name = item_name, Price = price });
            }

            return returner;
        }

        public List<ChickenPlace> GetAvailablePlaces(Location loc)
        {
            List<ChickenPlace> returner = new List<ChickenPlace>();

            var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = CHROME_USER_AGENT;
            string page_html = client.DownloadString(string.Format(FETCH_URL, loc.FirstPostCode));
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(page_html);

            var results_node = doc.DocumentNode.SelectSingleNode(".//div[@class='restsSearchResultsLayout']");
            foreach (var place_node in results_node.SelectNodes(".//div[@class='restsSearchItemRes']"))
            {
                ChickenPlace place = new ChickenPlace() { Source=SOURCE_NAME, MenuAvaiable=true };

                // Check if the place is open first
                var open_node = place_node.SelectSingleNode(".//a[contains(@class,'restsRestStatus')]");
                if (!open_node.Attributes["class"].Value.Split(' ').Contains("restsStatusOpen")) continue;

                var page_link = place_node.SelectSingleNode(".//a[@class='restPageLink']");
                place.Id = page_link.Attributes["href"].Value;
                place.Name = page_link.Attributes["title"].Value;

                // Get the rating. The rating is stored in a <div> as the style attribute, like "width:90%" means 90% rating
                // ToDo: make this a regex?
                var rating_node = place_node.SelectSingleNode(".//div[@class='restsRating']/div");
                if (rating_node != null)
                {
                    string rating_style = rating_node.Attributes["style"].Value;
                    place.Rating = Convert.ToDouble(rating_style.Split(':')[1].Replace("%", string.Empty));
                }

                // The search page on HungryHouse is a bit shit so I'm not sure if it returns closed places or not.
                // ToDo: Check this and remove closed places from returner.

                // Get the location of the place. This is a huge hack - the lat/long is encoded in a <a> tag's style attribute
                // which is used to display a google maps map.
                // Firs get the restsMap node
                var map_node = place_node.SelectSingleNode(".//div[@class='restsMap']");
                // ToDo: Make this actually work
                place.Address = map_node.SelectSingleNode(".//div").InnerText.Replace("\t", string.Empty).Trim();
                if (place.Address.Contains("Distance")) place.Address = place.Address.Remove(place.Address.IndexOf("Distance"));

                // Get the restsMapImage and extract the style
                string map_style = map_node.SelectSingleNode(".//a[@class='restsMapImage']").Attributes["style"].Value;
                string uri = UriExtractor.Match(map_style).Groups[0].Value;
                var parsed_qs = HttpUtility.ParseQueryString(uri);
                var location = parsed_qs["amp;center"].Split(',');
                place.Location = new Location() { Lat = float.Parse(location[0]), Long=float.Parse(location[1]) };
                returner.Add(place);
            }
            return returner;
        }

    }
}