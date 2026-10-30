"""Build the publication snapshot without changing application source files."""

import argparse
import hashlib
import html
import json
from collections import Counter
from html.parser import HTMLParser
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
PROJECTS = ("MVC_App", "MVC_App.BlazorClient", "MVC_App.ApiClient", "MVC_App.PgSqlClient")
SKIP = {"bin", "obj", ".vs", ".git", "migrations", "identity"}
SOURCE_EXTENSIONS = {".cs", ".cshtml", ".razor", ".css", ".js", ".json", ".md"}
SUPPORT = ("MVC_App.sln", "compose.yaml", ".dockerignore", "observability/otel-collector.yaml",
           "observability/logstash/pipeline/otel-logs.conf", "scripts/test-telemetry.py")
ARTICLE_EXCLUDED_FILES = {
    "MVC_App.sln",
    ".dockerignore",
    "scripts/test-telemetry.py",
    "MVC_App/Pages/_ViewImports.cshtml",
    "MVC_App/Pages/_ViewStart.cshtml",
    "MVC_App/Pages/Error.cshtml",
    "MVC_App/Pages/Error.cshtml.cs",
    "MVC_App/Pages/Index.cshtml",
    "MVC_App/Pages/Index.cshtml.cs",
    "MVC_App/Pages/Privacy.cshtml",
    "MVC_App/Pages/Privacy.cshtml.cs",
    "MVC_App/Pages/Shared/_Layout.cshtml",
    "MVC_App/Pages/Shared/_Layout.cshtml.css",
    "MVC_App/Pages/Shared/_ValidationScriptsPartial.cshtml",
    "MVC_App/wwwroot/css/site.css",
    "MVC_App/wwwroot/js/site.js",
    "MVC_App/Properties/launchSettings.json",
    "MVC_App/Views/_ViewImports.cshtml",
    "MVC_App/Views/_ViewStart.cshtml",
    "MVC_App/ViewModels/RegisterViewModel.cs",
    "MVC_App/ViewModels/LoginViewModel.cs",
    "MVC_App/Views/Account/AccessDenied.cshtml",
    "MVC_App/Views/Account/Login.cshtml",
    "MVC_App/Views/Account/Register.cshtml",
    "MVC_App.BlazorClient/Components/_Imports.razor",
    "MVC_App.BlazorClient/Components/Account/Login.razor",
    "MVC_App.BlazorClient/Components/Account/Logout.razor",
    "MVC_App.BlazorClient/Components/Account/RedirectToLogin.razor",
    "MVC_App.BlazorClient/Components/App.razor",
    "MVC_App.BlazorClient/Components/Layout/MainLayout.razor",
    "MVC_App.BlazorClient/Components/Layout/MainLayout.razor.css",
    "MVC_App.BlazorClient/Components/Layout/ReconnectModal.razor",
    "MVC_App.BlazorClient/Components/Layout/ReconnectModal.razor.css",
    "MVC_App.BlazorClient/Components/Layout/ReconnectModal.razor.js",
    "MVC_App.BlazorClient/Components/Pages/Error.razor",
    "MVC_App.BlazorClient/Components/Pages/Home.razor",
    "MVC_App.BlazorClient/Components/Pages/NotFound.razor",
    "MVC_App.BlazorClient/Components/Routes.razor",
    "MVC_App.BlazorClient/Properties/launchSettings.json",
    "MVC_App.BlazorClient/wwwroot/app.css",
    "MVC_App.PgSqlClient/README.md",
}
SOURCE_DETAILS = {
    "MVC_App/Telemetry/TelemetryExtensions.cs": "scripts/article-telemetry-details.html",
}


