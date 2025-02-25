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
using Hangfire;
using Hangfire.PostgreSql;
using Npgsql;
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
                //HostName = "192.168.1.76",
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

                    if (scheduleWarn != null&&scheduleWarn.MethodName!="Remove")
                    {
                        // Gelen obje bir metoda gidecek parametre olarak ve 10 dk sonra çalışacak
                        string jobId = BackgroundJob.Schedule(() => ProcessSchedule(scheduleWarn), TimeSpan.FromSeconds(20));

                        // JobId ve ScheduleWarnId'yi veritabanına ekleyelim
                        await InsertJobRecord(scheduleWarn.ScheduleWarnId.ToString(), jobId);

                        await _channel.BasicAckAsync(ea.DeliveryTag, false); // Mesaj işlendiyse onayla
                    }

                    if (scheduleWarn != null && scheduleWarn.MethodName == "Remove")
                    {
                        // Önce ScheduleWarnId'ye karşılık gelen JobId'yi veritabanından bul
                        string jobId = await GetJobIdByScheduleWarnId(scheduleWarn.ScheduleWarnId.ToString());

                        if (!string.IsNullOrEmpty(jobId))
                        {
                            // Hangfire'dan job'u sil
                            BackgroundJob.Delete(jobId);

                            // Veritabanından kaydı sil
                            await DeleteJobRecord(scheduleWarn.ScheduleWarnId.ToString());
                           
                        }

                        await _channel.BasicAckAsync(ea.DeliveryTag, false); // Mesaj işlendiyse onayla
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Task gönderme hatası: {ex.Message}");
                    await _channel.BasicAckAsync(ea.DeliveryTag, false); // Hata olursa mesajı tekrar kuyruğa koy
                }
            };

            _channel.BasicConsumeAsync(queue: "task_queue",
                                  autoAck: false, // Manuel onaylama
                                  consumer: consumer);

            Console.WriteLine("task_queue Consumer çalışıyor, task kuyruğu dinleniyor...");
        }
        public int sayac = 1;
        [AutomaticRetry(Attempts = 7, DelaysInSeconds = new int[] { 10, 30, 60, 120, 500,2000,5000 })]
        public static async Task ProcessSchedule(ScheduleWarn scheduleWarn)
        {
            try
            {
                if (scheduleWarn.BaseUrl.Contains("localhost"))
                    scheduleWarn.BaseUrl="http://127.0.0.1:8090";
                string baseUri = scheduleWarn.BaseUrl + "/ScheduleHookQueue/ReceiveHook";

                using (HttpClient client = new HttpClient())
                {
                    string jsonData = JsonConvert.SerializeObject(scheduleWarn);
                    StringContent content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = client.PostAsync(baseUri, content).Result;

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Hata: {response.StatusCode}");
                    }

                    Console.WriteLine("Has been send: "+"Time:"+scheduleWarn.NotificationTime +" Schedule Post to: "+baseUri);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata oluştu: {ex.Message}");
                throw; // Hangfire otomatik olarak tekrar deneyecektir
            }
        }
        private static async Task InsertJobRecord(string scheduleWarnId, string jobId)
        {
            string connectionString = ConsumerConfig.PostgresConnectionString;

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string insertQuery = @"
                INSERT INTO JobDtoScheduleWarn (ScheduleWarnId, JobId) 
                VALUES (@ScheduleWarnId, @JobId);";

                    using (var command = new NpgsqlCommand(insertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@ScheduleWarnId", scheduleWarnId);
                        command.Parameters.AddWithValue("@JobId", jobId);

                        await command.ExecuteNonQueryAsync();
                    }
                }
                Console.WriteLine($"Job eklendi -> ScheduleWarnId: {scheduleWarnId}, JobId: {jobId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Job ekleme hatası: {ex.Message}");
            }
        }
        private static async Task<string> GetJobIdByScheduleWarnId(string scheduleWarnId)
        {
            string connectionString = ConsumerConfig.PostgresConnectionString;

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = "SELECT JobId FROM JobDtoScheduleWarn WHERE ScheduleWarnId = @ScheduleWarnId";

                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ScheduleWarnId", scheduleWarnId);
                        var result = await command.ExecuteScalarAsync();
                        return result?.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JobId sorgulama hatası: {ex.Message}");
                return null;
            }
        }

        private static async Task DeleteJobRecord(string scheduleWarnId)
        {
            string connectionString = ConsumerConfig.PostgresConnectionString;

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string deleteQuery = "DELETE FROM JobDtoScheduleWarn WHERE ScheduleWarnId = @ScheduleWarnId";

                    using (var command = new NpgsqlCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@ScheduleWarnId", scheduleWarnId);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                Console.WriteLine($"Job kaydı silindi -> ScheduleWarnId: {scheduleWarnId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Job kaydı silme hatası: {ex.Message}");
            }
        }


    }
}
