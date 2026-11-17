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

        RuleFor(x => x.WarmUp)
            .GreaterThanOrEqualTo(0)
            .WithMessage(x => $"warmup ({x.WarmUp}) must be 0 or greater.");

        When(x => !string.IsNullOrWhiteSpace(x.ExportFile), () =>
        {
            RuleFor(x => x.ExportFile)
                .Must(IsValidExportFile)
                .WithMessage(x => $"exportfile ({x.ExportFile}) must be a valid path ending with a file name.");
        });
    }

    private static bool IsValidExportFile(string? exportFile)
    {
        var fileName = Path.GetFileName(exportFile);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        var folder = Path.GetDirectoryName(exportFile);
        return string.IsNullOrEmpty(folder) || folder.AsSpan().IndexOfAny(Path.GetInvalidPathChars()) < 0;
    }
}
