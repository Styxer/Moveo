using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class Task
    {
        
        public Guid Id { get; set; }
      
        public string Title { get; set; } = string.Empty;
       
        public string Description { get; set; } = string.Empty;
  
        public TaskStatus Status { get; set; }

        // Foreign key linking the task to its parent project.
        // Represents the 'many' side of the one-to-many relationship with Project.
        public Guid ProjectId { get; set; }
      
        public Project? Project { get; set; }
    }
}
