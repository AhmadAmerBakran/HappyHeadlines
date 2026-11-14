import fs from 'node:fs/promises';
import path from 'node:path';
import { chromium } from 'playwright';

const root = process.cwd();
const output = path.join(root, 'docs/week46-sustainability-editor');
await fs.mkdir(output, { recursive: true });

const checks = [
  { source: 'src/ArticleService/Data/ArticleRepository.cs',
    required: ['GetAllAsync(', 'GetHeadlinesAsync(', 'SELECT id, title, source, scope, created_at_utc'],
    line: 79, filename: '01 Network Package Size.png' },
  { source: 'src/WebApp/Program.cs',
    required: ["fetch('/api/headlines')", "showArticle(headline.id)", "fetch('/api/headlines/' + encodeURIComponent(id))"],
    line: 88, filename: '02 Lazy Fetch.png' },
  { source: 'src/Shared/Observability/ObservabilityExtensions.cs',
    required: ['if (statusCode >= 400', '"/health"', 'HappyHeadlinesDiagnostics.HttpRequests.Add(1, tags)'],
    line: 103, filename: '03 Reduce Logging.png' }
];
for (const item of checks) {
  const text = await fs.readFile(path.join(root, item.source), 'utf8');
  for (const expected of item.required) {
    if (!text.includes(expected)) throw new Error('Missing real source code: ' + expected);
  }
}

const host = 'http://127.0.0.1:8443';
const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] });
const context = await browser.newContext({ viewport: {width: 1600, height: 900}, deviceScaleFactor: 1 });
const page = await context.newPage();
async function openFile(item) {
  await page.keyboard.press('Control+p');
  await page.keyboard.type(item.source, {delay: 8});
  await page.keyboard.press('Enter');
  await page.waitForTimeout(2000);
  await page.keyboard.press('Control+g');
  await page.keyboard.type(String(item.line));
  await page.keyboard.press('Enter');
  await page.waitForTimeout(1200);
  const visible = await page.locator('body').innerText();
  const name = path.basename(item.source);
  if (!visible.includes(name)) {
    throw new Error('The editor does not show ' + name + ': ' + visible.slice(0, 420));
  }
  const editorText = await page.locator('.view-lines').allInnerTexts();
  if (editorText.length === 0) throw new Error('The editor did not render source lines');
  const text = editorText.join('\n');
  if (item.source.includes('ArticleRepository') && !text.includes('GetHeadlinesAsync')) {
    throw new Error('The headline query is not visible in the screenshot');
  }
  if (item.source.includes('ObservabilityExtensions') && !text.includes('"/health"')) {
    throw new Error('The health logging condition is not visible');
  }
  if (item.source.includes('WebApp') && !text.includes("fetch('/api/headlines')")) {
    throw new Error('Headline loading is not visible');
  }
  const dest = path.join(output, item.filename);
  await page.screenshot({ path: dest, fullPage: false, animations: 'disabled' });
  const size = (await fs.stat(dest)).size;
  if (size > 1000000) {
    throw new Error(item.filename + ' is larger than 1 MB (' + size + ')');
  }
  console.log('Captured VS Code editor screenshot: ' + dest + ' (' + size + ' bytes)');
}

try {
  let available = false;
  for (let n = 0; n < 30; n++) {
    try {
      const response = await fetch(host);
      if (response.ok) { available = true; break; }
    } catch {}
    await new Promise(r => setTimeout(r, 1500));
  }
  if (!available) throw new Error('VS Code editor did not become available');
  await page.goto(host + '/?folder=/home/coder/project', {waitUntil: 'domcontentloaded', timeout: 60000});
  await page.waitForTimeout(10000);
  const trustButton = page.getByRole('button', {name: /yes, i trust the authors|trust/i});
  if (await trustButton.count()) await trustButton.first().click();
  await page.waitForTimeout(1700);
  for (const item of checks) {
    await openFile(item);
  }
  await fs.writeFile(path.join(output, 'Descriptions.txt'), [
    '01 Network Package Size.png',
    'GAF tactic: Reduce Network Package Size. This is the article repository in VS Code.',
    'The headline query selects only the fields needed for the list and leaves out the article body.',
    '',
    '02 Lazy Fetch.png',
    'GAF tactic: Defer Fetching. This is the headlines page script.',
    'The page first loads a list of headlines. It requests the full article only when a title is selected.',
    '',
    '03 Reduce Logging.png',
    'GAF tactic: Reduce Logging. This is the shared HTTP request logging middleware.',
    'Successful health checks are skipped in the information logs, while failed requests are still recorded.',
    '',
    'The images are direct screenshots of the source files opened in a VS Code editor on GitHub Actions.',
    'They are not screenshots of performance measurements.'
  ].join('\n') + '\n');
} catch (error) {
  await page.screenshot({path: path.join(output, 'debug.png'), fullPage: false}).catch(() => {});
  throw error;
} finally {
  await browser.close();
}