def inventory():
    first_party, vendor, maps = [], [], []
    for project in PROJECTS:
        for path in sorted((ROOT / project).rglob("*")):
            if (not path.is_file()
                    or SKIP.intersection(part.lower() for part in path.relative_to(ROOT).parts)
                    or path.name.lower().endswith("modelsnapshot.cs")):
                continue
            relative = path.relative_to(ROOT).as_posix()
            if relative in ARTICLE_EXCLUDED_FILES:
                continue
            if "/wwwroot/lib/" in relative:
                if path.suffix == ".map":
                    maps.append(relative)
                elif path.suffix in {".css", ".js"} or path.name.startswith("LICENSE"):
                    vendor.append(relative)
            elif path.suffix in SOURCE_EXTENSIONS or path.name == "Dockerfile":
                first_party.append(relative)
    first_party.extend(path for path in SUPPORT if path not in ARTICLE_EXCLUDED_FILES)
    return first_party, vendor, maps


def publication_secrets():
    values = {}

    def add(value, label):
        if isinstance(value, str) and len(value) >= 4:
            values[value] = "<REDACTED_" + label + ">"

    def walk(value, trail=()):
        if isinstance(value, dict):
            for key, child in value.items():
                walk(child, trail + (key,))
        elif isinstance(value, list):
            for child in value:
                walk(child, trail)
        elif isinstance(value, str) and trail:
            key = trail[-1].lower()
            if key in {"password", "pwd", "username", "user id"}:
                add(value, key.upper().replace(" ", "_"))
            elif "Api" in trail and key in {"key", "keys"}:
                add(value, "API_KEY")
            elif trail[0] == "ConnectionStrings" or key == "connectionstring":
                add(value, "CONNECTION_STRING")
                for name, token in re.findall(r"(?:^|;)\s*([^=;]+)=([^;]+)", value):
                    if name.strip().lower() in {"password", "pwd", "user id", "uid", "instrumentationkey", "applicationid"}:
                        add(token.strip(), name.strip().upper().replace(" ", "_"))
            elif trail[0] == "Postgres" and key == "host":
                add(value, "DATABASE_HOST")

    for project in PROJECTS:
        for path in (ROOT / project).glob("appsettings*.json"):
            walk(json.loads(path.read_text(encoding="utf-8-sig")))
    return values


def sanitize(text, secrets):
    for value, placeholder in sorted(secrets.items(), key=lambda pair: -len(pair[0])):
        text = text.replace(value, placeholder)
    text = re.sub(r"[a-zA-Z0-9-]+\.database\.windows\.net", "example.database.windows.net", text)
    text = re.sub(r"[a-zA-Z0-9-]+\.postgres\.database\.azure\.com", "example.postgres.database.azure.com", text)
    return text


def anchor(path):
    return "file-" + re.sub(r"[^a-zA-Z0-9_-]", "-", path)


def source_section(path, explanation, secrets):
    source = (ROOT / path).read_text(encoding="utf-8-sig")
    published = sanitize(source, secrets)
    changes = source != published
    markup = (f'<section id="{anchor(path)}" data-source-path="{html.escape(path, quote=True)}">\n'
              f'<h3>{html.escape(path)}</h3>\n<p>{html.escape(explanation)}</p>\n'
              '<pre style="white-space:pre-wrap;overflow-wrap:anywhere"><code>'
              + html.escape(published) + '</code></pre>\n</section>\n')
    if path in SOURCE_DETAILS:
        markup += (ROOT / SOURCE_DETAILS[path]).read_text(encoding="utf-8-sig")
    record = {"path": path, "lines": len(source.splitlines()), "redacted": changes,
              "published_sha256": hashlib.sha256(published.encode("utf-8")).hexdigest()}
    return markup, record


