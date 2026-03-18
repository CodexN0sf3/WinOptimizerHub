using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;

namespace WinOptimizerHub.Services
{
    public class ExportService
    {
        // ── Public entry points ───────────────────────────────────────────

        public void ExportJunk(IEnumerable<CleanableFolder> folders, string filePath)
        {
            if (Path.GetExtension(filePath).Equals(".html", StringComparison.OrdinalIgnoreCase))
                File.WriteAllText(filePath, BuildJunkHtml(folders), Encoding.UTF8);
            else
                File.WriteAllText(filePath, BuildJunkCsv(folders), Encoding.UTF8);
        }

        public void ExportRegistry(IEnumerable<RegistryIssue> issues, string filePath)
        {
            if (Path.GetExtension(filePath).Equals(".html", StringComparison.OrdinalIgnoreCase))
                File.WriteAllText(filePath, BuildRegistryHtml(issues), Encoding.UTF8);
            else
                File.WriteAllText(filePath, BuildRegistryCsv(issues), Encoding.UTF8);
        }

        public void ExportPrivacy(IEnumerable<PrivacyCleanerService.PrivacyItem> items, string filePath)
        {
            if (Path.GetExtension(filePath).Equals(".html", StringComparison.OrdinalIgnoreCase))
                File.WriteAllText(filePath, BuildPrivacyHtml(items), Encoding.UTF8);
            else
                File.WriteAllText(filePath, BuildPrivacyCsv(items), Encoding.UTF8);
        }

        // ── CSV builders ─────────────────────────────────────────────────

        private static string BuildJunkCsv(IEnumerable<CleanableFolder> folders)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Category,Name,Path,Files,Size (bytes),Size,Selected");
            foreach (var f in folders)
                sb.AppendLine($"{Csv(f.Category)},{Csv(f.Name)},{Csv(f.Path)},{f.FileCount},{f.Size},{Csv(f.SizeDisplay)},{f.IsSelected}");
            return sb.ToString();
        }

