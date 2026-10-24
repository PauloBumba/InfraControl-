using System.Net;
using System.Text;

namespace InfraControl.Infrastructure.Integrations.Email;

/// <summary>
/// F41-T002: conteúdo dos e-mails transacionais — layout HTML único
/// (600px, coluna simples, 1 CTA, rodapé fixo) + plain-text equivalente
/// (deliverability). Cada ação só fornece os blocos (saudação, título,
/// descrição, CTA opcional, detalhes opcionais); feature nova amanhã
/// reusa a factory sem template novo. Nunca envia senha — só link.
/// </summary>
public static class EmailTemplate
{
    public sealed record Cta(string Label, string Url);

    public static (string Html, string PlainText) Build(
        string title,
        string description,
        Cta? cta = null,
        string? greeting = null,
        IReadOnlyList<(string Label, string Value)>? details = null)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n");
        html.Append("<html lang=\"pt-BR\">\n");
        html.Append("<head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n");
        html.Append("<title>").Append(WebUtility.HtmlEncode(title)).Append("</title></head>\n");
        html.Append("""
            <body style="margin:0;padding:0;background:#f4f5f7;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f4f5f7;padding:24px 0;">
            <tr><td align="center">
            <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;border:1px solid #e5e7eb;">
            <tr><td style="background:#111827;padding:20px 32px;">
            <span style="color:#ffffff;font-size:18px;font-weight:700;">InfraControl</span>
            </td></tr>
            <tr><td style="padding:32px;">
            """);

        if (!string.IsNullOrWhiteSpace(greeting))
            html.Append("<p style=\"margin:0 0 12px;font-size:15px;color:#374151;\">")
                .Append(WebUtility.HtmlEncode(greeting))
                .Append("</p>\n");

        html.Append("<h1 style=\"margin:0 0 12px;font-size:20px;color:#111827;\">")
            .Append(WebUtility.HtmlEncode(title))
            .Append("</h1>\n");

        html.Append("<p style=\"margin:0 0 20px;font-size:15px;color:#4b5563;line-height:1.5;\">")
            .Append(WebUtility.HtmlEncode(description))
            .Append("</p>\n");

        if (details is { Count: > 0 })
        {
            html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:0 0 20px;width:100%;border:1px solid #e5e7eb;border-radius:6px;\">\n");
            foreach (var (label, value) in details)
            {
                html.Append("<tr><td style=\"padding:10px 16px;border-bottom:1px solid #e5e7eb;font-size:13px;color:#6b7280;width:40%;\">")
                    .Append(WebUtility.HtmlEncode(label))
                    .Append("</td><td style=\"padding:10px 16px;border-bottom:1px solid #e5e7eb;font-size:13px;color:#111827;\">")
                    .Append(WebUtility.HtmlEncode(value))
                    .Append("</td></tr>\n");
            }
            html.Append("</table>\n");
        }

        if (cta is not null)
        {
            html.Append("<p style=\"margin:0 0 8px;\"><a href=\"")
                .Append(WebUtility.HtmlEncode(cta.Url))
                .Append("\" style=\"display:inline-block;padding:12px 24px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:6px;font-size:15px;font-weight:600;\">")
                .Append(WebUtility.HtmlEncode(cta.Label))
                .Append("</a></p>\n");
            html.Append("<p style=\"margin:0 0 20px;font-size:12px;color:#9ca3af;word-break:break-all;\">")
                .Append(WebUtility.HtmlEncode(cta.Url))
                .Append("</p>\n");
        }

        html.Append("""
            </td></tr>
            <tr><td style="padding:16px 32px;border-top:1px solid #e5e7eb;">
            <p style="margin:0;font-size:12px;color:#9ca3af;">Mensagem automática do InfraControl — não responda.</p>
            </td></tr>
            </table>
            </td></tr>
            </table>
            </body></html>
            """);

        var plain = new StringBuilder();
        plain.Append(title).Append('\n').Append(new string('=', title.Length)).Append("\n\n");
        if (!string.IsNullOrWhiteSpace(greeting))
            plain.Append(greeting).Append("\n\n");
        plain.Append(description).Append("\n\n");
        if (details is { Count: > 0 })
        {
            foreach (var (label, value) in details)
                plain.Append(label).Append(": ").Append(value).Append('\n');
            plain.Append('\n');
        }
        if (cta is not null)
        {
            plain.Append(cta.Label).Append(": ").Append(cta.Url).Append("\n\n");
        }
        plain.Append("--\nMensagem automática do InfraControl.");

        return (html.ToString(), plain.ToString());
    }
}
