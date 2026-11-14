from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs" / "week46-sustainability"
OUT.mkdir(parents=True, exist_ok=True)

repository = (ROOT / "src/ArticleService/Data/ArticleRepository.cs").read_text(encoding="utf-8")
webapp = (ROOT / "src/WebApp/Program.cs").read_text(encoding="utf-8")
observability = (ROOT / "src/Shared/Observability/ObservabilityExtensions.cs").read_text(encoding="utf-8")
notes = (ROOT / "docs/week45-sustainability.md").read_text(encoding="utf-8")

before_query = """SELECT id, title, content, source, scope, created_at_utc, updated_at_utc
FROM articles
ORDER BY created_at_utc DESC
LIMIT @limit;"""
after_query = """SELECT id, title, source, scope, created_at_utc
FROM articles
ORDER BY created_at_utc DESC
LIMIT @limit;"""
page_load = """const response = await fetch('/api/headlines');
const headlines = await response.json();"""
on_click = """button.addEventListener('click', () => showArticle(headline.id));"""
fetch_body = """const response = await fetch('/api/headlines/' + encodeURIComponent(id));
const article = await response.json();
document.getElementById('articleContent').textContent = article.content;"""
before_logging = """logger.LogInformation(
    "HTTP {Method} {Path} completed with {StatusCode} in {ElapsedMilliseconds} ms",
    context.Request.Method,
    context.Request.Path.Value,
    statusCode,
    stopwatch.ElapsedMilliseconds);"""
after_logging = """if (statusCode >= 400
    || !string.Equals(context.Request.Path.Value, "/health", StringComparison.OrdinalIgnoreCase))
{
    logger.LogInformation(
        "HTTP {Method} {Path} completed with {StatusCode} in {ElapsedMilliseconds} ms",
        context.Request.Method,
        context.Request.Path.Value,
        statusCode,
        stopwatch.ElapsedMilliseconds);
}"""

def check(source, block, name):
    lines = [line.strip() for line in source.splitlines()]
    expected = [line.strip() for line in block.splitlines()]
    cursor = 0
    for line in lines:
        if cursor < len(expected) and line == expected[cursor]:
            cursor += 1
    if cursor != len(expected):
        raise ValueError("Source changed; cannot show " + name)

for source, block, name in [
    (repository, before_query, "full article query"),
    (repository, after_query, "headline query"),
    (webapp, page_load, "headline list request"),
    (webapp, on_click, "headline click handler"),
    (webapp, fetch_body, "article content request"),
    (notes, before_logging, "previous request logging"),
    (observability, after_logging, "health check logging filter"),
    (observability, "HappyHeadlinesDiagnostics.HttpRequests.Add(1, tags);", "request metrics")
]:
    check(source, block, name)

FONT_DIR = Path("/usr/share/fonts/truetype/dejavu")
def font(name, size):
    file = FONT_DIR / name
    if file.exists():
        return ImageFont.truetype(str(file), size)
    return ImageFont.load_default()

title_font = font("DejaVuSans-Bold.ttf", 37)
small_font = font("DejaVuSans.ttf", 21)
caption_font = font("DejaVuSans.ttf", 23)
section_font = font("DejaVuSans-Bold.ttf", 23)
code_font = font("DejaVuSansMono.ttf", 18)
dark = "#182637"
muted = "#536578"
green = "#206956"
border = "#D8E0E8"

def card(draw, y, height, label, source, lines):
    draw.rounded_rectangle((66, y, 1534, y + height), radius=18, fill="#FFFFFF", outline=border, width=2)
    draw.text((92, y + 24), label, font=section_font, fill=dark)
    draw.text((92, y + 62), source, font=small_font, fill=muted)
    code_top = y + 100
    draw.rounded_rectangle((92, code_top, 1508, y + height - 9), radius=10, fill="#F3F6F9")
    yy = code_top + 13
    for line in lines:
        draw.text((117, yy), line, font=code_font, fill=dark)
        yy += 24
    if yy - 24 + 21 > y + height - 4:
        raise ValueError("Code overflow in " + label)

def make(filename, title, tactic, cards, footer):
    im = Image.new("RGB", (1600, 900), "#F7F9FB")
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, 1600, 145), fill=dark)
    d.text((67, 35), title, font=title_font, fill="#FFFFFF")
    d.text((68, 92), "GAF tactic: " + tactic, font=small_font, fill="#C9D7E3")
    for c in cards:
        card(d, *c)
    d.line((66, 824, 1534, 824), fill=border, width=2)
    d.text((68, 843), footer, font=caption_font, fill=green)
    file = OUT / filename
    im.save(file, "PNG", optimize=True)
    size = file.stat().st_size
    if size > 1_000_000:
        raise ValueError(filename + " exceeds the Moodle 1 MB limit")
    print(filename, size, "bytes")

make(
    "01 Network Package Size.png",
    "Smaller article responses",
    "Reduce Network Package Size",
    [
        (169, 270, "Full article list", "ArticleRepository.cs / GetAllAsync", before_query.splitlines()),
        (465, 284, "Headline list", "ArticleRepository.cs / GetHeadlinesAsync", after_query.splitlines()),
    ],
    "The headlines endpoint leaves out the full article content."
)

make(
    "02 Lazy Fetch.png",
    "Load article text when it is needed",
    "Defer Fetching / Lazy Fetch",
    [
        (169, 236, "When the page opens", "WebApp/Program.cs / loadHeadlines (selected lines)", page_load.splitlines()),
        (430, 354, "After a headline is selected", "WebApp/Program.cs / loadHeadlines and showArticle (selected lines)", [on_click, "", *fetch_body.splitlines()]),
    ],
    "Readers fetch full article text only after selecting a headline."
)

make(
    "03 Reduce Logging.png",
    "Skip successful health check logs",
    "Reduce Logging",
    [
        (165, 273, "Previous request logging", "docs/week45-sustainability.md", before_logging.splitlines()),
        (450, 363, "Current request logging", "ObservabilityExtensions.cs / UseHappyHeadlinesRequestLogging", after_logging.splitlines()),
    ],
    "Failed requests still log, and HTTP request metrics are kept."
)

(OUT / "Descriptions.txt").write_text(
    "01 Network Package Size.png\n"
    "GAF tactic: Reduce Network Package Size. We added a headlines query that returns "
    "only the fields needed for the article list. It leaves out the full article text, "
    "so smaller responses are sent to the reader.\n\n"
    "02 Lazy Fetch.png\n"
    "GAF tactic: Defer Fetching / Lazy Fetch. The page loads the headlines first. "
    "When a reader selects one, the WebApp requests the full article separately. "
    "Articles that are not opened do not have their content downloaded.\n\n"
    "03 Reduce Logging.png\n"
    "GAF tactic: Reduce Logging. Successful health checks no longer create an "
    "information log entry. Failed requests are still logged, and request metrics "
    "are still collected. This reduces unnecessary log traffic.\n\n"
    "The screenshots show selected source code, not measured electricity or CO2 savings.\n",
    encoding="utf-8"
)
