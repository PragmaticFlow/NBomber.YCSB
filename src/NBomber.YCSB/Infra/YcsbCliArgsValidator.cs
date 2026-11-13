using FluentValidation;

namespace NBomber.YCSB.Infra;

public class YcsbCliArgsValidator : AbstractValidator<YcsbCliArgs>
{
    public YcsbCliArgsValidator()
    {
        RuleFor(x => x.MinScanLength)
            .GreaterThanOrEqualTo(1)
            .WithMessage("minscanlength must be at least 1.");

        RuleFor(x => x.MinScanLength)
            .LessThanOrEqualTo(x => x.GetMaxScanLength())
            .WithMessage(x => $"minscanlength ({x.MinScanLength}) must not exceed maxscanlength ({x.GetMaxScanLength()}).");

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
