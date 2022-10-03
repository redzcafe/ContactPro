﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using ContactPro.Data;
using ContactPro.Models;
using Microsoft.AspNetCore.Authorization;
using ContactPro.Enums;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ContactPro.Services.Interfaces;
using ContactPro.Services;
using AspNetCore;

namespace ContactPro.Controllers
{
    public class ContactsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IImageService _imageService;
        private readonly IAddressBookService _addressBookService;


        public ContactsController(ApplicationDbContext context, 
                                    UserManager<AppUser> userManager,
                                    IImageService imageService,
                                    IAddressBookService addressBookService)
        {
            _context = context;
            _userManager = userManager;
            _imageService = imageService;
            _addressBookService = addressBookService;


        }

        // GET: Contacts
        [Authorize]
        public IActionResult Index(int categoryId)
        {
            var contacts = new List<Contact>();
            string appUserID = _userManager.GetUserId(User);

            // return UserID and Associated Contacts and Categories

            AppUser? appUser = _context.Users
                                        .Include(c => c.Contacts)
                                        .ThenInclude(c => c.Categories)
                                        .FirstOrDefault(u => u.Id == appUserID);

            var categories = appUser?.Categories;

            if (categoryId == 0)
            {
                contacts = appUser?.Contacts.OrderBy(c => c.LastName)
                                            .ThenBy(c => c.FirstName)
                                            .ToList();
            }
            else
            {

                contacts = appUser?.Categories.FirstOrDefault(c => c.Id == categoryId)
                                    .Contacts
                                    .OrderBy(c => c.LastName)
                                    .ThenBy(c => c.FirstName)
                                    .ToList();

            }

            ViewData["CategoryID"] = new SelectList(categories, "Id", "Name", categoryId);

            return View(contacts);


        }

        [Authorize]
        public IActionResult SearchContacts(string searchString)
        {

            string appUserId = _userManager.GetUserId(User);
            var contacts = new List<Contact>();

            AppUser? appUser = _context.Users
                                        .Include(c => c.Contacts)
                                        .ThenInclude(c => c.Categories)
                                        .FirstOrDefault(u => u.Id == appUserId);
            if (String.IsNullOrEmpty(searchString))
            {
                contacts = appUser?.Contacts
                                    .OrderBy(c => c.LastName)
                                    .ThenBy(c => c.FirstName)
                                    .ToList();

            }
            else
            {
                contacts = appUser?.Contacts.Where(c => c.FullName!.ToLower().Contains(searchString.ToLower()))
                                    .OrderBy(c => c.LastName)
                                    .ThenBy(c => c.FirstName)
                                    .ToList();

            }

            ViewData["CategoryId"] = new SelectList(appUser.Categories, "Id", "Name", 0);

            return View(nameof(Index), contacts);
        }

         
        // GET: Contacts/Details/5
        [Authorize]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Contacts == null)
            {
                return NotFound();
            }

            var contact = await _context.Contacts
                .Include(c => c.AppUser)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (contact == null)
            {
                return NotFound();
            }

            return View(contact);
        }

        // GET: Contacts/Create
        [Authorize]
        public async Task<IActionResult> Create()
        {

            //ViewData["AppUserId"] = new SelectList(_context.Users, "Id", "Id");
            string AppUserId = _userManager.GetUserId(User);

            ViewData["StatesList"] = new SelectList(Enum.GetValues(typeof(States)).Cast<States>().ToList());
            ViewData["CategoryList"] = new MultiSelectList(await _addressBookService.GetUserCategoriesAsync(AppUserId), "Id", "Name");


            return View();
        }

        // POST: Contacts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,FirstName,LastName,BirthDate,Address1,Address2,City,State,Zipcode,EmailAddress,PhoneNumber,ImageFile")] Contact contact, List<int> CategoryList)
        {
            ModelState.Remove("AppUserId");


            if (ModelState.IsValid)
            {
                contact.AppUserId = _userManager.GetUserId(User);
                contact.Created = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);

                if (contact.BirthDate != null) {
                    contact.BirthDate = DateTime.SpecifyKind(contact.BirthDate.Value, DateTimeKind.Utc);
                }

                if(contact.ImageFile != null)
                {
                    contact.ImageData = await _imageService.ConvertFileToByteArrayAsync(contact.ImageFile);
                    contact.ImageType = contact.ImageFile.ContentType;

                }
               

                _context.Add(contact);
                await _context.SaveChangesAsync();

                // loop over all the selected categories
                foreach (int categoryId in CategoryList)
                {
                    await _addressBookService.AddContactToCategoryAsync(categoryId, contact.Id);

                }


                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Contacts/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Contacts == null)
            {
                return NotFound();
            }

            var contact = await _context.Contacts.FindAsync(id);
            if (contact == null)
            {
                return NotFound();
            }
            ViewData["AppUserId"] = new SelectList(_context.Users, "Id", "Id", contact.AppUserId);
            return View(contact);
        }

        // POST: Contacts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,AppUserId,FirstName,LastName,BirthDate,Address1,Address2,City,State,Zipcode,EmailAddress,PhoneNumber,Created,ImageData,ImageType")] Contact contact)
        {
            if (id != contact.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(contact);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ContactExists(contact.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["AppUserId"] = new SelectList(_context.Users, "Id", "Id", contact.AppUserId);
            return View(contact);
        }

        // GET: Contacts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Contacts == null)
            {
                return NotFound();
            }

            var contact = await _context.Contacts
                .Include(c => c.AppUser)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (contact == null)
            {
                return NotFound();
            }

            return View(contact);
        }

        // POST: Contacts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Contacts == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Contacts'  is null.");
            }
            var contact = await _context.Contacts.FindAsync(id);
            if (contact != null)
            {
                _context.Contacts.Remove(contact);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ContactExists(int id)
        {
          return _context.Contacts.Any(e => e.Id == id);
        }
    }
}