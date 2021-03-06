﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using FindMeChicken_ASP.Lib.DB;
using FindMeChicken_POCO;
using ServiceStack.Common;
using ServiceStack.ServiceClient.Web;
using ServiceStack.ServiceHost;
using ServiceStack.ServiceInterface;
using ServiceStack.WebHost.Endpoints;
using ServiceStack.Logging;
using ServiceStack.Logging.Log4Net;
using Funq;

namespace FindMeChicken_ASP
{
    public class Global : System.Web.HttpApplication
    {
        public class ChickenCheckerBase : AppHostBase
        {
            //Tell Service Stack the name of your application and where to find your web services
            public ChickenCheckerBase() : base("ChickenChecker Web Service", typeof(FindMeChickenService).Assembly) { }

            public override void Configure(Container container)
            {
                ILog logger = LogManager.GetLogger(GetType());

                //register user-defined REST-ful urls
                Routes.Add<ChickenSearchRequest>("/searchChicken");
                Routes.Add<ChickenMenuRequest>("/getMenu");
                logger.Debug("Routes configured");
            }
        }

        protected void Application_Start(object sender, EventArgs e)
        {
            new ChickenCheckerBase().Init();
            LogManager.LogFactory = new Log4NetFactory(true);
            ILog logger = LogManager.GetLogger(GetType());
            logger.Debug("Logging configured");
            DB.SetupDatabase();
            logger.Debug("Application configured");
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