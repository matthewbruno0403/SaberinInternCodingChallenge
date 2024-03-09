using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ContactManager.Models
{
    public class SaveContactViewModel
    {
        public Guid ContactId { get; set; }
        public string Title { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        //Added date annotations for validation
        [DataType(DataType.Date)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime DOB { get; set; }
        public List<EmailViewModel> Emails { get; set; } = new List<EmailViewModel>();
        public List<AddressViewModel> Addresses { get; set; } = new List<AddressViewModel>();

    }
}
