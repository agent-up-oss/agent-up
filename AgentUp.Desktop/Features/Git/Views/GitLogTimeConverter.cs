using System.Globalization;
using AgentUp.Desktop.Features.Git.Providers;
using Avalonia.Data.Converters;

namespace AgentUp.Desktop.Features.Git.Views;

public sealed class GitLogTimeConverter : IValueConverter
{
    public static readonly GitLogTimeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => GitLogLayoutProvider.FormatTime(value as string ?? string.Empty);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
