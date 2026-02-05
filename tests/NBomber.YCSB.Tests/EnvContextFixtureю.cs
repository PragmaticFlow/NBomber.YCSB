using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;

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
            }
        }

        public void Dispose()
        {
            _docker.Stop();
            _docker.Dispose();
        }
    }
}
