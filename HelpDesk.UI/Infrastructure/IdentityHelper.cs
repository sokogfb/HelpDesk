﻿using HelpDesk.DAL.Concrete;
using HelpDesk.DAL.Entities;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HelpDesk.UI.Infrastructure
{
    public class IdentityHelper
    {
        public UserManager UserManager
        {
            get
            {
                return HttpContext.Current.GetOwinContext().GetUserManager<UserManager>();
            }
        }

        public RoleManager RoleManager
        {
            get
            {
                return HttpContext.Current.GetOwinContext().GetUserManager<RoleManager>();
            }
        }

        private User currentUser = null;

        public User CurrentUser
        {
            get
            {
                if (HttpContext.Current.User.Identity.IsAuthenticated)
                {
                    if (currentUser == null)
                    {
                        currentUser = UserManager.FindByName(HttpContext.Current.User.Identity.Name);
                        currentUser.LastActivity = DateTime.Now;
                        UserManager.Update(currentUser);
                    }
                }
                else
                    currentUser = null;
                return currentUser;
            }
        }

        private bool? isCurrentUserAnAdministrator = null;

        public bool IsCurrentUserAnAdministrator()
        {
            if (HttpContext.Current.User.Identity.IsAuthenticated)
            {
                if (isCurrentUserAnAdministrator == null)
                    isCurrentUserAnAdministrator = UserManager.IsInRole(CurrentUser.Id, "Admin");
            }
            else
                isCurrentUserAnAdministrator = null;
            return isCurrentUserAnAdministrator ?? false;
        }
    }
}