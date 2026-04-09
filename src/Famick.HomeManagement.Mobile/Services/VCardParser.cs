using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Parses vCard 3.0/4.0 formatted text into <see cref="SharedContactData"/>.
/// </summary>
public static class VCardParser
{
    public static SharedContactData? Parse(string vCardText)
    {
        if (string.IsNullOrWhiteSpace(vCardText))
            return null;

        // Unfold continuation lines (RFC 6350 §3.2: lines starting with space/tab are continuations)
        vCardText = UnfoldLines(vCardText);

        var lines = vCardText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        var contact = new SharedContactData();
        var inCard = false;
        var nameSet = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            if (trimmed.Equals("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase))
            {
                inCard = true;
                continue;
            }

            if (trimmed.Equals("END:VCARD", StringComparison.OrdinalIgnoreCase))
                break;

            if (!inCard)
                continue;

            var (property, parameters, value) = ParseLine(trimmed);
            if (string.IsNullOrEmpty(value) && property != "N")
                continue;

            switch (property.ToUpperInvariant())
            {
                case "N":
                    ParseStructuredName(value, contact);
                    nameSet = true;
                    break;

                case "FN":
                    if (!nameSet)
                        ParseFormattedName(value, contact);
                    break;

                case "ORG":
                    contact.CompanyName = UnescapeValue(value.Split(';')[0]);
                    break;

                case "TITLE":
                    contact.Title = UnescapeValue(value);
                    break;

                case "NOTE":
                    contact.Notes = UnescapeValue(value);
                    break;

                case "BDAY":
                    ParseBirthday(value, contact);
                    break;

                case "TEL":
                    ParsePhone(parameters, value, contact);
                    break;

                case "EMAIL":
                    ParseEmail(parameters, value, contact);
                    break;

                case "ADR":
                    ParseAddress(parameters, value, contact);
                    break;

                case "PHOTO":
                    ParsePhoto(parameters, value, contact);
                    break;
            }
        }

        // If we got no meaningful data, return null
        if (string.IsNullOrWhiteSpace(contact.FirstName) &&
            string.IsNullOrWhiteSpace(contact.LastName) &&
            string.IsNullOrWhiteSpace(contact.CompanyName) &&
            contact.PhoneNumbers.Count == 0 &&
            contact.EmailAddresses.Count == 0)
        {
            return null;
        }

        return contact;
    }

    private static string UnfoldLines(string text)
    {
        // RFC 6350: continuation lines start with a single space or tab
        return System.Text.RegularExpressions.Regex.Replace(
            text, @"\r?\n[ \t]", string.Empty);
    }

    private static (string Property, Dictionary<string, string> Parameters, string Value) ParseLine(string line)
    {
        // Handle group prefix (e.g., "item1.TEL;TYPE=...")
        var colonIndex = line.IndexOf(':');
        if (colonIndex < 0)
            return (line, new(), string.Empty);

        var propertyPart = line[..colonIndex];
        var value = line[(colonIndex + 1)..];

        // Remove group prefix
        var dotIndex = propertyPart.IndexOf('.');
        if (dotIndex >= 0)
            propertyPart = propertyPart[(dotIndex + 1)..];

        // Split property name from parameters
        var segments = propertyPart.Split(';');
        var property = segments[0];
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < segments.Length; i++)
        {
            var param = segments[i];
            var equalsIndex = param.IndexOf('=');
            if (equalsIndex >= 0)
            {
                var key = param[..equalsIndex];
                var val = param[(equalsIndex + 1)..];
                // Accumulate multiple values for the same key (e.g., type=HOME;type=VOICE)
                parameters[key] = parameters.TryGetValue(key, out var existingVal)
                    ? existingVal + "," + val
                    : val;
            }
            else
            {
                // vCard 2.1 style: TYPE directly as parameter (e.g., TEL;CELL:...)
                parameters["TYPE"] = parameters.TryGetValue("TYPE", out var existing)
                    ? existing + "," + param
                    : param;
            }
        }

        return (property, parameters, value);
    }

    private static void ParseStructuredName(string value, SharedContactData contact)
    {
        // N:LastName;FirstName;MiddleName;Prefix;Suffix
        var parts = value.Split(';');
        if (parts.Length > 0) contact.LastName = UnescapeValue(parts[0]);
        if (parts.Length > 1) contact.FirstName = UnescapeValue(parts[1]);
        if (parts.Length > 2) contact.MiddleName = UnescapeValue(parts[2]);

        // Clear empty strings
        if (string.IsNullOrWhiteSpace(contact.FirstName)) contact.FirstName = null;
        if (string.IsNullOrWhiteSpace(contact.MiddleName)) contact.MiddleName = null;
        if (string.IsNullOrWhiteSpace(contact.LastName)) contact.LastName = null;
    }

