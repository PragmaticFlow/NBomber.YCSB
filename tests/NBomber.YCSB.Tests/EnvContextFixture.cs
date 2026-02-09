using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Npgsql;

namespace NBomber.YCSB.Tests
{
    public class EnvContextFixture : IDisposable
    {
        private readonly ICompositeService _docker;

        public EnvContextFixture()
        {
            var startDocker = true;

            if (startDocker)
            {
                var dockerCompose = "docker-compose.yml";

                _docker = new Builder()
                    .UseContainer()
                    .UseCompose()
                    .FromFile(dockerCompose)
                    .RemoveOrphans()
                    .Build();

                _docker.Start();

                WaitForPostgres().Wait();
            }
        }

        private static async Task WaitForPostgres()
        {
            var csb = new NpgsqlConnectionStringBuilder
            {
                Host = "localhost",
                Port = 5432,
                Database = "mydb",
                Username = "myuser",
                Password = "mysecretpassword",
                SslMode = SslMode.Disable,
                Timeout = 5
            };

            var until = DateTime.UtcNow.AddSeconds(30);

            while (DateTime.UtcNow < until)
            {
                try
                {
                    await using var conn = new NpgsqlConnection(csb.ConnectionString);
                    await conn.OpenAsync();

                    return;
                }
                catch
                {
                    await Task.Delay(500);
                }
            }
        }

        public void Dispose()
        {
            _docker.Stop();
            _docker.Dispose();
        }
    }
}
