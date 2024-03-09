using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ContactManager.Data;
using ContactManager.Hubs;
using ContactManager.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MailKit;
using MimeKit;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ContactManager.Controllers
{
    public class ContactsController : Controller
    {
        //Added Ilogger field
        private readonly ApplicationContext _context;
        private readonly IHubContext<ContactHub> _hubContext;
        private readonly ILogger<ContactsController> _logger;

        //Added ILogger to constructor
        public ContactsController(ApplicationContext context, IHubContext<ContactHub> hubContext, ILogger<ContactsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        //Added try catch to DeleteContact
        //Added Logging to DeleteContact
        public async Task<IActionResult> DeleteContact(Guid id)
        {
            _logger.LogInformation("Attempting to delete contact with ID: {ContactId}", id);

            try
            {
                var contactToDelete = await _context.Contacts
                    .Include(x => x.EmailAddresses)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (contactToDelete == null)
                {
                    _logger.LogWarning("DeleteContact attempt for non-existing contact ID: {ContactId}", id);
                    return BadRequest();
                }

                _context.EmailAddresses.RemoveRange(contactToDelete.EmailAddresses);
                _context.Contacts.Remove(contactToDelete);

                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("Update");

                _logger.LogInformation("Contact with ID: {ContactId} deleted successfully.", id);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in {MethodName} with details: {ErrorMessage}", nameof(DeleteContact), ex.Message);
                return StatusCode(500, "An error occurred while processing your request.");
            }

        }

        //Added try catch to EditContact
        //Added Logging to EditContact
        //add AvailableTitles
        public async Task<IActionResult> EditContact(Guid id)
        {
            _logger.LogInformation("Initiating edit for contact with ID: {ContactId}", id);
            try
            {
                var contact = await _context.Contacts
                    .Include(x => x.EmailAddresses)
                    .Include(x => x.Addresses)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (contact == null)
                {
                    _logger.LogWarning("Attempted to edit a non-existing contact with ID: {ContactId}", id);
                    return NotFound();
                }

                var viewModel = new EditContactViewModel
                {
                    Id = contact.Id,
                    Title = contact.Title,
                    FirstName = contact.FirstName,
                    LastName = contact.LastName,
                    DOB = contact.DOB,
                    EmailAddresses = contact.EmailAddresses,
                    Addresses = contact.Addresses,
                    AvailableTitles = GetAvailableTitles()
                };

                _logger.LogInformation("Successfully prepared edit view model for contact with ID: {ContactId}", id);
                return PartialView("_EditContact", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in {MethodName} with details: {ErrorMessage}", nameof(EditContact), ex.Message);
                return StatusCode(500, "An internal error occurred while processing your request.");
            }
        }

        //Added try catch to GetContact
        //Added Logging to GetContact
        public async Task<IActionResult> GetContacts()
        {
            _logger.LogInformation("Attempting to retrieve all contacts");
            try
            {
                var contactList = await _context.Contacts
                    .OrderBy(x => x.FirstName)
                    .ToListAsync();

                _logger.LogInformation("Successfully retrieved {Count} contacts.", contactList.Count);
                return PartialView("_ContactTable", new ContactViewModel { Contacts = contactList });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occured in {MethodName} with details: {ErrorMessage}", nameof(GetContacts), ex.Message);
                return StatusCode(500, "An internal error occurred while processing your request.");
            }

        }

        public IActionResult Index()
        {
            return View();
        }

        //Modify NewContact() to provide new instance of EditContactViewModel  with available titles populated
        public IActionResult NewContact()
        {
            var viewModel = new EditContactViewModel
            {
                AvailableTitles = GetAvailableTitles()
            };

            return PartialView("_EditContact", viewModel);
        }

        private List<SelectListItem> GetAvailableTitles()
        {
            // This method should return a list of SelectListItem objects representing the titles.
            // For example:
            return new List<SelectListItem>
    {
        new SelectListItem { Text = "Mr", Value = "Mr" },
        new SelectListItem { Text = "Mrs", Value = "Mrs" },
        new SelectListItem { Text = "Ms", Value = "Ms" },
        new SelectListItem { Text = "Dr", Value = "Dr" },
    };
        }

        //Added try catch to SaveContact
        //Added logger to SaveContact
        [HttpPost]
        public async Task<IActionResult> SaveContact([FromBody] SaveContactViewModel model)
        {
            _logger.LogInformation("Starting to save contact. ContactId: {ContactId}", model.ContactId);
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model state is invalid. Errors: {ModelStateErrors}", ModelState.Values.SelectMany(v => v.Errors));
                    return BadRequest(ModelState);
                }

                var contact = model.ContactId == Guid.Empty
                    ? new Contact { Title = model.Title, FirstName = model.FirstName, LastName = model.LastName, DOB = model.DOB }
                    : await _context.Contacts.Include(x => x.EmailAddresses).Include(x => x.Addresses).FirstOrDefaultAsync(x => x.Id == model.ContactId);

                if (contact == null)
                {
                    _logger.LogWarning("Contact to save not found. ContactId: {ContactId}", model.ContactId);
                    return NotFound();
                }

                if (model.ContactId != Guid.Empty)
                {
                    _logger.LogInformation("Updating contact. Clearing existing addresses and emails. ContactId: {ContactId}", model.ContactId);
                }

                _context.EmailAddresses.RemoveRange(contact.EmailAddresses);
                _context.Addresses.RemoveRange(contact.Addresses);

                _logger.LogDebug("Adding updated information to contact. ContactId: {ContactId}", model.ContactId);

                // Ensure only one primary email is set
                bool primaryEmailSet = false;
                foreach (var emailViewModel in model.Emails) 
                {
                    bool isPrimary = emailViewModel.IsPrimary && !primaryEmailSet;
                    if (isPrimary)
                    {
                        primaryEmailSet = true; // Mark that a primary email has been set
                    }
                    else
                    {
                        // If another primary email is attempted to be set, override it to false
                        emailViewModel.IsPrimary = false;
                    }

                    contact.EmailAddresses.Add(new EmailAddress
                    {
                        Type = emailViewModel.Type,
                        Email = emailViewModel.Email,
                        Contact = contact,
                        IsPrimary = isPrimary
                    });
                }


                foreach (var address in model.Addresses)
                {
                    contact.Addresses.Add(new Address
                    {
                        Street1 = address.Street1,
                        Street2 = address.Street2,
                        City = address.City,
                        State = address.State,
                        Zip = address.Zip,
                        Type = address.Type
                    });
                }

                contact.Title = model.Title;
                contact.FirstName = model.FirstName;
                contact.LastName = model.LastName;
                contact.DOB = model.DOB;

                if (model.ContactId == Guid.Empty)
                {
                    await _context.Contacts.AddAsync(contact);
                }
                else
                {
                    _context.Contacts.Update(contact);
                }

                _logger.LogInformation("Saving changes to the database. ContactId: {ContactId}", contact.Id);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Database changes saved successfully. ContactId: {ContactId}", contact.Id);

                _logger.LogInformation("Sending SignalR notification to all clients.");
                await _hubContext.Clients.All.SendAsync("Update");

                _logger.LogInformation("Sending email notification for contact update. ContactId: {ContactId}", contact.Id);
                SendEmailNotification(contact.Id);

                _logger.LogInformation("Contact saved successfully. ContactId: {ContactId}", contact.Id);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occured in {MethodName} with details: {ErrorMessage}", nameof(SaveContact), ex.Message);
                return StatusCode(500, "An internal error occurred while processing your request.");
            }
        }

        // Added try catch to SendEmailNotification
        //Added Logging to SendEmailNotification
        private void SendEmailNotification(Guid contactId)
        {
            _logger.LogInformation("Starting to send email notification for contact ID: {ContactId}", contactId);
            try
            {
                var message = new MimeMessage();

                message.From.Add(new MailboxAddress("noreply", "noreply@contactmanager.com"));
                message.To.Add(new MailboxAddress("SysAdmin", "Admin@contactmanager.com"));
                message.Subject = "ContactManager System Alert";

                message.Body = new TextPart("plain")
                {
                    Text = "Contact with id:" + contactId.ToString() + " was updated"
                };

                using (var client = new SmtpClient())
                {
                    // For demo-purposes, accept all SSL certificates (in case the server supports STARTTLS)
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    client.Connect("127.0.0.1", 25, false);

                    client.Send(message);
                    client.Disconnect(true);
                }
                _logger.LogInformation("Email notification sent successfully for contact ID: {ContactId}", contactId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occured in {MethodName} with details: {ErrorMessage}", nameof(SendEmailNotification), ex.Message);
            }
        }

    }

}