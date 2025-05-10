using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class Project {
     
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
       
        // Represents a one-to-many relationship with the Task entity.
        public ICollection<Task> Tasks { get; set; } = new List<Task>();
    }
}
