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

namespace FindMeChicken_ASP.Lib.DB
{
    public class DB
    {
        // Temp for now. Maybe move to something more durable later?
        static OrmLiteConnectionFactory factory;
        

        public static void SetupDatabase()
        {
            string path = Path.GetTempFileName();
            factory = new OrmLiteConnectionFactory(path,false, SqliteDialect.Provider);
            using (IDbConnection db = factory.OpenDbConnection())
            {
                db.CreateTableIfNotExists<ChickenPlace>();
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