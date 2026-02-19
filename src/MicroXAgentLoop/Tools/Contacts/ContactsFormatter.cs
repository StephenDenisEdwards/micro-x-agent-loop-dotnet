using Google.Apis.PeopleService.v1.Data;

namespace MicroXAgentLoop.Tools.Contacts;

public static class ContactsFormatter
{
    public static string FormatContactSummary(Person person)
    {
        var name = person.Names is { Count: > 0 }
            ? person.Names[0].DisplayName ?? "(no name)"
            : "(no name)";

        var resourceName = person.ResourceName ?? "";

        var lines = new List<string>
        {
            $"ResourceName: {resourceName}",
            $"  Name: {name}",
        };

        if (person.EmailAddresses is { Count: > 0 })
            lines.Add($"  Email: {person.EmailAddresses[0].Value ?? ""}");

        if (person.PhoneNumbers is { Count: > 0 })
            lines.Add($"  Phone: {person.PhoneNumbers[0].Value ?? ""}");

        return string.Join("\n", lines);
    }

    public static string FormatContactDetail(Person person)
    {
        var name = person.Names is { Count: > 0 }
            ? person.Names[0].DisplayName ?? "(no name)"
            : "(no name)";

        var resourceName = person.ResourceName ?? "";
        var etag = person.ETag ?? "";

        var lines = new List<string>
        {
            $"ResourceName: {resourceName}",
            $"Name: {name}",
            $"Etag: {etag}",
        };

        if (person.EmailAddresses is { Count: > 0 })
        {
            foreach (var e in person.EmailAddresses)
            {
                var label = e.Type ?? "other";
                lines.Add($"Email ({label}): {e.Value ?? ""}");
            }
        }

        if (person.PhoneNumbers is { Count: > 0 })
        {
            foreach (var p in person.PhoneNumbers)
            {
                var label = p.Type ?? "other";
                lines.Add($"Phone ({label}): {p.Value ?? ""}");
            }
        }

        if (person.Addresses is { Count: > 0 })
        {
            foreach (var a in person.Addresses)
            {
                var label = a.Type ?? "other";
                lines.Add($"Address ({label}): {a.FormattedValue ?? ""}");
            }
        }

        if (person.Organizations is { Count: > 0 })
        {
            foreach (var o in person.Organizations)
            {
                var title = o.Title ?? "";
                var orgName = o.Name ?? "";
                lines.Add($"Organization: {orgName}" + (string.IsNullOrEmpty(title) ? "" : $" ({title})"));
            }
        }

        if (person.Biographies is { Count: > 0 })
            lines.Add($"Biography: {person.Biographies[0].Value ?? ""}");

        return string.Join("\n", lines);
    }
}
