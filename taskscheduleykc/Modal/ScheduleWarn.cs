using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace taskscheduleykc.Modal
{
    public class ScheduleWarn
    {
        public Guid ScheduleWarnId { get; set; }
        public Guid UserId { get; set; }
        public Guid ProductId { get; set; }
        public string BaseUrl { get; set; }
        public string MethodName { get; set; }
        public string ObjectStringfy { get; set; }
        public string SecretKey { get; set; }
        public Guid EventId { get; set; }
        public bool Recived { get; set; }
        /// <summary>
        /// "0"-Notification Sistem "1" E-mail
        /// </summary>
        public string NotificationType { get; set; }
        public DateTime NotificationTime { get; set; }

    }
}
