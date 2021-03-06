﻿using FindMeChicken_ASP.Lib;
using FindMeChicken_ASP.Sources;
using FindMeChicken_ASP.Sources.HungryHouse;
using FindMeChicken_ASP.Sources.JustEat;
using FindMeChicken_ASP.Sources.KFC;
using FindMeChicken_POCO;
using ServiceStack.Logging;
using ServiceStack.ServiceInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FindMeChicken_ASP.Sources.JustEatAPI;

namespace FindMeChicken_ASP
{
    

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
            try
            {
                return source.GetPlaceMenu(req.Id);
            }
            catch (Exception ex)
            {
                logger.Error("GetPlaceMenu failed", ex);
               return new ChickenMenuRequestResponse() { Result = null };
            }
        }
    }


    public class FindMeChickenService : Service
    {
        public static Dictionary<string, ISource> SOURCES = new Dictionary<string, ISource>() { {"KFC", new KFCSource()},
                                                                                         {"HungryHouse", new HungryHouseSource()},
                                                                                         {"JustEatAPI", new JustEatAPISource()}};

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
            if (string.IsNullOrEmpty(PostCode))
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
                logger.Debug(string.Format("Starting thread for source {0}", source.GetType().FullName));
                task_list.Add(
                        Task<List<ChickenPlace>>.Factory.StartNew(delegate() {
                            try{
                                return source.GetAvailablePlaces(new Location()
                                {
                                    Lat = req.Lat,
                                    Long = req.Long,
                                    PostCode = PostCode,
                                    FirstPostCode = PostCode.Split(' ')[0]
                                });
                            } catch (Exception ex)
                            {
                                logger.Error(string.Format("Error processing places for {0}", source.GetType().FullName), ex);
                                return new List<ChickenPlace>();
                            };
                        })
                    );
            }

            foreach (var task in task_list) places.AddRange(task.Result);
            logger.Debug("Threads joined");
            foreach (var p in places) p.Distance = Math.Round(GeoCodeCalc.CalcDistance(req.Lat, req.Long,
                                                                                       p.Location.Lat, p.Location.Long, GeoCodeCalcMeasurement.Miles), 2);

            // http://stackoverflow.com/questions/489258/linq-distinct-on-a-particular-property
            var duplicates_excluded = places.GroupBy(p => p.Name).Select(g => g.First()).ToList();

            var ordered = (from place in duplicates_excluded
                           orderby place.Distance
                           select place).Take(10).ToList();

            timer.Stop();
            logger.Debug(string.Format("Took {0} to fetch places", timer.Elapsed.ToString()));

            return new ChickenSearchRequestResponse() { Result = ordered, PostCode=PostCode, TimeTaken=timer.Elapsed.Seconds.ToString() };
        }
    }
}