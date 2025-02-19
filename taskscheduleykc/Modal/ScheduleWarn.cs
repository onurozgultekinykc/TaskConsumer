using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace taskscheduleykc.Modal
{
    public class ScheduleWarn
    {
        public Guid UserId { get; set; }
        public Guid ProductId { get; set; }
        public string BaseUrl { get; set; }
        public string MethodName { get; set; }
        public string ObjectStringfy { get; set; }
      
    }
}
