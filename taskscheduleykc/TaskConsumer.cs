using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using taskscheduleykc.Modal;

namespace taskscheduleykc
{
    internal class TaskConsumer
    {
        private ConnectionFactory _factory;
        private IConnection _connection;
        private IChannel _channel;

        public async Task InitializeAsync()
        {
            _factory = new ConnectionFactory
            {
                Port = 5672,
                HostName = "c_rabbitmq",
                UserName = "user",
                Password = "1234567",

            };

            // RabbitMQ bağlantısı oluştur (senkron API kullanıldığı için doğrudan çağırılıyor)
            _connection =await _factory.CreateConnectionAsync();
            _channel =await _connection.CreateChannelAsync();

            // Kuyruğu tanımla (Asenkron olmayan metot olduğu için direkt çağırılıyor)
            await _channel.QueueDeclareAsync(queue: "task_queue",
                                   durable: true,
                                   exclusive: false,
                                   autoDelete: false,
                                   arguments: null);

            await Task.CompletedTask; // Metodun async yapısını korumak için ekledik
        }


        public void StartListening()
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    var scheduleWarn = JsonConvert.DeserializeObject<ScheduleWarn>(message);

                    if (scheduleWarn != null)
                    {
                       //iş yapılacak
                        await _channel.BasicAckAsync(ea.DeliveryTag, false); // Mesaj işlendiyse onayla
                    }
                 
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Mail gönderme hatası: {ex.Message}");
                    await _channel.BasicAckAsync(ea.DeliveryTag, false); // Hata olursa mesajı tekrar kuyruğa koy
                }
            };

            _channel.BasicConsumeAsync(queue: "task_queue",
                                  autoAck: false, // Manuel onaylama
                                  consumer: consumer);

            Console.WriteLine("task_queue Consumer çalışıyor, schedule kuyruğu dinleniyor...");
        }
        public int sayac = 1;
     
    }
}