class ArticleParser(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.stack = []
        self.ids = []
        self.sources = []
        self.forbidden = []
        self.image_placeholders = []
        self.current_source = None
        self.in_code = False
        self.source_text = {}

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag in {"html", "head", "body", "meta", "title", "a", "details", "summary", "script", "iframe", "object", "embed", "style", "link"}:
            self.forbidden.append(tag)
        if tag == "figure" and "data-pptx-slide" in attrs:
            self.image_placeholders.append(attrs["data-pptx-slide"])
        assert "hidden" not in attrs, "Hidden article content"
        assert not re.search(r"display\s*:\s*none|visibility\s*:\s*(?:hidden|collapse)",
                             attrs.get("style", ""), re.IGNORECASE), "Hidden article styling"
        if "id" in attrs:
            self.ids.append(attrs["id"])
        if "data-source-path" in attrs:
            self.current_source = attrs["data-source-path"]
            self.sources.append(self.current_source)
            self.source_text[self.current_source] = ""
        if tag == "code" and self.current_source:
            assert self.stack and self.stack[-1] == "pre", "Source must be displayed in a pre/code block"
            self.in_code = True
        if tag not in {"area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"}:
            self.stack.append(tag)

    def handle_decl(self, decl):
        raise AssertionError("Blogger fragment must not contain a document declaration")

    def handle_endtag(self, tag):
        if not self.stack or self.stack.pop() != tag:
            raise AssertionError("Mismatched HTML closing tag: " + tag)
        if tag == "code":
            self.in_code = False
        if tag == "section" and self.current_source:
            self.current_source = None

    def handle_data(self, data):
        if self.in_code and self.current_source:
            self.source_text[self.current_source] += data


def validate():
    first, vendor, maps = inventory()
    document = (ROOT / "article.html").read_text(encoding="utf-8")
    secrets = publication_secrets()
    parser = ArticleParser()
    parser.feed(document)
    parser.close()
    assert not parser.stack, "Unclosed HTML tags"
    assert not parser.forbidden, "Document wrappers, links, collapsible sections, stylesheets or active content in publication"
    assert len(parser.ids) == len(set(parser.ids)), "Duplicate HTML IDs"
    required_sections = {"kibana-data-view", "kibana-kql-examples", "kibana-dashboard-panels",
                         "run-prerequisites", "run-observability", "run-mvc", "run-blazor",
                         "run-diagnostics", "run-workload", "run-postgres", "run-verify-stop", "run-troubleshooting"}
    assert required_sections.issubset(parser.ids), "Missing application startup or Kibana dashboard instructions"
    assert Counter(parser.image_placeholders) == Counter(str(slide) for slide in range(1, 9)), "Expected one visible image placeholder per PowerPoint slide"
    assert Counter(parser.sources) == Counter(first), "First-party source coverage mismatch"
    assert not ARTICLE_EXCLUDED_FILES.intersection(parser.sources), "User-excluded file listing remains in the article"
    assert not any("identity" in path.lower().split("/") or path.lower().endswith(".csproj")
                   for path in parser.sources), "Identity folder files and project files must not be published"
    assert not any("migrations" in path.lower().split("/") or path.lower().endswith("modelsnapshot.cs")
                   for path in parser.sources), "Generated migrations or model snapshots must not be published"
    for path in first:
        expected = sanitize((ROOT / path).read_text(encoding="utf-8-sig"), secrets)
        assert parser.source_text[path] == expected, "Incomplete or altered code listing: " + path
    for path, details_path in SOURCE_DETAILS.items():
        source_start = document.index(f'data-source-path="{path}"')
        source_end = document.index('</section>', source_start) + len('</section>')
        details = sanitize((ROOT / details_path).read_text(encoding="utf-8-sig"), secrets).strip()
        assert document[source_end:].lstrip().startswith(details), "Detailed explanation must follow its complete source listing"
        assert details.count('<li>') >= 20, "Expected detailed, point-by-point telemetry explanation"
    decoded = html.unescape(document)
    for value in secrets:
        assert value not in decoded, "Sensitive value remains in published output"
    assert document.strip().startswith("<article>") and document.strip().endswith("</article>"), "Expected a paste-ready article fragment"
    print("PASS: Blogger HTML fragment without document wrappers; eight PowerPoint image placeholders")
    print("PASS: application startup, diagnostic workload, Kibana KQL and dashboard sections present")
    print(f"PASS: {len(first)} first-party/support files; complete inline source verified")
    print(f"PASS: {len(ARTICLE_EXCLUDED_FILES)} requested files excluded; detailed telemetry explanation follows its code")
    print("PASS: generated migration/designer files and model snapshots excluded from the article")
    print("PASS: Identity folder files and .csproj listings excluded from the article")
    print(f"PASS: {len(vendor)} bundled vendor/license files and {len(maps)} generated source maps excluded")
    print("PASS: no hyperlinks, collapsible sections, hidden content or active scripts; known secret values absent")
    print(f"article.html: {len(document.encode('utf-8')):,} bytes")


def build():
    notes = json.loads((ROOT / "scripts/article-file-notes.json").read_text(encoding="utf-8-sig"))
    first, vendor, maps = inventory()
    assert not set(first) - set(notes), "Missing file-level explanation"
    secrets = publication_secrets()
    parts = ['<article>\n',
             (ROOT / "scripts/article-introduction.html").read_text(encoding="utf-8-sig"),
             '<section id="source-catalog">\n<h2>A closer look at the code</h2>\n'
             '<p>With the request flow and local setup in mind, we can look at how the pieces are implemented. '
             'Start with the MVC host and telemetry registration, then follow the clients and supporting configuration. '
             'The notes beside each file call out the behavior worth noticing as you read.</p>\n']
    introductions = {
        "MVC_App": "The backend is a useful starting point because it receives the API calls, applies the business rules and talks to SQL Server. Notice how much of the logging happens around the controllers rather than inside each action.",
        "MVC_App.BlazorClient": "On the Blazor side, follow a component action into the typed API client. The outgoing handler and error boundary fill the gaps left by ordinary HTTP request logging in an interactive-server application.",
        "MVC_App.ApiClient": "The API console gives us repeatable traffic to investigate. Its local reports describe the calls it made; the backend supplies the corresponding logs, traces and metrics in ELK.",
        "MVC_App.PgSqlClient": "The PostgreSQL console takes a different approach: it adds business activities and measurements around repository operations, then exports to Azure Monitor. Keep this path separate from the local ELK setup as you read.",
        "Solution and observability support": "These files connect the application exporters to storage and Kibana. Read the Collector and Logstash configuration together to see why logs take a different route from traces and metrics."
    }
    records = []
    for project in (*PROJECTS, "Solution and observability support"):
        entries = [path for path in first if (path.startswith(project + "/") if project in PROJECTS else path in SUPPORT)]
        parts.append(f'<h3>{html.escape(project)}</h3>\n<p>{html.escape(introductions[project])}</p>\n')
        for path in entries:
            markup, record = source_section(path, notes[path], secrets)
            parts.append(markup)
            records.append(record)
    parts.append('</section>\n<section id="conclusion">\n'
                 '<h2>Conclusion</h2><p>A slow page or a failed API call is much easier to investigate when you can follow it beyond '
                 'the response status. In this solution, the request logs describe the outcome, the traces connect HTTP and SQL work, '
                 'and the metrics show whether the behavior is isolated or recurring.</p>\n'
                 '<p>Start with one successful request and one deliberate failure. Find their completion logs in Kibana, '
                 'inspect the status and error fields, and use the trace ID to follow the related events. Once that path makes sense, '
                 'the workload clients and dashboard panels become useful ways to explore larger patterns—not just ways to produce more data.</p>\n'
                 '</section>\n</article>\n')
    document = sanitize("".join(parts), secrets)
    (ROOT / "article.html").write_text(document, encoding="utf-8", newline="\n")
    manifest = {"first_party_and_support_count": len(first), "vendor_and_license_count": 0,
                "format": "Blogger HTML fragment without document wrappers",
                "pptx_image_placeholder_slides": list(range(1, 9)),
                "source_display": "fully visible inline pre/code blocks; no hyperlinks or collapsible sections",
                "excluded_requested_files": sorted(ARTICLE_EXCLUDED_FILES),
                "post_source_explanations": SOURCE_DETAILS,
                "excluded_vendor_and_license_files": vendor, "excluded_generated_source_maps": maps, "files": records,
                "excluded": ["Identity folder files", ".csproj project files", "generated migrations and migration designers", "EF Core model snapshots", "bundled third-party library distributions", "generated source maps", "bin/obj", "IDE/private user state", "binary favicon", "runtime telemetry data", "reference decks and tutorials", "upgrade history", "publishing helper tools"]}
    (ROOT / "docs/article-source-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    validate()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--validate", action="store_true", help="Validate the existing article without regenerating it")
    args = parser.parse_args()
    validate() if args.validate else build()
