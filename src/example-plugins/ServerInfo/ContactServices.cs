using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using ServiceStack;
using ServiceStack.Redis;
using ServiceStack.OrmLite;
using ServiceStack.Templates;
using ServiceStack.FluentValidation;

namespace ServerInfo
{
    [Route("/contacts", "GET")]
    public class GetContacts : IReturn<GetContactsResponse> {}

    public class GetContactsResponse 
    {
        public List<Contact> Results { get; set; }
    }

    [Route("/contacts", "POST PUT")]
    public class StoreContact 
    {
        public string Name { get; set; }
        public string Company { get; set; }
        public int Age { get; set; }
    }

    public class StoreContactResponse 
    {
        public ResponseStatus ResponseStatus { get; set; }
    }

    public class Contact
    {
        public string Name { get; set; }
        public string Company { get; set; }
        public int Age { get; set; }
    }

    [Route("/contacts/reset")]
    public class ResetContacts {}

    public class ContactValidator : AbstractValidator<StoreContact>
    {
        public ContactValidator()
        {
            RuleFor(r => r.Name).NotEmpty();
            RuleFor(r => r.Age).GreaterThan(13).WithMessage("Contacts must be older than 13");
            RuleFor(r => r.Company).NotEmpty();
        }
    }

    public class ContactServices : Service
    {
        private static ConcurrentDictionary<string, Contact> Contacts = new ConcurrentDictionary<string, Contact>();

        public object Get(GetContacts request) => new GetContactsResponse { Results = Contacts.Values.OrderBy(x => x.Name).ToList() };

        public object Any(StoreContact request) 
        {
            Contacts[request.Name] = request.ConvertTo<Contact>();
            return new StoreContactResponse();
        }        

        public void Any(ResetContacts request) => Contacts.Clear();
    }

}