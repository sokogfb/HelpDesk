﻿using HelpDesk.DAL.Concrete;
using Microsoft.AspNet.Identity;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Owin;

namespace HelpDesk.UI
{
    public class IdentityConfig
    {
        public void Configuration(IAppBuilder app)
        {
            app.CreatePerOwinContext<HelpDeskContext>(HelpDeskContext.Create);
            app.CreatePerOwinContext<UserManager>(UserManager.Create);
            app.CreatePerOwinContext<RoleManager>(RoleManager.Create);
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
                LoginPath = new PathString("/Account/Login"),
            });
        }
    }
}