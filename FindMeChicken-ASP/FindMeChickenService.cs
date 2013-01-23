using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using ServiceStack.Common;
using ServiceStack.ServiceClient.Web;
using ServiceStack.ServiceHost;
using ServiceStack.ServiceInterface;
using ServiceStack.WebHost.Endpoints;
using ServiceStack.DataAnnotations;
using ServiceStack.Logging;
using System.Diagnostics;
using FindMeChicken_ASP.Sources;
using FindMeChicken_ASP.Sources.KFC;
using FindMeChicken_ASP.Sources.JustEat;
using FindMeChicken_ASP.Sources.HungryHouse;
using FindMeChicken_ASP.Lib;
using System.Threading.Tasks;

namespace FindMeChicken_ASP
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
    }

    public class ChickenMenuRequest
    {
        public string SourceName { get; set; }
        public string Id { get; set; }
    }

    public class ChickenMenuRequestResponse
    {
        public List<ChickenMenu> Result { get; set; }
    }

    public class GetChickenMenuService : Service
    {
        public ChickenMenuRequestResponse Get(ChickenMenuRequest req)
        {
            ILog logger = LogManager.GetLogger(GetType());
            logger.Debug("Processing ChickenMenuRequest");
            if (req.SourceName == null || req.Id == null || !FindMeChickenService.SOURCES.ContainsKey(req.SourceName))
            {
                logger.Error("ChickenMenuRequest failed validation, returning null. State:");
                logger.Error(string.Format(" -> req.SourceName: {0}", req.SourceName));
                logger.Error(string.Format(" -> req.Id:         {0}", req.Id));
                return null;
            }

            ISource source = FindMeChickenService.SOURCES[req.SourceName];
            var res = source.GetPlaceMenu(req.Id);
            return new ChickenMenuRequestResponse() { Result = res };
        }
    }


    public class FindMeChickenService : Service
    {
        public static Dictionary<string, ISource> SOURCES = new Dictionary<string, ISource>() { {"KFC", new KFCSource()},
                                                                                         {"HungryHouse", new HungryHouseSource()},
                                                                                         {"JustEat", new JustEatSource()}};

        public static ILocationProvider[] LocationProviders = new ILocationProvider[] { new YQL(), new BingMaps() };

        public ChickenSearchRequestResponse Get(ChickenSearchRequest req)
        {
            ILog logger = LogManager.GetLogger(GetType());
            logger.Debug("Processing ChickenSearchRequest");
            if (req.Lat == 0 && req.Long == 0)
            {
                logger.Error("Error validating ChickenSearchRequest, Lat & Long are 0");
                return new ChickenSearchRequestResponse() { Result = null };
            }

            string PostCode = req.PostCode;
            logger.Debug(string.Format("PostCode: {0}", PostCode));
            if (string.IsNullOrEmpty(req.PostCode))
            {
                // Try each of our GeoLocation services in turn until we get a result
                foreach (ILocationProvider provider in LocationProviders)
                {
                    PostCode = provider.GetPostcodeFromLatLong(req.Lat, req.Long);
                    logger.Debug(string.Format(" - Location provider {0} found {1} as the PostCode", provider.GetType().FullName, PostCode));
                    if (!string.IsNullOrEmpty(PostCode)) break;
                }
            }

            Stopwatch timer = new Stopwatch();
            logger.Debug("Starting threads...");
            timer.Start();

            var places = new List<ChickenPlace>();
            // To Do: Make this Linq and turn task_list into task_list[]. Faster?
            var task_list = new List<Task<List<ChickenPlace>>>();
            foreach (ISource source in SOURCES.Values)
            {
                if (string.IsNullOrEmpty(PostCode) && source.RequiresPostcode()) continue;
                task_list.Add(
                        Task<List<ChickenPlace>>.Factory.StartNew(() => source.GetAvailablePlaces(new Location()
                        {
                            Lat = req.Lat,
                            Long = req.Long,
                            PostCode = PostCode,
                            FirstPostCode = PostCode.Split(' ')[0]
                        }))
                    );
            }

            foreach (var task in task_list) places.AddRange(task.Result);
            logger.Debug("Threads joined");
            foreach (var p in places) p.Distance = Math.Round(GeoCodeCalc.CalcDistance(req.Lat, req.Long,
                                                                                       p.Location.Lat, p.Location.Long, GeoCodeCalcMeasurement.Miles), 2);

            var ordered = (from place in places orderby place.Distance select place).Take(10);

            timer.Stop();
            logger.Debug(string.Format("Took {0} to fetch places", timer.Elapsed.ToString()));

            return new ChickenSearchRequestResponse() { Result = ordered.ToList() };
        }
    }
}