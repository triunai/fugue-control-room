using System.ComponentModel;
using System.Globalization;

namespace Fugue.Domain.Ids;

/// <summary>
/// Lets MVC bind a strongly-typed id from a route or query string (MVC treats a struct without a
/// <see cref="TypeConverter"/> as a complex type). The id also implements <see cref="IParsable{TSelf}"/> for minimal-API style binding.
/// </summary>
public sealed class GuidIdTypeConverter<TId> : TypeConverter
    where TId : struct, IStronglyTypedId<TId>
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            if (Guid.TryParse(text, out Guid guid) && guid != Guid.Empty)
            {
                return TId.From(guid);
            }

            throw new FormatException($"'{text}' is not a valid {typeof(TId).Name}.");
        }

        return base.ConvertFrom(context, culture, value);
    }
}
