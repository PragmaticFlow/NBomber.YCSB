using System.CommandLine;
using NBomber.YCSB.Infra;

namespace NBomber.YCSB.Tests
{
    public class ScanLengthTests
    {
        private const int Draws = 100_000;

        private static YcsbCliArgs ParseRun(params string[] args)
        {
            var runCommand = new Command("run");
            YcsbCliArgs.AddOptionsTo(runCommand);

            var rootCommand = new RootCommand();
            rootCommand.Subcommands.Add(runCommand);

            var parseResult = rootCommand.Parse(["run", .. args]);
            Assert.Empty(parseResult.Errors);

            return YcsbCliArgs.FromParseResult(parseResult);
        }

        private static int[] Draw(YcsbCliArgs settings, int count, int seed)
        {
            var dataGen = new DataGenerator(settings);
            var random = new Random(seed);
            var values = new int[count];

            for (var i = 0; i < count; i++)
                values[i] = dataGen.NextScanLength(random);

            return values;
        }

        // Defaults and overrides

        [Theory]
        [InlineData(Workload.A, 1000)]
        [InlineData(Workload.B, 1000)]
        [InlineData(Workload.C, 1000)]
        [InlineData(Workload.D, 1000)]
        [InlineData(Workload.E, 100)]
        [InlineData(Workload.F, 1000)]
        public void GetDefaultMaxScanLength_Returns_Value_Per_Workload(Workload workload, int expected)
        {
            Assert.Equal(expected, WorkloadManager.GetDefaultMaxScanLength(workload));
        }

        [Fact]
        public void MinScanLength_Defaults_To_One()
        {
            Assert.Equal(1, new YcsbCliArgs().MinScanLength);
        }

        [Theory]
        [InlineData(Workload.E)]
        [InlineData(Workload.A)]
        public void GetMaxScanLength_Explicit_Value_Overrides_Default(Workload workload)
        {
            var settings = new YcsbCliArgs { Workload = workload, MaxScanLength = 500 };

            Assert.Equal(500, settings.GetMaxScanLength());
        }

        [Fact]
        public void GetMaxScanLength_Without_Explicit_Value_Uses_Workload_Default()
        {
            Assert.Equal(100, new YcsbCliArgs { Workload = Workload.E }.GetMaxScanLength());
            Assert.Equal(1000, new YcsbCliArgs { Workload = Workload.A }.GetMaxScanLength());
        }

        // Validator

        [Fact]
        public void Validator_Accepts_Defaults()
        {
            var result = new YcsbCliArgsValidator().Validate(new YcsbCliArgs { Workload = Workload.E });

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validator_Rejects_Min_Below_One()
        {
            var result = new YcsbCliArgsValidator().Validate(new YcsbCliArgs { Workload = Workload.A, MinScanLength = 0 });

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("minscanlength"));
        }

        [Fact]
        public void Validator_Rejects_Min_Above_Explicit_Max()
        {
            var result = new YcsbCliArgsValidator().Validate(
                new YcsbCliArgs { Workload = Workload.A, MinScanLength = 50, MaxScanLength = 10 });

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("minscanlength") && e.ErrorMessage.Contains("maxscanlength"));
        }

        [Fact]
        public void Validator_Rejects_Min_Above_Workload_Default_Max()
        {
            var result = new YcsbCliArgsValidator().Validate(new YcsbCliArgs { Workload = Workload.E, MinScanLength = 200 });

            Assert.False(result.IsValid);
        }

        // Parsing

        [Fact]
        public void Parse_Without_Scan_Options_Gives_Defaults()
        {
            var settings = ParseRun("--workload", "E");

            Assert.Equal(1, settings.MinScanLength);
            Assert.Null(settings.MaxScanLength);
        }

        [Fact]
        public void Parse_Explicit_Scan_Options()
        {
            var settings = ParseRun("--workload", "E", "--minscanlength", "5", "--maxscanlength", "500");

            Assert.Equal(5, settings.MinScanLength);
            Assert.Equal(500, settings.MaxScanLength);
        }

        // Generation

        [Fact]
        public void NextScanLength_Workload_E_Defaults_Cover_Full_Range_Uniformly()
        {
            var values = Draw(new YcsbCliArgs { Workload = Workload.E }, Draws, seed: 12345);

            Assert.All(values, v => Assert.InRange(v, 1, 100));
            Assert.Contains(1, values);
            Assert.Contains(100, values);
            Assert.InRange(values.Average(), 49.5, 51.5);
        }

        [Fact]
        public void NextScanLength_Equal_Bounds_Gives_Fixed_Length()
        {
            var values = Draw(new YcsbCliArgs { Workload = Workload.E, MinScanLength = 10, MaxScanLength = 10 }, 1000, seed: 1);

            Assert.All(values, v => Assert.Equal(10, v));
        }

        [Fact]
        public void NextScanLength_Explicit_Max_Is_Respected()
        {
            var values = Draw(new YcsbCliArgs { Workload = Workload.E, MaxScanLength = 500 }, Draws, seed: 2);

            Assert.All(values, v => Assert.InRange(v, 1, 500));
            Assert.Contains(values, v => v > 100);
        }

        [Fact]
        public void NextScanLength_Min_Override_Uses_Workload_Default_Max()
        {
            var values = Draw(new YcsbCliArgs { Workload = Workload.E, MinScanLength = 20 }, Draws, seed: 3);

            Assert.All(values, v => Assert.InRange(v, 20, 100));
        }

        [Fact]
        public void NextScanLength_Int_Max_Value_Does_Not_Overflow()
        {
            var values = Draw(new YcsbCliArgs { Workload = Workload.E, MaxScanLength = int.MaxValue }, 10_000, seed: 4);

            Assert.All(values, v => Assert.InRange(v, 1, int.MaxValue));
        }
    }
}
