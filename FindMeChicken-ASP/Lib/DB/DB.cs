using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using System.IO;
using System.Reflection;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;
using ServiceStack.DataAnnotations;
using ServiceStack.Logging;

namespace FindMeChicken_ASP.Lib.DB
{
    public class DB
    {
        // Temp for now. Maybe move to something more durable later?
        static OrmLiteConnectionFactory factory;
        

        public static void SetupDatabase()
        {
            ILog logger = LogManager.GetLogger("DB.SetuPDatabase");
            logger.Debug("Setting up Database");
            string path = Path.GetTempFileName();
            logger.Debug(string.Format(" - Path for Database: {0}", path));
            factory = new OrmLiteConnectionFactory(path,false, SqliteDialect.Provider);
            try
            {
                using (IDbConnection db = factory.OpenDbConnection())
                {
                    logger.Debug(" - Database created");
                    db.CreateTableIfNotExists<ChickenPlace>();
                    logger.Debug("  - ChickenPlace table configured");
                }
            }
            catch (Exception ex)
            {
                logger.Error(string.Format("Error creating Database: {0}", ex.Message), ex);
                throw;
            }
        }

        public static IDbConnection GetConnection()
        {
            return factory.OpenDbConnection();
        }

        public static void AddChickenPlace(ChickenPlace place)
        {
            using (var conn = GetConnection())
            {
                conn.Insert<ChickenPlace>(place);
            }
        }

        /// <summary>
        /// Check if a tombstone exists in the database saying that this place does *not* have any chicken. Returns true if we are sure this place
        /// doesn't have any lovely chicken, otherwise return false which means we should check (and create a tombstone if it doesn't)
        /// </summary>
        /// <param name="place"></param>
        /// <returns></returns>
        public static bool DoesChickenPlaceNotHaveChicken(ChickenPlace place)
        {
            using (var conn = GetConnection())
            {
                DateTime ExpireTime = DateTime.UtcNow.AddMonths(-2);
                var result = conn.Select<ChickenPlace>(q => q.Id == place.Id && q.Source == place.Source && q.HasChicken == false);
                if (result.Count() > 0) return true;
                return false;
            }
        }

        public static List<ChickenPlace> GetChickenPlaceById(string Source, string[] Id)
        {
            using (var conn = GetConnection())
            {
                List<ChickenPlace> returner = new List<ChickenPlace>();
                var results = conn.Select<ChickenPlace>(q => Sql.In(q.Id, Id) && q.HasChicken == true);

                foreach (var result in results) returner.Add(result as ChickenPlace);

                return returner;
            }
        }
    }
}