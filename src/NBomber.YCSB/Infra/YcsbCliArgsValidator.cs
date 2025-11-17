using FluentValidation;

namespace NBomber.YCSB.Infra
{
    public class YcsbCliArgsValidator : AbstractValidator<YcsbCliArgs>
    {
        public YcsbCliArgsValidator()
        {
            When(x => !string.IsNullOrWhiteSpace(x.ExportFile), () =>
            {
                RuleFor(x => x.ExportFile)
                    .Must(DirectoryExists)
                    .WithMessage("The directory for ExportFile does not exist.");
            });

        }

        private bool DirectoryExists(string? exportFilePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(exportFilePath);
                return Directory.Exists(dir);
            }
            catch
            {
                return false;
            }
        }
    }
}