        private static string BuildRegistryCsv(IEnumerable<RegistryIssue> issues)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Issue Type,Key Path,Value Name,Description,Safe,Selected");
            foreach (var i in issues)
                sb.AppendLine($"{Csv(i.IssueType)},{Csv(i.KeyPath)},{Csv(i.ValueName)},{Csv(i.Description)},{i.IsSafe},{i.IsSelected}");
            return sb.ToString();
        }

        private static string BuildPrivacyCsv(IEnumerable<PrivacyCleanerService.PrivacyItem> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Category,Name,Description,Estimated Size (bytes),Selected");
            foreach (var p in items)
                sb.AppendLine($"{Csv(p.Category)},{Csv(p.Name)},{Csv(p.Description)},{p.EstimatedSize},{p.IsSelected}");
            return sb.ToString();
        }

        // ── HTML builders ─────────────────────────────────────────────────

        private static string BuildJunkHtml(IEnumerable<CleanableFolder> folders)
        {
            var list = folders.ToList();
            long totalBytes = list.Sum(f => f.Size);
            int totalFiles = list.Sum(f => f.FileCount);

            var rows = new StringBuilder();
            foreach (var f in list)
            {
                rows.AppendLine($@"<tr>
                    <td><span class='badge'>{H(f.Category)}</span></td>
                    <td>{H(f.Name)}</td>
                    <td class='mono'>{H(f.Path)}</td>
                    <td class='num'>{f.FileCount:N0}</td>
                    <td class='num'>{H(f.SizeDisplay)}</td>
                    <td>{(f.IsSelected ? "<span class='yes'>✓ Yes</span>" : "<span class='no'>✗ No</span>")}</td>
                </tr>");
            }

            string summary = $"{list.Count} categories &nbsp;·&nbsp; {totalFiles:N0} files &nbsp;·&nbsp; {FormatHelper.FormatSize(totalBytes)} total";
            return WrapHtml("Junk Cleaner Results", summary, $@"
                <table>
                    <thead><tr>
                        <th>Category</th><th>Name</th><th>Path</th>
                        <th>Files</th><th>Size</th><th>Selected</th>
                    </tr></thead>
                    <tbody>{rows}</tbody>
                </table>");
        }

        private static string BuildRegistryHtml(IEnumerable<RegistryIssue> issues)
        {
            var list = issues.ToList();
            int safeCount = list.Count(i => i.IsSafe);

            var rows = new StringBuilder();
            foreach (var i in list)
            {
                rows.AppendLine($@"<tr>
                    <td><span class='badge'>{H(i.IssueType)}</span></td>
                    <td class='mono'>{H(i.KeyPath)}</td>
                    <td class='mono'>{H(i.ValueName)}</td>
                    <td>{H(i.Description)}</td>
                    <td>{(i.IsSafe ? "<span class='yes'>✓ Safe</span>" : "<span class='warn'>⚠ Review</span>")}</td>
                    <td>{(i.IsSelected ? "<span class='yes'>✓ Yes</span>" : "<span class='no'>✗ No</span>")}</td>
                </tr>");
            }

            string summary = $"{list.Count} issues found &nbsp;·&nbsp; {safeCount} safe &nbsp;·&nbsp; {list.Count - safeCount} need review";
            return WrapHtml("Registry Cleaner Results", summary, $@"
                <table>
                    <thead><tr>
                        <th>Type</th><th>Key Path</th><th>Value</th>
                        <th>Description</th><th>Safety</th><th>Selected</th>
                    </tr></thead>
                    <tbody>{rows}</tbody>
                </table>");
        }

        private static string BuildPrivacyHtml(IEnumerable<PrivacyCleanerService.PrivacyItem> items)
        {
            var list = items.ToList();
            long totalBytes = list.Sum(p => p.EstimatedSize);

            var rows = new StringBuilder();
            foreach (var p in list)
            {
                rows.AppendLine($@"<tr>
                    <td><span class='badge'>{H(p.Category)}</span></td>
                    <td>{H(p.Name)}</td>
                    <td>{H(p.Description)}</td>
                    <td class='num'>{FormatHelper.FormatSize(p.EstimatedSize)}</td>
                    <td>{(p.IsSelected ? "<span class='yes'>✓ Yes</span>" : "<span class='no'>✗ No</span>")}</td>
                </tr>");
            }

            string summary = $"{list.Count} items &nbsp;·&nbsp; {FormatHelper.FormatSize(totalBytes)} estimated";
            return WrapHtml("Privacy Cleaner Results", summary, $@"
                <table>
                    <thead><tr>
                        <th>Category</th><th>Name</th><th>Description</th>
                        <th>Est. Size</th><th>Selected</th>
                    </tr></thead>
                    <tbody>{rows}</tbody>
                </table>");
        }

        // ── HTML shell ────────────────────────────────────────────────────

        private static string WrapHtml(string title, string summary, string body)
        {
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8""/>
<meta name=""viewport"" content=""width=device-width, initial-scale=1""/>
<title>{H(title)} — WinOptimizerHub</title>
<style>
  :root {{
    --bg: #0f1117; --bg2: #1a1d27; --border: #2a2d3a;
    --text: #e2e8f0; --muted: #8892a4;
    --accent: #6366f1; --yes: #22c55e; --warn: #f59e0b; --no: #ef4444;
  }}
  * {{ box-sizing: border-box; margin: 0; padding: 0; }}
  body {{ background: var(--bg); color: var(--text); font-family: 'Segoe UI', system-ui, sans-serif; font-size: 14px; padding: 32px; }}
  header {{ margin-bottom: 28px; }}
  header h1 {{ font-size: 22px; font-weight: 700; color: var(--text); display: flex; align-items: center; gap: 10px; }}
  header h1::before {{ content: ''; display: inline-block; width: 4px; height: 22px; background: var(--accent); border-radius: 2px; }}
  .meta {{ color: var(--muted); font-size: 12px; margin-top: 6px; }}
  .summary {{ background: var(--bg2); border: 1px solid var(--border); border-radius: 8px;
              padding: 12px 18px; margin-bottom: 20px; color: var(--muted); font-size: 13px; }}
  table {{ width: 100%; border-collapse: collapse; background: var(--bg2);
           border: 1px solid var(--border); border-radius: 8px; overflow: hidden; }}
  thead tr {{ background: #20243a; }}
  th {{ padding: 10px 14px; text-align: left; font-size: 11px; font-weight: 600;
        text-transform: uppercase; letter-spacing: .05em; color: var(--muted); border-bottom: 1px solid var(--border); }}
  td {{ padding: 9px 14px; border-bottom: 1px solid var(--border); vertical-align: top; }}
  tr:last-child td {{ border-bottom: none; }}
  tr:hover td {{ background: #1e2232; }}
  .mono {{ font-family: 'Cascadia Code', 'Consolas', monospace; font-size: 12px; color: var(--muted); word-break: break-all; }}
  .num {{ text-align: right; font-variant-numeric: tabular-nums; }}
  .badge {{ background: #2a2d3a; color: var(--accent); border-radius: 4px; padding: 2px 8px; font-size: 11px; font-weight: 600; white-space: nowrap; }}
  .yes {{ color: var(--yes); font-weight: 600; }}
  .warn {{ color: var(--warn); font-weight: 600; }}
  .no {{ color: var(--no); }}
  footer {{ margin-top: 24px; color: var(--muted); font-size: 11px; text-align: center; }}
</style>
</head>
<body>
<header>
  <h1>{H(title)}</h1>
  <div class=""meta"">Generated by WinOptimizerHub &nbsp;·&nbsp; {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>
</header>
<div class=""summary"">{summary}</div>
{body}
<footer>WinOptimizerHub — System Optimization Suite</footer>
</body>
</html>";
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static string Csv(string value)
        {
            if (value == null) return string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private static string H(string value)
        {
            if (value == null) return string.Empty;
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}
