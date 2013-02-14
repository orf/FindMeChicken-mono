using FindMeChicken_ASP.Lib;
using FindMeChicken_ASP.Lib.DB;
using FindMeChicken_POCO;
using ServiceStack.Logging;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FindMeChicken_ASP.Sources.JustEat
{
    public class JustEatSource : ISource
    {
        const string SOURCE_NAME = "JustEat";
        const string IOS_USER_AGENT = @"Mozilla/5.0 (iPhone; U; CPU iPhone OS 4_3_2 like Mac OS X; en-us) 
                                        AppleWebKit/533.17.9 (KHTML, like Gecko) Version/5.0.2 Mobile/8H7 
                                        Safari/6533.18.5";
        const string HOST = "http://www.just-eat.co.uk";
        const string BASE_URL = HOST + "/area/{0}";
        HashSet<string> ALLOWED_CUISINES = new HashSet<string>() { "pizza", "kebabs", "american" };

        ILog logger = LogManager.GetLogger(typeof(JustEatSource));

        public const bool SUPPORTS_MENU = true;

        public bool RequiresPostcode() { return true; }

        public bool SupportsMenu() { return true; }

        public ChickenMenuRequestResponse GetPlaceMenu(string id)
        {
            logger.Info(string.Format("Fetching place menu: {0}", id));
            var returner = new List<ChickenMenu>();
            var page = new HtmlDocument();

            var client = new TimeoutWebClient();
            client.SetTimeout(4);
            client.Headers[HttpRequestHeader.UserAgent] = IOS_USER_AGENT;
            try
            {
                logger.Info(string.Format("Downloading JustEat page {0}", HOST + id));
                page.Load(client.OpenRead(HOST + id));
                
                logger.Debug("Downloaded page");
            }
            catch (Exception ex)
            {
                logger.Error("Could not fetch JustEat page", ex);
                return new ChickenMenuRequestResponse() { Result = null };
            }

            // Loop through each category
            var product_nodes = page.DocumentNode.SelectNodes(".//li[contains(@class, 'cat')]");
            if (product_nodes == null)
            {
                logger.Error("Could not select product_nodes");
                return new ChickenMenuRequestResponse() { Result = null };
            }

            foreach (var node in product_nodes)
            {
                // Check if the title contains chicken
                if (node.SelectSingleNode(".//h2[@class='trigger']").InnerText.ToLower().Contains("chicken"))
                {
                    // Go through each item and build our ChickenMenu list
                    foreach (var product_node in node.SelectNodes(".//li[contains(@class,'multi') or contains(@class,'single')]"))
                    {
                        // This is pretty f***ing fragile. A small change could break it.
                        // Solution? Wrap it in a catch-all :)
                        try
                        {
                            // Some products are an umbrella with multiple sub-products. This is handled here:
                            if (product_node.Attributes["class"].Value.ToLower().Contains("multi"))
                            {
                                string name = product_node.SelectSingleNode(".//h2").InnerText;
                                foreach (var sub_node in product_node.SelectNodes(".//li[contains(@class, 'product-row')]"))
                                {
                                    ChickenMenu item = new ChickenMenu();
                                    
                                    item.Name = string.Format("{0}: {1}", name, sub_node.SelectSingleNode(".//span[contains(@class,'vname')").InnerText);
                                    item.Price = Convert.ToDecimal(product_node.SelectSingleNode(".//span[contains(@class,'vprice')]").InnerText.Split(' ')[1]);
                                    returner.Add(item);
                                }
                            }
                            else
                            {
                                string name = product_node.SelectSingleNode(".//h3").InnerText;
                                ChickenMenu item = new ChickenMenu();
                                item.Name = name;
                                item.Price = Convert.ToDecimal(product_node.SelectSingleNode(".//span[contains(@class,'vprice')]").InnerText.Split(' ')[1]);
                                if (product_node.SelectSingleNode(".//div[@class='description']") != null)
                                {
                                    item.Description = product_node.SelectSingleNode(".//div[@class='description']").InnerText;
                                }
                                returner.Add(item);
                            }
                        }
                        catch (Exception ex) {
                            logger.Error("Error parsing ChickenMenuItem", ex);
                        }
                    }
                    // Break out of the loop if we have found our chicken header
                    break;
                }
            }

            return new ChickenMenuRequestResponse() { Result = returner };
        }

        string RemoveUselessCharacters(string input)
        {
            return input.Trim(new char[] { '\r', '\n', '\t', ' ' });
        }

        public List<ChickenPlace> GetAvailablePlaces(Location loc)
        {
            List<ChickenPlace> possible_places = new List<ChickenPlace>();
            List<ChickenPlace> found_places = new List<ChickenPlace>();

            var client = new TimeoutWebClient();
            client.SetTimeout(4);
            client.Headers[HttpRequestHeader.UserAgent] = IOS_USER_AGENT;

            var doc = new HtmlDocument();
            string query_url = string.Format(BASE_URL, loc.FirstPostCode);
            logger.Info(string.Format("Downloading page {0}", query_url));
            try
            {
                doc.Load(client.OpenRead(query_url));
            }
            catch (Exception ex)
            {
                logger.Error("Failed to download page", ex);
                return found_places;
            }
            
            var OpenSections = doc.GetElementbyId("OpenRestaurants");
            var OpenPlaceNodes = OpenSections.SelectNodes(".//li");
            if (OpenPlaceNodes == null)
            {
                logger.Info("Found no OpenPlaceNodes, returning");
                return found_places;
            }

            // Go through each takeaway and discard some based on their cuisine types.
            foreach (var TakeAway in OpenPlaceNodes)
            {
                var place = new ChickenPlace();
                place.Source = SOURCE_NAME;
                place.MenuAvaiable = this.SupportsMenu();
                // Get the <a> tag containing the name and the link to the place
                var name_link = TakeAway.SelectSingleNode("a[contains(@class,'name')]");
                place.Id = name_link.Attributes["href"].Value;

                // Extract the title of the place. Some titles have <span> elements with "sponsored" in them, if this is the case then skip it.
                var takeaway_name_base = name_link.SelectSingleNode("h2");
                var name_node = takeaway_name_base;
                if (takeaway_name_base.SelectSingleNode("span") != null) name_node = takeaway_name_base.SelectSingleNode("span");

                place.Name = RemoveUselessCharacters(takeaway_name_base.InnerText).Replace("sponsored", string.Empty).Replace("Sponsored", string.Empty);

                // Get the <div> that contains the rating and cuisine types
                var place_details = name_link.SelectSingleNode("./div[@class='restaurantDetails']");
                // Get a list of cuisine types
                string cuisine_string = place_details.SelectSingleNode("./p[@class='cuisineTypeList']").InnerText.Trim();
                string[] cuisine_list = (from cuisine in cuisine_string.Split(',') select RemoveUselessCharacters(cuisine).ToLower()).ToArray();
                // Take the list of cuisines and intersect it with the allowed cuisine list
                var cuisine_intersection = ALLOWED_CUISINES.Intersect(cuisine_list);
                if (cuisine_intersection.Count() == 0) continue;


                // Get the rating. Its a bit of a hack, but w/e
                var rating_node = place_details.SelectSingleNode("./p[contains(@class,'rating')]");
                // The rating itself is in the class.
                place.Rating = NumberScale.ScaleNumber(Convert.ToInt32(rating_node.Attributes["class"].Value.Split('-')[1]), 0, 60, 100, 0);

                if (!DB.DoesChickenPlaceNotHaveChicken(place))
                {
                    var saved_results = DB.GetChickenPlaceById(SOURCE_NAME, new string[] { place.Id });
                    if (saved_results.Count() == 0)
                    {
                        possible_places.Add(place);
                    }
                    else
                    {
                        found_places.Add(saved_results[0]);
                    }
                };
            };


            Parallel.ForEach(possible_places, place =>
            {
                // Ok. Now we have to fetch the actual menu page
                var menu_doc = new HtmlDocument();
                var menu_client = new TimeoutWebClient();
                //menu_client.SetTimeout(3);
                try
                {
                    menu_doc.Load(menu_client.OpenRead(HOST + place.Id));
                }
                catch (Exception ex)
                {
                    logger.Error(string.Format("Could not download JustEat page for place {0}", place.Id), ex);
                    return;
                }

                // Check if they actually serve fried chicken
                // XPath has now lower-case function (for some insane reason), hence the use of the rather ugly translate hack.
                var has_chicken = menu_doc.DocumentNode.SelectSingleNode(@".//h2[@class='H2MC' and
                                                                contains(translate(text(),
                                                                                   'ABCDEFGHIJKLMNOPQRSTUVWXYZ',
                                                                                   'abcdefghijklmnopqrstuvwxyz'),
                                                                         'chicken')]");
                if (has_chicken == null)
                {
                    // No chicken here. Create a tombstone
                    place.HasChicken = false;
                    DB.AddChickenPlace(place);
                    return;
                };

                
                // Get the address (used for geolocating)
                var address_node = menu_doc.DocumentNode.SelectSingleNode(".//span[@itemtype='http://schema.org/PostalAddress']");

                if (address_node == null) {
                    logger.Error(string.Format("Could not find address for {0}", place.Id));
                    return;
                }

                var address_street = address_node.SelectSingleNode(".//span[@itemprop='streetAddress']").InnerText;
                var address_place = address_node.SelectSingleNode(".//span[@itemprop='addressLocality']").InnerText;
                var address_postcode = address_node.SelectSingleNode(".//span[@itemprop='postalCode']").InnerText;
                place.Address = string.Format("{0}, {1}, {2}", address_street, address_place, address_postcode).Trim();

                lock (found_places) found_places.Add(place);
                place.HasChicken = true;
                DB.AddChickenPlace(place);

            });

            // Lets GeoCode the *shit* out of our places using YQL
            YQL.GeoLocatePlaces(ref found_places);

            return found_places;
        }
    }
}