    private static void ParseFormattedName(string value, SharedContactData contact)
    {
        var name = UnescapeValue(value)?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            contact.FirstName = parts[0];
        }
        else if (parts.Length == 2)
        {
            contact.FirstName = parts[0];
            contact.LastName = parts[1];
        }
        else if (parts.Length >= 3)
        {
            contact.FirstName = parts[0];
            contact.MiddleName = string.Join(" ", parts[1..^1]);
            contact.LastName = parts[^1];
        }
    }

    private static void ParseBirthday(string value, SharedContactData contact)
    {
        // Formats: YYYY-MM-DD, YYYYMMDD, --MM-DD (no year), --MMDD
        value = value.Trim().Replace("-", "");

        if (value.Length == 8 && int.TryParse(value, out _))
        {
            contact.BirthYear = int.Parse(value[..4]);
            contact.BirthMonth = int.Parse(value[4..6]);
            contact.BirthDay = int.Parse(value[6..8]);
        }
        else if (value.Length == 4 && int.TryParse(value, out _))
        {
            // --MMDD (dashes already stripped)
            contact.BirthMonth = int.Parse(value[..2]);
            contact.BirthDay = int.Parse(value[2..4]);
        }
    }

    private static void ParsePhone(Dictionary<string, string> parameters, string value, SharedContactData contact)
    {
        var phone = UnescapeValue(value)?.Trim();
        if (string.IsNullOrWhiteSpace(phone))
            return;

        var tag = MapPhoneTag(parameters);
        contact.PhoneNumbers.Add(new SharedPhoneEntry
        {
            PhoneNumber = phone,
            Tag = tag
        });
    }

    private static void ParseEmail(Dictionary<string, string> parameters, string value, SharedContactData contact)
    {
        var email = UnescapeValue(value)?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return;

        var tag = MapEmailTag(parameters);
        contact.EmailAddresses.Add(new SharedEmailEntry
        {
            Email = email,
            Tag = tag
        });
    }

    private static void ParseAddress(Dictionary<string, string> parameters, string value, SharedContactData contact)
    {
        // ADR: POBox;Extended;Street;City;Region;PostalCode;Country
        var parts = value.Split(';');

        var street = parts.Length > 2 ? UnescapeValue(parts[2]) : null;
        var extended = parts.Length > 1 ? UnescapeValue(parts[1]) : null;
        var city = parts.Length > 3 ? UnescapeValue(parts[3]) : null;
        var region = parts.Length > 4 ? UnescapeValue(parts[4]) : null;
        var postal = parts.Length > 5 ? UnescapeValue(parts[5]) : null;
        var country = parts.Length > 6 ? UnescapeValue(parts[6]) : null;

        // Skip empty addresses
        if (string.IsNullOrWhiteSpace(street) && string.IsNullOrWhiteSpace(city) &&
            string.IsNullOrWhiteSpace(postal))
            return;

        var tag = MapAddressTag(parameters);
        contact.Addresses.Add(new SharedAddressEntry
        {
            AddressLine1 = street,
            AddressLine2 = extended,
            City = city,
            StateProvince = region,
            PostalCode = postal,
            Country = country,
            Tag = tag
        });
    }

    private static void ParsePhoto(Dictionary<string, string> parameters, string value, SharedContactData contact)
    {
        try
        {
            // PHOTO is typically base64-encoded: PHOTO;ENCODING=b;TYPE=JPEG:<base64data>
            // or in vCard 4.0: PHOTO;MEDIATYPE=image/jpeg:<base64data>
            var isBase64 = parameters.Any(p =>
                p.Key.Equals("ENCODING", StringComparison.OrdinalIgnoreCase) &&
                (p.Value.Equals("b", StringComparison.OrdinalIgnoreCase) ||
                 p.Value.Equals("BASE64", StringComparison.OrdinalIgnoreCase)));

            // vCard 4.0 uses data URI: data:image/jpeg;base64,<data>
            if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var commaIndex = value.IndexOf(',');
                if (commaIndex > 0)
                {
                    value = value[(commaIndex + 1)..];
                    isBase64 = true;
                }
            }

            if (!isBase64 && !parameters.ContainsKey("ENCODING"))
            {
                // Assume base64 if no encoding specified but value looks like base64
                isBase64 = value.Length > 100 && !value.Contains("://");
            }

            if (isBase64 && !string.IsNullOrWhiteSpace(value))
            {
                contact.ProfileImageData = Convert.FromBase64String(value.Trim());
            }
        }
        catch
        {
            // Ignore photo parsing errors
        }
    }

    private static int MapPhoneTag(Dictionary<string, string> parameters)
    {
        var typeValue = GetTypeValue(parameters);
        if (string.IsNullOrEmpty(typeValue))
            return 99; // Other

        return typeValue.ToUpperInvariant() switch
        {
            var t when t.Contains("CELL") || t.Contains("MOBILE") => 0,
            var t when t.Contains("HOME") => 1,
            var t when t.Contains("WORK") => 2,
            var t when t.Contains("FAX") => 3,
            _ => 99
        };
    }

    private static int MapEmailTag(Dictionary<string, string> parameters)
    {
        var typeValue = GetTypeValue(parameters);
        if (string.IsNullOrEmpty(typeValue))
            return 0; // Personal

        return typeValue.ToUpperInvariant() switch
        {
            var t when t.Contains("WORK") => 1,
            var t when t.Contains("SCHOOL") => 2,
            var t when t.Contains("HOME") || t.Contains("PERSONAL") => 0,
            _ => 0
        };
    }

    private static int MapAddressTag(Dictionary<string, string> parameters)
    {
        var typeValue = GetTypeValue(parameters);
        if (string.IsNullOrEmpty(typeValue))
            return 0; // Home

        return typeValue.ToUpperInvariant() switch
        {
            var t when t.Contains("HOME") => 0,
            var t when t.Contains("WORK") => 1,
            _ => 0
        };
    }

    private static string? GetTypeValue(Dictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("TYPE", out var type))
            return type.Replace("\"", ""); // Strip quotes from iOS vCards
        if (parameters.TryGetValue("type", out type))
            return type.Replace("\"", "");
        return null;
    }

    private static string? UnescapeValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return value
            .Replace("\\n", "\n")
            .Replace("\\N", "\n")
            .Replace("\\,", ",")
            .Replace("\\;", ";")
            .Replace("\\\\", "\\");
    }
}
