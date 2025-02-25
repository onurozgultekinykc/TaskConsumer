
using Hangfire;
using Hangfire.PostgreSql;
using Npgsql;
using System;
using taskscheduleykc;

class Program
{
    static async Task Main(string[] args)
    {
        string connectionString = ConsumerConfig.PostgresConnectionString;

        // Önce tabloyu kontrol edip oluştur
        await EnsureTableExists(connectionString);

        var postgresOptions = new PostgreSqlStorageOptions
        {
            PrepareSchemaIfNecessary = true, // Gerekirse şemayı otomatik oluştur
            QueuePollInterval = TimeSpan.FromSeconds(10) // Kuyruk sorgulama aralığı
        };

        GlobalConfiguration.Configuration
            .UsePostgreSqlStorage(connectionString, postgresOptions);

        using (var server = new BackgroundJobServer())
        {
            Console.WriteLine("Hangfire is ready!");

            // RabbitMQ dinlemeyi başlat
            var taskConsumer = new TaskConsumer();
            taskConsumer.InitializeAsync().Wait();
            taskConsumer.StartListening();

            // Programın kapanmaması için bekleme
            Console.WriteLine("Task Consumer is running!");
            await Task.Delay(-1);
        }
    }

    private static async Task EnsureTableExists(string connectionString)
    {
        try
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string createTableQuery = @"
                    CREATE TABLE IF NOT EXISTS JobDtoScheduleWarn (
                        ScheduleWarnId VARCHAR(255) PRIMARY KEY,
                        JobId VARCHAR(255) NOT NULL
                    );";

                using (var command = new NpgsqlCommand(createTableQuery, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
            Console.WriteLine("JobDtoScheduleWarn table checked ");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tablo kontrol edilirken hata oluştu: {ex.Message}");
        }
    }

}