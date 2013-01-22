using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using FindMeChicken_ASP.Lib.DB;
using ServiceStack.Common;
using ServiceStack.ServiceClient.Web;
using ServiceStack.ServiceHost;
using ServiceStack.ServiceInterface;
using ServiceStack.WebHost.Endpoints;
using Funq;

namespace FindMeChicken_ASP
{
    public class Program
    {
        static void Main(string[] args)
        {
            var appHost = new Global.ChickenCheckerBase();
            appHost.Init();
            DB.SetupDatabase();
            appHost.Start("http://*:8080");
            Console.WriteLine("Serving...");
            Console.ReadKey();
        }
    }

    public class Global// : System.Web.HttpApplication
    {
        public class ChickenCheckerBase : AppHostHttpListenerBase 
        {
            //Tell Service Stack the name of your application and where to find your web services
            public ChickenCheckerBase() : base("ChickenChecker Web Service", typeof(FindMeChickenService).Assembly) { }

            public override void Configure(Container container)
            {
                //register user-defined REST-ful urls
                Routes.Add<ChickenSearchRequest>("/searchChicken").Add<ChickenMenuRequest>("/getMenu");
            }
        }

        protected void Application_Start(object sender, EventArgs e)
        {
            new ChickenCheckerBase().Init();
            DB.SetupDatabase();
        }

        protected void Session_Start(object sender, EventArgs e)
        {

        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {

        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {

        }

        protected void Application_Error(object sender, EventArgs e)
        {

        }

        protected void Session_End(object sender, EventArgs e)
        {

        }

        protected void Application_End(object sender, EventArgs e)
        {

        }
    }
}