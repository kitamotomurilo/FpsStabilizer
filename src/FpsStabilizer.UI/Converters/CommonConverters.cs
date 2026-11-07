using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FpsStabilizer.UI.Converters
{
    /// <summary>
    /// Converte bool para Visibility
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;
        public Visibility FalseVisibility { get; set; } = Visibility.Collapsed;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                var result = Invert ? !boolValue : boolValue;
                return result ? Visibility.Visible : FalseVisibility;
            }
            return FalseVisibility;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                var result = visibility == Visibility.Visible;
                return Invert ? !result : result;
            }
            return false;
        }
    }

    /// <summary>
    /// Converte null para Visibility
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;
        public Visibility NullVisibility { get; set; } = Visibility.Collapsed;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var isNull = value == null;
            var result = Invert ? !isNull : isNull;
            return result ? NullVisibility : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Converte enum para bool (para RadioButton binding)
    /// </summary>
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue && parameter != null)
            {
                return parameter;
            }
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Converte valor numérico para cor baseada em thresholds
    /// </summary>
    public class ValueToColorConverter : IValueConverter
    {
        public double GreenThreshold { get; set; } = 60;
        public double YellowThreshold { get; set; } = 30;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                if (d >= GreenThreshold) return new SolidColorBrush(Color.FromRgb(76, 175, 80));   // Green
                if (d >= YellowThreshold) return new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
                return new SolidColorBrush(Color.FromRgb(244, 67, 54));                            // Red
            }
            if (value is int i)
            {
                return Convert((double)i, targetType, parameter, culture);
            }
            return new SolidColorBrush(Color.FromRgb(136, 136, 136)); // Gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Converte bytes para string legível (KB, MB, GB)
    /// </summary>
    public class BytesToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
                int i = 0;
                double dblBytes = bytes;

                while (dblBytes >= 1024 && i < suffixes.Length - 1)
                {
                    dblBytes /= 1024;
                    i++;
                }

                return $"{dblBytes:0.##} {suffixes[i]}";
            }
            if (value is double d)
            {
                return Convert((long)d, targetType, parameter, culture);
            }
            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Converte TimeSpan para string legível
    /// </summary>
    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan ts)
            {
                if (ts.TotalDays >= 1)
                    return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
                if (ts.TotalHours >= 1)
                    return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
                if (ts.TotalMinutes >= 1)
                    return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
                return $"{ts.Seconds}s";
            }
            return "0s";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Converte prioridade do processo para string
    /// </summary>
    public class ProcessPriorityToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FpsStabilizer.Core.Models.ProcessPriority priority)
            {
                return priority switch
                {
                    FpsStabilizer.Core.Models.ProcessPriority.Idle => "Idle",
                    FpsStabilizer.Core.Models.ProcessPriority.BelowNormal => "Below Normal",
                    FpsStabilizer.Core.Models.ProcessPriority.Normal => "Normal",
                    FpsStabilizer.Core.Models.ProcessPriority.AboveNormal => "Above Normal",
                    FpsStabilizer.Core.Models.ProcessPriority.High => "High",
                    FpsStabilizer.Core.Models.ProcessPriority.RealTime => "Real Time",
                    _ => "Normal"
                };
            }
            return "Normal";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Converte AffinityMode para string
    /// </summary>
    public class AffinityModeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FpsStabilizer.Core.Models.AffinityMode mode)
            {
                return mode switch
                {
                    FpsStabilizer.Core.Models.AffinityMode.AllCores => "Todos os Cores",
                    FpsStabilizer.Core.Models.AffinityMode.SpecificCores => "Cores Específicos",
                    FpsStabilizer.Core.Models.AffinityMode.PerformanceCoresOnly => "Apenas Performance",
                    FpsStabilizer.Core.Models.AffinityMode.EfficiencyCoresOnly => "Apenas Efficiency",
                    FpsStabilizer.Core.Models.AffinityMode.ExcludeCore0 => "Excluir Core 0",
                    FpsStabilizer.Core.Models.AffinityMode.Custom => "Personalizado",
                    _ => "Todos os Cores"
                };
            }
            return "Todos os Cores";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Inverte bool
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
    }

    /// <summary>
    /// Converte string vazia/null para bool (para validação)
    /// </summary>
    public class StringToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace(value?.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Multi-converter para habilitar botão se múltiplas condições
    /// </summary>
    public class AndMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return values.All(v => v is bool b && b);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}