using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using FindMeChicken_ASP;
using FindMeChicken_ASP.Lib;
using FindMeChicken_ASP.Sources;
using FindMeChicken_POCO;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Logging;

namespace FindMeChicken_ASP.Sources.JustEatAPI
{
    public class JustEatAPISource : ISource
    {
        ILog logger = LogManager.GetLogger(typeof(JustEatAPISource));
        HashSet<string> ALLOWED_CUISINES = new HashSet<string>() { "pizza", "kebabs", "american" };
        string SOURCE_NAME = "JustEatAPI";

        RequestContext GetRequestContext()
        {
            return new RequestContext()
            {
                CountryCode = "UK",
                LanguageCode = "en-GB",
                Password = "iPhoneAppProofOfConcept",
                Username = "iPhone-V1"
            };
        }

        int GetPlaceCurrentMenuId(BasicHttpBinding_IMenuApi client, int place_id, string first_postcode)
        {
            logger.Debug(string.Format("GetPlaceCurrentMenuId started by thread {0}", Thread.CurrentThread.ManagedThreadId));
            var ret = client.GetCurrentMenu(new Restaurant() { Id=place_id}, new MenuCriteria() { Postcode=first_postcode,
                                                                                               LocalTime=DateTime.Now.ToString("o"),
                                                                                               ForDelivery=true,
                                                                                               Formatting="SafeHtml" }, GetRequestContext()).Id;
            logger.Debug(string.Format("GetPlaceCurrentMenuId finished by thread {0}", Thread.CurrentThread.ManagedThreadId));
            return ret;
        }

        BasicHttpBinding_IMenuApi GetClient()
        {
            return new BasicHttpBinding_IMenuApi() { Url = "http://api.just-eat.com/MenuApi.svc" };
        }

        public List<ChickenPlace> GetAvailablePlaces(Location loc)
        {
            List<ChickenPlace> returner = new List<ChickenPlace>();
            List<RestaurantSearchResult> possible_places = new List<RestaurantSearchResult>();

            var client = GetClient();
            var search_criteria = new RestaurantSearchCriteria();
            search_criteria.Postcode = loc.FirstPostCode;
            var request_context = GetRequestContext();

            logger.Debug("Starting LINQ");
            /* Woah this is a big (parallel) LINQ statement. Don't be afraid.
             * We simply take all the restaurants open, check that they serve any cuisine in ALLOWED_CUISINES,
             * then check if they have a menu category that contains the word "chicken". If they do then we create a 
             * new ChickenPlace for the restaurant, ready to be GeoLocated and sent back to the client.
             */
            var places = (from place in client.GetRestaurantsV2(search_criteria, request_context).Restaurants.AsParallel()
                         where
                            place.IsOpenNow
                            // Does it serve one of our allowed cuisines?
                            && ALLOWED_CUISINES.Intersect(from cname
                                                         in place.CuisineTypes
                                                         select cname.Name.ToLower()).Count() != 0
                            // Does it have a Chicken category?
                            && (from menu_item
                                in client.GetCategoriesForMenu(new Menu1() { Id = GetPlaceCurrentMenuId(client,
                                                                                                        place.Id,
                                                                                                        loc.FirstPostCode) },
                                                                              request_context)
                                where menu_item.Name.ToLower().Contains("chicken")
                                select menu_item).Count() != 0

                         select new ChickenPlace()
                                    {
                                        Id = place.Id.ToString(),
                                        Source = SOURCE_NAME,
                                        Name = place.Name,
                                        Address = string.Format("{0}, {1}, {2}", place.Address, place.City, place.Postcode),
                                        MenuAvaiable = true,
                                        Location = null, // Will be filled in later.
                                        HasChicken = true,
                                        Rating = NumberScale.ScaleNumber(Convert.ToInt32(place.RatingForDisplay),0,6,0,100),
                                        TelephoneNumber = null
                                    }).ToArray();
            logger.Debug("LINQ over");
            returner.AddRange(places);
            YQL.GeoLocatePlaces(ref returner);
            logger.Debug("Geolocation complete, returning");
            return returner;
        }

        public ChickenMenuRequestResponse GetPlaceMenu(string place_id)
        {
            int place_id_int = Convert.ToInt32(place_id);
            var client = GetClient();
            var req_context = GetRequestContext();
            var menu_id = GetPlaceCurrentMenuId(client, place_id_int, null);
            var placeMenu1 = new Menu1() { Id = menu_id };
            logger.Debug("Starting GetCategoriesForMenu");
            ProductCategory[] menu = client.GetCategoriesForMenu(placeMenu1, req_context);
            logger.Debug("GetCatagoriesForMenu complete");
            foreach (var cat in client.GetCategoriesForMenu(placeMenu1, req_context))
            {
                if (cat.Name.ToLower().Contains("chicken"))
                {
                    logger.Debug("Starting GetProducts");
                    // We have a winner. Take the ID and fetch the actual menu
                    var realMenu = client.GetProducts(placeMenu1, new ProductCategory1() { Id = cat.Id }, req_context);
                    logger.Debug("GetProducts complete");
                    List<ChickenMenu> returner = new List<ChickenMenu>();

                    foreach (Product prod in realMenu)
                    {
                       returner.Add(new ChickenMenu(){
                                                Name = prod.Name,
                                                Price = prod.Price,
                                                Description = prod.Description
                       });
                    }
                    return new ChickenMenuRequestResponse() { Result = returner, MenuNotes=cat.Notes };
                }
            }
            return new ChickenMenuRequestResponse() { Result = null };
        }

        public bool RequiresPostcode()
        {
            return true;
        }

        public bool SupportsMenu()
        {
            return true;
        }

    }
}