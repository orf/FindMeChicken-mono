using HtmlAgilityPack;
using FindMeChicken_ASP.Lib;
using FindMeChicken_POCO;
using ServiceStack.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.IO;

namespace FindMeChicken_ASP.Sources.HungryHouse
{
    public class HungryHouseSource : ISource
    {
        const string SOURCE_NAME = "HungryHouse";
        const string FETCH_URL = "http://search.hungryhouse.co.uk/restaurants/{0}/Burgers-_-Chicken/0-40?q=%22Fried+Chicken%22";
        const string MENU_URL = "http://hungryhouse.co.uk/ajax{0}/menu?q=\"fried%20chicken\"";
        const string CHROME_USER_AGENT = "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/537.11 (KHTML, like Gecko) Chrome/23.0.1271.64 Safari/537.11";

        ILog logger = LogManager.GetLogger(typeof(HungryHouseSource));

        Regex UriExtractor = new Regex(@"\((.*?)\)",RegexOptions.Compiled);

        public bool RequiresPostcode()
        {
            return true;
        }

        public bool SupportsMenu()
        {
            return true;
        }

        public ChickenMenuRequestResponse GetPlaceMenu(string id)
        {
            this.logger.Debug("Got ChickenMenu request");
            var returner = new List<ChickenMenu>();
            
            TimeoutWebClient client = new TimeoutWebClient();
            client.SetTimeout(2);
            client.Headers[HttpRequestHeader.UserAgent] = CHROME_USER_AGENT;
            HtmlDocument doc = new HtmlDocument();
            string menu_url = string.Format(MENU_URL, id);
            logger.Error(string.Format("Downloading page {0}", menu_url));

            string page_html;
            try
            {
                page_html = client.DownloadString(menu_url);
                logger.Debug("Downloaded page");
            }
            catch (Exception e)
            {
                logger.Error(string.Format("Could not fetch menu page: {0}", menu_url), e);
                return new ChickenMenuRequestResponse() { Result = null };
            }

            doc.LoadHtml(page_html);

            foreach (var item_node in doc.DocumentNode.SelectNodes(".//tr[contains(@class, 'menuItem')]"))
            {
                string item_name;
                decimal price;

                try
                {
                    item_name = item_node.SelectSingleNode(".//div[@class='menuItemName']/a").InnerText;
                }
                catch (Exception e)
                {
                    logger.Error("Could not get menuItemName from item_node", e);
                    continue;
                }

                try
                {
                    // http://stackoverflow.com/questions/3575331/how-do-extract-decimal-number-from-string-in-c-sharp
                    string to_extract = item_node.SelectSingleNode(".//div[@class='menuItemPrice']").InnerText;
                    var match = Regex.Split(to_extract, @"[^0-9\.]+").Where(c => c != "." && c.Trim() != "").ToArray();
                    if (match.Count() == 0)
                    {
                        logger.Error("Got 0 matches for price");
                        continue;
                    }
                    price = Convert.ToDecimal(match[0]);
                }
                catch (FormatException ex)
                {
                    logger.Error("Could not convert price to decimal", ex);
                    continue;
                }

                returner.Add(new ChickenMenu() { Name = item_name, Price = price });
            }

            return new ChickenMenuRequestResponse() { Result = returner };
        }


