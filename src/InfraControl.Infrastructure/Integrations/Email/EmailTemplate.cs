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
            <body style="margin:0;padding:0;background:#f6f7f9;font-family:Inter,Segoe UI,Roboto,Helvetica,Arial,sans-serif;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f6f7f9;padding:32px 16px;">
            <tr><td align="center">
            <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e7e9ee;box-shadow:0 1px 2px rgba(15,23,42,0.02),0 8px 24px -16px rgba(15,23,42,0.4);">
            <tr><td style="background:linear-gradient(90deg,#101828 0%,#1f2937 100%);padding:24px 40px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="left">
            <span style="color:#ffffff;font-size:16px;font-weight:600;letter-spacing:0.2px;">InfraControl</span>
            </td></tr>
            </table>
            </td></tr>
            <tr><td style="padding:40px 40px 32px;">
            """);

        if (!string.IsNullOrWhiteSpace(greeting))
            html.Append("<p style=\"margin:0 0 12px;font-size:15px;color:#374151;\">")
                .Append(WebUtility.HtmlEncode(greeting))
                .Append("</p>\n");

            html.Append("<h1 style=\"margin:0 0 12px;font-size:22px;line-height:1.25;color:#0f172a;font-weight:600;\">")
                .Append(WebUtility.HtmlEncode(title))
                .Append("</h1>\n");

            html.Append("<p style=\"margin:0 0 24px;font-size:15px;color:#475569;line-height:1.7;\">")
                .Append(WebUtility.HtmlEncode(description))
                .Append("</p>\n");

        if (details is { Count: > 0 })
        {
            html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:0 0 24px;width:100%;border:1px solid #e7e9ee;border-radius:10px;overflow:hidden;\">\n");
            foreach (var (label, value) in details)
            {
                html.Append("<tr><td style=\"padding:12px 18px;border-bottom:1px solid #eef1f6;font-size:13px;color:#6b7280;width:38%;background:#f8f9fb;\">")
                    .Append(WebUtility.HtmlEncode(label))
                    .Append("</td><td style=\"padding:12px 18px;border-bottom:1px solid #eef1f6;font-size:14px;color:#1f2937;\">")
                    .Append(WebUtility.HtmlEncode(value))
                    .Append("</td></tr>\n");
            }
            html.Append("</table>\n");
        }

        if (cta is not null)
        {
            html.Append("<p style=\"margin:0 0 10px;\"><a href=\"")
                .Append(WebUtility.HtmlEncode(cta.Url))
                .Append("\" style=\"display:inline-block;padding:12px 28px;background:#0052cc;color:#ffffff;text-decoration:none;border-radius:999px;font-size:15px;font-weight:600;box-shadow:0 1px 2px rgba(0,82,204,0.25);\">")
                .Append(WebUtility.HtmlEncode(cta.Label))
                .Append("</a></p>\n");
            html.Append("<p style=\"margin:0 0 24px;font-size:12px;color:#9aa0b4;word-break:break-all;\">")
                .Append(WebUtility.HtmlEncode(cta.Url))
                .Append("</p>\n");
        }

        html.Append("""
            </td></tr>
            <tr><td style="padding:18px 40px;border-top:1px solid #eef1f6;background:#fbfbfd;">
            <p style="margin:0;font-size:12px;color:#8a90a2;">Este e-mail foi enviado automaticamente pelo InfraControl. Se não foi você quem solicitou, ignore esta mensagem.</p>
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
