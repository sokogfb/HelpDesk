﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Linq.Expressions;
using PagedList;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.AspNet.Identity;
using System.Text;
using HelpDesk.DAL.Concrete;
using HelpDesk.DAL.Abstract;
using HelpDesk.DAL.Entities;
using HelpDesk.UI.ViewModels.Tickets;

namespace HelpDesk.UI.Controllers.MVC
{
    [Authorize(Roles = "Admin")]
    public class TicketsController : Controller
    {
        private UserManager UserManager
        {
            get
            {
                return HttpContext.GetOwinContext().GetUserManager<UserManager>();
            }
        }

        private RoleManager RoleManager
        {
            get
            {
                return HttpContext.GetOwinContext().GetUserManager<RoleManager>();
            }
        }

        private User CurrentUser
        {
            get
            {
                return UserManager.FindByNameAsync(User.Identity.Name).Result;
            }
        }

        private IUnitOfWork unitOfWork;

        public TicketsController(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        [OverrideAuthorization]
        public ViewResult Index()
        {
            return View("Index");
        }

        [OverrideAuthorization]
        public ViewResult Create()
        {
            CreateViewModel model = new CreateViewModel
            {
                Requester = CurrentUser,
                RequestedByID = CurrentUser.Id,
                Categories = unitOfWork.CategoryRepository.Get(orderBy: x => x.OrderBy(c => c.Order))
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [OverrideAuthorization]
        public async Task<ActionResult> Create([Bind(Include = "RequestedByID,CategoryID,Title,Content")] CreateViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    Ticket ticket = new Ticket
                    {
                        CreatorId = CurrentUser.Id,
                        RequesterId = model.RequestedByID,
                        CreateDate = DateTime.Now,
                        Status = "New",
                        CategoryId = model.CategoryID,
                        Title = model.Title,
                        Content = model.Content
                    };
                    unitOfWork.TicketRepository.Insert(ticket);
                    unitOfWork.Save();

                    TempData["Success"] = "Successfully added new ticket!";
                    return RedirectToAction("Index");
                }
            }
            catch
            {
                ModelState.AddModelError("", "Cannot create ticket. Try again!");
            }
            model.Requester = await UserManager.FindByIdAsync(model.RequestedByID);
            model.Categories = unitOfWork.CategoryRepository.Get(orderBy: x => x.OrderBy(c => c.Order));
            return View(model);
        }

        [OverrideAuthorization]
        public async Task<ActionResult> Edit(int? id)
        {
            Ticket ticket = unitOfWork.TicketRepository.GetById(id ?? 0);
            if (ticket == null)
            {
                return HttpNotFound();
            }
            if (!await UserManager.IsInRoleAsync(CurrentUser.Id, "Admin") && ticket.CreatorId != CurrentUser.Id)
            {
                TempData["Fail"] = "You can't modify ticket which you didn't create!";
                return RedirectToAction("Index", "Home");
            }
            EditViewModel model = new EditViewModel
            {
                TicketId = ticket.TicketId,
                RequesterId = ticket.RequesterId,
                AssignedUserId = ticket.AssignedUserId,
                Status = ticket.Status,
                CategoryId = ticket.CategoryId,
                Title = ticket.Title,
                Content = ticket.Content,
                Solution = ticket.Solution,
                Creator = ticket.Creator,
                CreateDate = ticket.CreateDate.ToString("yyyy-MM-dd hh:mm:ss"),
                Requester = ticket.Requester,                
                AssignedUser = ticket.AssignedUser
            };
            string adminRoleId = RoleManager.FindByName("Admin").Id;
            model.Administrators = UserManager.Users.Where(u => u.Roles.FirstOrDefault(x => x.RoleId == adminRoleId) != null).OrderBy(u => u.FirstName);
            model.Categories = unitOfWork.CategoryRepository.Get(orderBy: x => x.OrderBy(c => c.Order));
            ViewBag.IsCurrentUserAdmin = await UserManager.IsInRoleAsync(CurrentUser.Id, "Admin");
            return View(model);
        }

        private void updateTicketHistory(Ticket currentTicket, Ticket updatedTicket)
        {
            List<TicketsHistory> ticketsHistoryList = new List<TicketsHistory>();

            if (currentTicket.RequesterId != updatedTicket.RequesterId)
            {
                string requesterName = updatedTicket.Requester != null ? $"{updatedTicket.Requester.FirstName} {updatedTicket.Requester.LastName}" : "";
                ticketsHistoryList.Add(new TicketsHistory { Column = "requester", NewValue = requesterName });
            }

            if (currentTicket.AssignedUserId != updatedTicket.AssignedUserId)
            {
                string assignedUserName = updatedTicket.AssignedUser != null ? $"{updatedTicket.AssignedUser.FirstName} {updatedTicket.AssignedUser.LastName}" : "";
                ticketsHistoryList.Add(new TicketsHistory { Column = "assigned user", NewValue = assignedUserName });
            }

            if (currentTicket.Status != updatedTicket.Status)
                ticketsHistoryList.Add(new TicketsHistory { Column = "status", NewValue = updatedTicket.Status });

            if (currentTicket.CategoryId != updatedTicket.CategoryId)
            {
                string categoryName = updatedTicket.Category != null ? updatedTicket.Category.Name : "";
                ticketsHistoryList.Add(new TicketsHistory { Column = "category", NewValue = categoryName });
            }

            if (currentTicket.Title != updatedTicket.Title)
                ticketsHistoryList.Add(new TicketsHistory { Column = "title", NewValue = updatedTicket.Title });

            if (currentTicket.Content != updatedTicket.Content)
                ticketsHistoryList.Add(new TicketsHistory { Column = "content", NewValue = updatedTicket.Content });
            
            if (currentTicket.Solution != updatedTicket.Solution)
                ticketsHistoryList.Add(new TicketsHistory { Column = "solution", NewValue = updatedTicket.Solution });

            foreach (var log in ticketsHistoryList)
            {
                log.Date = DateTime.Now;
                log.AuthorId = CurrentUser.Id;
                log.TicketId = currentTicket.TicketId;
                unitOfWork.TicketsHistoryRepository.Insert(log);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [OverrideAuthorization]
        public async Task<ActionResult> Edit([Bind(Include = "TicketID,RequestedByID,AssignedToID,Status,CategoryID,Title,Content,Solution")] EditViewModel model)
        {
            Ticket ticket = unitOfWork.TicketRepository.GetById(model.TicketId);
            if (ticket == null)
            {
                return HttpNotFound();
            }

            model.Requester = await UserManager.FindByIdAsync(model.RequesterId);
            if (await UserManager.IsInRoleAsync(CurrentUser.Id, "Admin"))
            {
                model.AssignedUser = await UserManager.FindByIdAsync(model.AssignedUserId);
            }
            else
            {
                if (ticket.CreatorId != CurrentUser.Id)
                {
                    TempData["Fail"] = "You can't modify ticket which you didn't create!";
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ModelState.Remove("AssignedToID");
                    model.AssignedUserId = ticket.AssignedUserId;
                    model.AssignedUser = ticket.AssignedUser;

                    ModelState.Remove("Status");
                    model.Status = ticket.Status;

                    ModelState.Remove("Solution");
                    model.Solution = ticket.Solution;
                }
            }

            //try
            {
                if (ModelState.IsValid)
                {
                    Ticket oldTicket = (Ticket)ticket.Clone();

                    ticket.RequesterId = model.RequesterId;
                    ticket.CategoryId = model.CategoryId;
                    ticket.Title = model.Title;
                    ticket.Content = model.Content;
                    ticket.Status = model.Status;
                    ticket.AssignedUserId = model.AssignedUserId;
                    ticket.Solution = model.Solution;
                    unitOfWork.TicketRepository.Update(ticket);

                    updateTicketHistory(oldTicket, ticket);
                    unitOfWork.Save();

                    TempData["Success"] = "Successfully edited ticket!";
                    return RedirectToAction("Index");
                }
            }
            //catch
            //{
            //    ModelState.AddModelError("", "Cannot edit ticket. Try again!");
            //}
            model.Creator = ticket.Creator;
            model.CreateDate = ticket.CreateDate.ToString("yyyy-MM-dd hh:mm:ss");

            string adminRoleId = RoleManager.FindByName("Admin").Id;
            model.Administrators = UserManager.Users.Where(u => u.Roles.FirstOrDefault(x => x.RoleId == adminRoleId) != null).OrderBy(u => u.FirstName);
            model.Categories = unitOfWork.CategoryRepository.Get(orderBy: x => x.OrderBy(c => c.Order));
            ViewBag.IsCurrentUserAdmin = await UserManager.IsInRoleAsync(CurrentUser.Id, "Admin");
            return View(model);
        }

        [OverrideAuthorization]
        public async Task<ActionResult> History(int id)
        {

            try
            {
                Ticket ticket = unitOfWork.TicketRepository.GetById(id);
                if (ticket == null)
                    throw new Exception($"Ticket id {id} doesn't exist");

                if (!await UserManager.IsInRoleAsync(CurrentUser.Id, "Admin") && ticket.CreatorId != CurrentUser.Id && ticket.RequesterId != CurrentUser.Id)
                    return new HttpUnauthorizedResult();

                HistoryViewModel model = new HistoryViewModel
                {
                    TicketId = id,
                    Logs = new List<HistoryViewModel.Log>()
                };
                foreach (var log in unitOfWork.TicketsHistoryRepository.Get(filters: new Expression<Func<TicketsHistory, bool>>[] { l => l.TicketId == ticket.TicketId }, orderBy: x => x.OrderByDescending(l => l.Date)))
                {
                    User author = UserManager.FindById(log.AuthorId);
                    model.Logs.Add(new HistoryViewModel.Log
                    {
                        Date = log.Date,
                        AuthorId = log.AuthorId,
                        AuthorName = author != null ? $"{author.FirstName} {author.LastName}" : null,
                        Column = log.Column,
                        NewValue = log.NewValue
                    });
                }
                return View(model);
            }
            catch
            {
                TempData["Fail"] = "Poblem with reading user history. Try again!";
                return RedirectToAction("Index", "Home");
            }

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            Ticket ticket = unitOfWork.TicketRepository.GetById(id);
            if (ticket == null)
            {
                return HttpNotFound();
            }
            if (!(await UserManager.IsInRoleAsync(CurrentUser.Id, "Admin")) && ticket.CreatorId != CurrentUser.Id)
                return RedirectToAction("Index");
            try
            {
                foreach (var log in unitOfWork.TicketsHistoryRepository.Get(filters: new Expression<Func<TicketsHistory, bool>>[] { x => x.TicketId == id }))
                    unitOfWork.TicketsHistoryRepository.Delete(log);
                unitOfWork.TicketRepository.Delete(id);
                unitOfWork.Save();

                TempData["Success"] = "Successfully deleted ticket!";
            }
            catch
            {
                TempData["Fail"] = "Cannot delete ticket. Try again!";
            }
            return RedirectToAction("Index");
        }

        // todo: move to UsersController or use Web Api instead if possible
        [OverrideAuthorization]
        public JsonResult FindUsers(string search)
        {
            var result = from x in UserManager.Users.Where
                         (
                              u => (u.FirstName + " " + u.LastName).ToLower().Contains(search.ToLower()) ||
                              u.Email.ToLower().Contains(search.ToLower())
                         )
                         select new
                         {
                             UserID = x.Id,
                             FirstName = x.FirstName,
                             LastName = x.LastName,
                             Email = x.Email
                         };
            return Json(result);
        }

        public FileResult DownloadTicketsAsCSV()
        {
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("Created on;Created by;Requested by;Assigned to;Status;Category;Title;Content;Solution");
            foreach (Ticket ticket in unitOfWork.TicketRepository.Get(orderBy: x => x.OrderByDescending(t => t.CreateDate)))
            {
                csv.AppendLine($"{ticket.CreateDate};{ticket.Creator?.FirstName} {ticket.Creator?.LastName};{ticket.Requester?.FirstName} {ticket.Requester?.LastName};{ticket.AssignedUser?.FirstName} {ticket.AssignedUser?.LastName};{ticket.Status};{ticket.Category?.Name};{ticket.Title};{ticket.Content};{ticket.Solution};");
            }
            return File(Encoding.GetEncoding("ISO-8859-2").GetBytes(csv.ToString()), "text/plain", string.Format("tickets.csv"));
        }

        [HttpPost]
        public async Task<JsonResult> AssignUserToTicket(string userId, int ticketId)
        {
            try
            {
                User user = await UserManager.FindByIdAsync(userId);
                Ticket ticket = unitOfWork.TicketRepository.GetById(ticketId);
                Ticket oldTicket = (Ticket)ticket.Clone();

                ticket.AssignedUserId = user.Id;
                ticket.Status = "In progress";
                unitOfWork.TicketRepository.Update(ticket);

                updateTicketHistory(oldTicket, ticket);
                unitOfWork.Save();

                TempData["Success"] = "Successfully assigned user to ticket!";
            }
            catch
            {
                TempData["Fail"] = "Cannot assign user to ticket. Try again!";
            }
            return Json(new { });
        }

        [HttpPost]
        public async Task<JsonResult> SolveTicket(string userId, int ticketId, string solution)
        {
            try
            {
                User user = await UserManager.FindByIdAsync(userId);
                Ticket ticket = unitOfWork.TicketRepository.GetById(ticketId);
                Ticket oldTicket = (Ticket)ticket.Clone();

                ticket.AssignedUserId = user.Id;
                ticket.Status = "Solved";
                ticket.Solution = solution;
                unitOfWork.TicketRepository.Update(ticket);

                updateTicketHistory(oldTicket, ticket);                
                unitOfWork.Save();
                TempData["Success"] = "Successfully solved ticket!";
            }
            catch
            {
                TempData["Fail"] = "Cannot solve ticket. Try again!";
            }
            return Json(new { });
        }

        [HttpPost]
        public JsonResult CloseTicket(int ticketId)
        {
            try
            {
                Ticket ticket = unitOfWork.TicketRepository.GetById(ticketId);
                Ticket oldTicket = (Ticket)ticket.Clone();
                
                ticket.AssignedUserId = CurrentUser.Id;
                ticket.Status = "Closed";
                unitOfWork.TicketRepository.Update(ticket);

                updateTicketHistory(oldTicket, ticket);                
                unitOfWork.Save();
                TempData["Success"] = "Successfully closed ticket!";
            }
            catch
            {
                TempData["Fail"] = "Cannot close ticket. Try again!";
            }
            return Json(new { });
        }
    }
}
