using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ContactManager.Data;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ContactManager.Models
{
    public class EditContactViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        //Add titles for dropdown
        public List<SelectListItem> AvailableTitles { get; set; } = new List<SelectListItem>();
        public string FirstName { get; set; }
        public string LastName { get; set; }
        //Add date annotations for validation
        [DataType(DataType.Date)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime DOB { get; set; }
        public List<EmailAddress> EmailAddresses { get; set; }
        public List<Address> Addresses { get; set; }
    }
}