        public List<ChickenPlace> GetAvailablePlaces(Location loc)
        {
            logger.Debug(string.Format("GetAvailablePlaces called: Lat: {0} Long: {1} PostCode: {2}", loc.Lat, loc.Long, loc.PostCode));
            List<ChickenPlace> returner = new List<ChickenPlace>();

            var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = CHROME_USER_AGENT;
            string page_html;
            try
            {
                string furl = string.Format(FETCH_URL, loc.FirstPostCode);
                logger.Debug(string.Format("Fetching URL {0}", furl));
                page_html = client.DownloadString(furl);
            }
            catch (Exception ex)
            {
                logger.Error("Could not fetch URL", ex);
                return returner;
            }

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(page_html);

            var results_node = doc.DocumentNode.SelectSingleNode(".//div[@class='restsSearchResultsLayout']");

            if (results_node == null)
            {
                logger.Error("Could not parse restsSearchResultsLayout from HTML response");
                return returner;
            }

            foreach (var place_node in results_node.SelectNodes(".//div[@class='restsSearchItemRes']"))
            {
                ChickenPlace place = new ChickenPlace() { Source=SOURCE_NAME, MenuAvaiable=true };

                try
                {
                    // Check if the place is open first
                    var open_node = place_node.SelectSingleNode(".//a[contains(@class,'restsRestStatus')]");
                    if (open_node == null)
                    {
                        logger.Error("restsRestsStatus is null");
                        continue;
                    }

                    if (!open_node.Attributes["class"].Value.Split(' ').Contains("restsStatusOpen")) continue;

                    var page_link = place_node.SelectSingleNode(".//a[@class='restPageLink']");
                    if (page_link == null)
                    {
                        logger.Error("restPageLink is null");
                        continue;
                    }

                    place.Id = page_link.Attributes["href"].Value;
                    place.Name = page_link.Attributes["title"].Value;

                    // Get the rating. The rating is stored in a <div> as the style attribute, like "width:90%" means 90% rating
                    // ToDo: make this a regex?
                    var rating_node = place_node.SelectSingleNode(".//div[@class='restsRating']/div");
                    if (rating_node != null)
                    {
                        Rating place_rating = new Rating();
                        string rating_text = rating_node.InnerText;
                        // Text is like so: "Average rating: X of Y"
                        try
                        {
                            string x_of_y = rating_text.Split(':')[1].Trim();
                            int thescore = Convert.ToInt32(x_of_y.Split(' ')[0]);
                            int max = Convert.ToInt32(x_of_y.Split(' ')[2]);
                            place_rating.Score = NumberScale.ScaleNumber(thescore, 0, 50, 100, 0);
                        }
                        catch (Exception ex)
                        {
                            logger.Error("Could not parse rating_node", ex);
                        }

                        // Get the total number of reviewers
                        var review_count_node = place_node.SelectSingleNode(".//a[@rel='restsReviewsTab']");
                        if (review_count_node != null)
                        {
                            place_rating.RatingsCount = Convert.ToInt32(review_count_node.InnerText.Split(' ')[0]);
                        }

                        if (place_rating.Score != 0) place.Rating = place_rating;
                    }

                    // The search page on HungryHouse is a bit shit so I'm not sure if it returns closed places or not.
                    // ToDo: Check this and remove closed places from returner.

                    // Get the location of the place. This is a huge hack - the lat/long is encoded in a <a> tag's style attribute
                    // which is used to display a google maps map.
                    // Firs get the restsMap node
                    var map_node = place_node.SelectSingleNode(".//div[@class='restsMap']");
                    if (map_node != null)
                    {
                        try
                        {
                            place.Address = map_node.SelectSingleNode(".//div").InnerText.Replace("\t", string.Empty).Trim();
                            if (place.Address.Contains("Distance")) place.Address = place.Address.Remove(place.Address.IndexOf("Distance"));
                        }
                        catch (Exception ex)
                        {
                            logger.Error("Could not parse the address", ex);
                            continue;
                        }

                        // Get the restsMapImage and extract the style
                        try
                        {
                            var node = map_node.SelectSingleNode(".//a[@class='restsMapImage']");
                            //logger.Debug("Found map node");

                            string map_style = node.Attributes["style"].Value;
                            //logger.Debug("Got map style attribute");

                            string uri = UriExtractor.Match(map_style).Groups[0].Value;
                            //logger.Debug(string.Format("Extracted URI: {0}", uri));

                            var parsed_qs = HttpUtility.ParseQueryString(uri);
                            //logger.Debug(string.Format("Parsed QS. Keys: {0}", string.Join(", ", parsed_qs.AllKeys)));
                            // On mono the key is "center", but on my development machine the key is amp;center. Weird.
                            string key = parsed_qs.AllKeys.Contains("center") ? "center" : "amp;center";
  
                            var location = parsed_qs[key].Split(',');
                            //logger.Debug(string.Format("Location: {0},{1}", location[0], location[1]));
                            place.Location = new Location() { Lat = float.Parse(location[0]), Long = float.Parse(location[1]) };
                        }
                        catch (Exception ex)
                        {
                            logger.Error("Could not extract location from URI", ex);
                            string fpath = Path.GetTempFileName();
                            File.WriteAllText(fpath, map_node.InnerHtml);
                            logger.Error(string.Format("Dumped map_node to file: {0}", fpath));
                            continue;
                        }
                    }
                    else
                    {
                        logger.Error(string.Format("Could not parse map node for place {0}", place.Name));
                        continue;
                    }
                    
                }
                catch (Exception ex)
                {
                    logger.Error("Could not iterate over place", ex);
                }
                returner.Add(place);
            }
            return returner;
        }

    }
}