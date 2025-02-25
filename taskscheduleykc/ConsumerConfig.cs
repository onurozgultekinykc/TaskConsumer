using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace taskscheduleykc
{
    public class ConsumerConfig
    {
        public static string PostgresConnectionString { get; } = "Host=c_rabbitmq;Port=5432;Username=taskconsumer;Password=soft2022;Database=hangfire;";
    }
}
