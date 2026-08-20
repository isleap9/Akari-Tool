using System;
using System.Globalization;
using System.Text.Json;

namespace AkariTool.Infrastructure.Features.Common.Utilities;

public static class NumericConversionHelper
{
    /// <summary>
    /// Converts a raw powercfg API value to display units based on the display unit string.
    /// Inverse of <see cref="ConvertToSystemUnits"/> (which lives on PowerCfgApplier).
    /// For example, converts 1200 seconds to 20 minutes when display units are "Minutes".
    /// </summary>
    public static int ConvertFromSystemUnits(int systemValue, string? displayUnits)
    {
        return displayUnits?.ToLowerInvariant() switch
        {
            "minutes" => systemValue / 60,
            "hours" => systemValue / 3600,
            // USB selective suspend timeout (the sole "Milliseconds" setting today) is
            // stored natively in milliseconds, so the display unit matches the system
            // unit 1:1 (mirror of Winhance's UnitConversionHelper).
            "milliseconds" => systemValue,
            _ => systemValue
        };
    }

    public static int ConvertNumericValue(object value)
    {
        return value switch
        {
            int intVal => intVal,
            long longVal => (int)longVal,
            double doubleVal => (int)doubleVal,
            float floatVal => (int)floatVal,
            string stringVal when int.TryParse(stringVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
            JsonElement je when je.TryGetInt32(out int jsonInt) => jsonInt,
            _ => throw new ArgumentException($"Cannot convert '{value}' (type: {value?.GetType().Name ?? "null"}) to numeric value")
        };
    }
}
