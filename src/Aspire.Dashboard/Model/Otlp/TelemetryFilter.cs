// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using Aspire.Dashboard.Otlp.Model;
using Aspire.Dashboard.Resources;
using Microsoft.Extensions.Localization;

namespace Aspire.Dashboard.Model.Otlp;

[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public class TelemetryFilter : IEquatable<TelemetryFilter>
{
    public string Field { get; set; } = default!;
    public FilterCondition Condition { get; set; }
    public string Value { get; set; } = default!;

    private string DebuggerDisplayText => $"{Field} {ConditionToString(Condition, null)} {Value}";

    public string GetDisplayText(IStringLocalizer<StructuredFiltering> loc) => $"{ResolveFieldName(Field)} {ConditionToString(Condition, loc)} {Value}";

    public static string ResolveFieldName(string name)
    {
        return name switch
        {
            KnownStructuredLogFields.MessageField => "Message",
            KnownStructuredLogFields.ApplicationField => "Application",
            KnownStructuredLogFields.TraceIdField => "TraceId",
            KnownStructuredLogFields.SpanIdField => "SpanId",
            KnownStructuredLogFields.OriginalFormatField => "OriginalFormat",
            KnownStructuredLogFields.CategoryField => "Category",
            KnownTraceFields.NameField => "Name",
            KnownTraceFields.SpanIdField => "SpanId",
            KnownTraceFields.TraceIdField => "TraceId",
            KnownTraceFields.KindField => "Kind",
            KnownTraceFields.ApplicationField => "Application",
            KnownTraceFields.StatusField => "Status",
            KnownTraceFields.SourceField => "Source",
            _ => name
        };
    }

    public static string ConditionToString(FilterCondition c, IStringLocalizer<StructuredFiltering>? loc) =>
        c switch
        {
            FilterCondition.Equals => "==",
            FilterCondition.Contains => loc?[nameof(StructuredFiltering.ConditionContains)] ?? "contains",
            FilterCondition.GreaterThan => ">",
            FilterCondition.LessThan => "<",
            FilterCondition.GreaterThanOrEqual => ">=",
            FilterCondition.LessThanOrEqual => "<=",
            FilterCondition.NotEqual => "!=",
            FilterCondition.NotContains => loc?[nameof(StructuredFiltering.ConditionNotContains)] ?? "not contains",
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
        };

    private static Func<string?, string, bool> ConditionToFuncString(FilterCondition c) =>
        c switch
        {
            FilterCondition.Equals => (a, b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase),
            FilterCondition.Contains => (a, b) => a != null && a.Contains(b, StringComparison.OrdinalIgnoreCase),
            // Condition.GreaterThan => (a, b) => a > b,
            // Condition.LessThan => (a, b) => a < b,
            // Condition.GreaterThanOrEqual => (a, b) => a >= b,
            // Condition.LessThanOrEqual => (a, b) => a <= b,
            FilterCondition.NotEqual => (a, b) => !string.Equals(a, b, StringComparison.OrdinalIgnoreCase),
            FilterCondition.NotContains => (a, b) => a != null && !a.Contains(b, StringComparison.OrdinalIgnoreCase),
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
        };

    private static Func<DateTime, DateTime, bool> ConditionToFuncDate(FilterCondition c) =>
        c switch
        {
            FilterCondition.Equals => (a, b) => a == b,
            //Condition.Contains => (a, b) => a.Contains(b),
            FilterCondition.GreaterThan => (a, b) => a > b,
            FilterCondition.LessThan => (a, b) => a < b,
            FilterCondition.GreaterThanOrEqual => (a, b) => a >= b,
            FilterCondition.LessThanOrEqual => (a, b) => a <= b,
            FilterCondition.NotEqual => (a, b) => a != b,
            //Condition.NotContains => (a, b) => !a.Contains(b),
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
        };

    private static Func<double, double, bool> ConditionToFuncNumber(FilterCondition c) =>
        c switch
        {
            FilterCondition.Equals => (a, b) => a == b,
            //Condition.Contains => (a, b) => a.Contains(b),
            FilterCondition.GreaterThan => (a, b) => a > b,
            FilterCondition.LessThan => (a, b) => a < b,
            FilterCondition.GreaterThanOrEqual => (a, b) => a >= b,
            FilterCondition.LessThanOrEqual => (a, b) => a <= b,
            FilterCondition.NotEqual => (a, b) => a != b,
            //Condition.NotContains => (a, b) => !a.Contains(b),
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
        };

    private string? GetFieldValue(OtlpLogEntry x)
    {
        return Field switch
        {
            KnownStructuredLogFields.MessageField => x.Message,
            KnownStructuredLogFields.ApplicationField => x.ApplicationView.Application.ApplicationName,
            KnownStructuredLogFields.TraceIdField => x.TraceId,
            KnownStructuredLogFields.SpanIdField => x.SpanId,
            KnownStructuredLogFields.OriginalFormatField => x.OriginalFormat,
            KnownStructuredLogFields.CategoryField => x.Scope.ScopeName,
            _ => x.Attributes.GetValue(Field)
        };
    }

    private string? GetFieldValue(OtlpSpan x)
    {
        return Field switch
        {
            KnownTraceFields.ApplicationField => x.Source.Application.ApplicationName,
            KnownTraceFields.TraceIdField => x.TraceId,
            KnownTraceFields.SpanIdField => x.SpanId,
            KnownTraceFields.KindField => x.Kind.ToString(),
            KnownTraceFields.StatusField => x.Status.ToString(),
            KnownTraceFields.SourceField => x.Scope.ScopeName,
            KnownTraceFields.NameField => x.Name,
            _ => x.Attributes.GetValue(Field)
        };
    }

    public IEnumerable<OtlpLogEntry> Apply(IEnumerable<OtlpLogEntry> input)
    {
        switch (Field)
        {
            case nameof(OtlpLogEntry.TimeStamp):
                {
                    var date = DateTime.Parse(Value, CultureInfo.InvariantCulture);
                    var func = ConditionToFuncDate(Condition);
                    return input.Where(x => func(x.TimeStamp, date));
                }
            case nameof(OtlpLogEntry.Severity):
                {
                    if (Enum.TryParse<LogLevel>(Value, ignoreCase: true, out var value))
                    {
                        var func = ConditionToFuncNumber(Condition);
                        return input.Where(x => func((int)x.Severity, (double)value));
                    }
                    return input;
                }
            case nameof(OtlpLogEntry.Message):
                {
                    var func = ConditionToFuncString(Condition);
                    return input.Where(x => func(x.Message, Value));
                }
            default:
                {
                    var func = ConditionToFuncString(Condition);
                    return input.Where(x => func(GetFieldValue(x), Value));
                }
        }
    }

    public bool Apply(OtlpSpan span)
    {
        var fieldValue = GetFieldValue(span);
        var func = ConditionToFuncString(Condition);
        return func(fieldValue, Value);
    }

    public bool Equals(TelemetryFilter? other)
    {
        if (other == null)
        {
            return false;
        }

        if (Field != other.Field)
        {
            return false;
        }

        if (Condition != other.Condition)
        {
            return false;
        }

        if (!string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}