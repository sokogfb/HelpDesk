﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using HelpDesk.Entities;
using System.Data.Entity;

namespace HelpDesk.DAL
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly HelpDeskContext context = new HelpDeskContext();

        private UserRepository userRepository;
        private GenericRepository<Ticket> ticketRepository;
        private GenericRepository<Category> categoryRepository;

        public UserRepository UserRepository
        {
            get
            {
                if (userRepository == null)
                    userRepository = new UserRepository();
                return userRepository;
            }
        }

        public GenericRepository<Ticket> TicketRepository
        {
            get
            {
                if (ticketRepository == null)
                    ticketRepository = new GenericRepository<Ticket>(context);
                return ticketRepository;
            }
        }

        public GenericRepository<Category> CategoryRepository
        {
            get
            {
                if (categoryRepository == null)
                    categoryRepository = new GenericRepository<Category>(context);
                return categoryRepository;
            }
        }

        public void Save()
        {
            context.SaveChanges();
        }
    }
}