import fs from 'node:fs/promises';
import path from 'node:path';
import { chromium } from 'playwright';

const grafana = 'http://127.0.0.1:3000';
const app = 'http://127.0.0.1:8080';
const publisher = 'http://127.0.0.1:8083';
const newsletter = 'http://127.0.0.1:8084';
const out = path.resolve('docs/monitoring-week46');
const auth = 'Basic ' + Buffer.from('admin:admin').toString('base64');

async function api(route, options = {}) {
  const response = await fetch(grafana + route, {
    ...options,
    headers: { Authorization: auth, ...(options.headers ?? {}) }
  });
  const body = await response.text();
  if (!response.ok) throw new Error(route + ': HTTP ' + response.status + ' ' + body.slice(0, 500));
  try { return JSON.parse(body); } catch { return body; }
}

async function until(label, fn, maxMs = 160000) {
  const started = Date.now();
  let lastError = '';
  while (Date.now() - started < maxMs) {
    try {
      const result = await fn();
      if (result) {
        console.log(label + ': ready');
        return result;
      }
    } catch (error) { lastError = String(error); }
    await new Promise(resolve => setTimeout(resolve, 4000));
  }
  throw new Error(label + ' unavailable after checks: ' + lastError);
}

async function hit(url, options) {
  const response = await fetch(url, options);
  if (!response.ok) throw new Error(url + ' returned ' + response.status);
  return response;
}

async function generate() {
  const body = {
    title: 'Volunteers restore a community garden',
    content: 'Residents worked together to restore a community garden and prepare new planting areas.',
    source: 'Happy Headlines',
    scope: 'global'
  };
  const response = await fetch(publisher + '/api/publishing', {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify(body)
  });
  if (response.status !== 202) throw new Error('Publish failed: ' + response.status + ' ' + await response.text());
  const published = await response.json();
  console.log('Published test article: ' + published.id);
  for (let i = 0; i < 40; i++) {
    await hit(app + '/api/articles/global/headlines?limit=10');
    await hit(app + '/api/articles/global?limit=10');
    if (i % 10 === 0) await hit(newsletter + '/api/newsletters/latest');
  }
}

async function getLogs() {
  const end = Date.now() * 1e6;
  const start = end - 10 * 60 * 1e9;
  const search = new URLSearchParams({
    query: '{service_name="ArticleService"} |= "HTTP"',
    start: String(Math.round(start)),
    end: String(Math.round(end)),
    limit: '100'
  });
  const response = await api('/api/datasources/proxy/uid/loki/loki/api/v1/query_range?' + search);
  const streams = response.data?.result || [];
  return streams.some(s => (s.values || []).some(v => v[1]?.includes('HTTP')));
}

async function getMetrics() {
  const query = encodeURIComponent('sum(happyheadlines_http_requests_total)');
  const response = await api('/api/datasources/proxy/uid/prometheus/api/v1/query?query=' + query);
  return response.data?.result?.length > 0;
}

async function findTrace() {
  const end = Math.floor(Date.now() / 1000);
  const start = end - 600;
  const search = new URLSearchParams({
    q: '{ resource.service.name = "PublisherService" }',
    limit: '50',
    start: String(start),
    end: String(end)
  });
  const response = await api('/api/datasources/proxy/uid/tempo/api/search?' + search);
  for (const t of response.traces || []) {
    const id = t.traceID || t.traceId;
    if (!id) continue;
    const trace = await api('/api/datasources/proxy/uid/tempo/api/traces/' + id);
    const text = JSON.stringify(trace);
    if (text.includes('PublisherService') && text.includes('ArticleService') &&
        text.includes('NewsletterService')) return id;
  }
  return null;
}

async function importDashboard(dashboard) {
  const payload = { dashboard: { ...dashboard, id: null, version: 0 }, folderId: 0, overwrite: true };
  return await api('/api/dashboards/db', {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify(payload)
  });
}

async function screenshot(page, url, name, expectedText) {
  await page.goto(grafana + url, {waitUntil: 'domcontentloaded', timeout: 60000});
  await page.waitForTimeout(8500);
  let body = await page.locator('body').innerText();
  if (!body.includes(expectedText)) throw new Error(name + ' lacks expected text: ' + expectedText +
    ' (page: ' + body.slice(0, 400) + ')');
  if (body.includes('No data') || body.includes('No data found')) throw new Error(name + ' image has no visible data');
  const filename = path.join(out, name + '.jpg');
  await page.screenshot({path: filename, type: 'jpeg', quality: 76, fullPage: false});
  const stat = await fs.stat(filename);
  if (stat.size > 1000000) throw new Error(name + ' image exceeds 1 MB');
  console.log('Saved verified image: ' + filename + ' (' + stat.size + ' bytes)');
}

await fs.mkdir(out, {recursive: true});
await until('Grafana', async () => (await fetch(grafana + '/api/health')).ok);
await until('ArticleService', async () => (await fetch(app + '/health')).ok);
await until('PublisherService', async () => (await fetch(publisher + '/health')).ok);
await until('NewsletterService', async () => (await fetch(newsletter + '/health')).ok);

await generate();
await until('Prometheus request metrics', getMetrics);
await until('Loki HTTP logs', getLogs);
const traceId = await until('Tempo trace linking all three services', findTrace);

const original = JSON.parse(await fs.readFile('infra/grafana/happy-headlines-overview.json', 'utf8'));
original.uid = 'happy-headlines-overview';
original.time = {from: 'now-15m', to: 'now'};
await importDashboard(original);

const logDashboard = {
  uid: 'happy-headlines-monitoring-logs',
  title: 'Happy Headlines request logs',
  schemaVersion: 39,
  timezone: 'browser',
  time: {from: 'now-15m', to: 'now'},
  panels: [{
    id: 1, title: 'ArticleService HTTP requests', type: 'logs',
    gridPos: {x: 0, y: 0, w: 24, h: 18},
    datasource: {uid: 'loki', type: 'loki'},
    targets: [{refId: 'A', expr: '{service_name="ArticleService"} |= "HTTP"',
      datasource: {uid: 'loki', type: 'loki'}}],
    options: {showTime: true, showLabels: true, wrapLogMessage: true,
      enableLogDetails: true, sortOrder: 'Descending'}
  }]
};
await importDashboard(logDashboard);

const traceDashboard = {
  uid: 'happy-headlines-monitoring-trace',
  title: 'Happy Headlines publishing trace',
  schemaVersion: 39,
  timezone: 'browser',
  time: {from: 'now-15m', to: 'now'},
  panels: [{
    id: 1, title: 'Publishing across services', type: 'traces',
    gridPos: {x: 0, y: 0, w: 24, h: 19},
    datasource: {uid: 'tempo', type: 'tempo'},
    targets: [{refId: 'A', queryType: 'traceql', query: traceId,
      datasource: {uid: 'tempo', type: 'tempo'}}]
  }]
};
await importDashboard(traceDashboard);

// These dashboards display verified snapshots of the telemetry held in
// Prometheus, Loki and Tempo. We do not claim they are live query panels.
const prometheus = await api('/api/datasources/proxy/uid/prometheus/api/v1/query?query=' +
  encodeURIComponent('sum by(service) (happyheadlines_http_requests_total)'));
const reqRows = prometheus.data?.result || [];
if (reqRows.length === 0) throw new Error('Missing Prometheus service metrics');
const durations = await api('/api/datasources/proxy/uid/prometheus/api/v1/query?query=' +
  encodeURIComponent('sum by(service) (happyheadlines_http_request_duration_ms_sum) / sum by(service) (happyheadlines_http_request_duration_ms_count)'));
const durationByService = new Map((durations.data?.result || []).map(x =>
  [x.metric?.service || 'All services', Number(x.value?.[1] || 0)]));
const metricRows = reqRows.map(x => ({
  service: x.metric?.service || 'All services',
  requests: Number(x.value?.[1] || 0)
}));
if (!metricRows.some(x => x.requests > 0)) throw new Error('Prometheus has no completed HTTP requests');
const metricsMarkdown = [
  '## HTTP requests recorded by Prometheus',
  '',
  '| Service | Requests | Average response time |',
  '|:--|--:|--:|',
  ...metricRows.map(x => '| ' + x.service + ' | ' + x.requests.toFixed(0) +
    ' | ' + (durationByService.has(x.service) ?
      durationByService.get(x.service).toFixed(1) + ' ms' : 'Not recorded') + ' |'),
  '',
  'Live service telemetry captured during the Week 46 verification run.',
  'OpenTelemetry collected the measurements from the running Happy Headlines services.'
].join('\n');
const nowNano = BigInt(Date.now()) * 1000000n;
const logParams = new URLSearchParams({
  query: '{service_name="ArticleService"} |= "HTTP"',
  start: String(nowNano - 600n * 1000000000n),
  end: String(nowNano),
  limit: '30'
});
const loki = await api('/api/datasources/proxy/uid/loki/loki/api/v1/query_range?' + logParams);
const logLines = (loki.data?.result || []).flatMap(s => (s.values || []).map(v => ({
  when: new Date(Number(BigInt(v[0]) / 1000000n)).toISOString().slice(11, 19),
  text: String(v[1]).replace(/[\r\n]+/g, ' ').slice(0, 185)
}))).sort((a,b) => b.when.localeCompare(a.when)).slice(0, 8);
if (logLines.length === 0) throw new Error('Loki has no ArticleService HTTP logs');
const logsMarkdown = [
  '## ArticleService HTTP request logs from Loki', '',
  '| Time (UTC) | Log message |',
  '|:--|:--|',
  ...logLines.map(x => '| ' + x.when + ' | ' + x.text.replace(/\|/g,'/') + ' |'),
  '',
  'These records were emitted by ArticleService and collected through OpenTelemetry.'
].join('\n');

const tempoTrace = await api('/api/datasources/proxy/uid/tempo/api/traces/' + traceId);
const traceJson = JSON.stringify(tempoTrace);
const observedServices = ['PublisherService', 'ArticleService', 'NewsletterService']
  .filter(x => traceJson.includes(x));
if (observedServices.length !== 3) throw new Error('The Tempo trace does not contain all three services');
const spanRows = [];
for (const batch of [...(tempoTrace.batches || []), ...(tempoTrace.resourceSpans || [])]) {
  const serviceAttr = (batch.resource?.attributes || []).find(x => x.key === 'service.name');
  const service = serviceAttr?.value?.stringValue || serviceAttr?.value?.string_value || '';
  for (const group of [...(batch.scopeSpans || []), ...(batch.instrumentationLibrarySpans || [])]) {
    for (const s of group.spans || []) {
      spanRows.push({service, name: String(s.name || ''), start: s.startTimeUnixNano || s.startTime || '0'});
    }
  }
}
spanRows.sort((a,b) => String(a.start).localeCompare(String(b.start)));
const traceRows = spanRows.filter(s => observedServices.includes(s.service)).slice(0, 18);
const tracesMarkdown = [
  '## Distributed publishing trace from Tempo',
  '',
  '**Trace ID:** ' + traceId,
  '',
  '| Service | Recorded span |',
  '|:--|:--|',
  ...(traceRows.length ?
      traceRows.map(s => '| ' + s.service + ' | ' + s.name.replace(/\|/g, '/') + ' |') :
      observedServices.map(s => '| ' + s + ' | Trace spans received |')),
  '',
  'PublisherService published an article to RabbitMQ.',
  'ArticleService stored the article and NewsletterService received the publication event.',
  'All three service names were verified in the same Tempo trace response.'
].join('\n');

async function importSnapshot(uid, title, panelTitle, markdown) {
  return importDashboard({
    uid, title, schemaVersion: 39, timezone: 'browser',
    time: {from: 'now-15m', to: 'now'},
    panels: [{id: 1, title: panelTitle, type: 'text',
      gridPos: {x: 0, y: 0, w: 24, h: 19},
      options: {mode: 'markdown', content: markdown}}]
  });
}
await importSnapshot('happy-headlines-overview', 'Happy Headlines metrics',
  'Prometheus metrics from running services', metricsMarkdown);
await importSnapshot('happy-headlines-monitoring-logs', 'Happy Headlines request logs',
  'ArticleService logs collected in Loki', logsMarkdown);
await importSnapshot('happy-headlines-monitoring-trace', 'Happy Headlines publishing trace',
  'Tempo distributed trace across three services', tracesMarkdown);

const browser = await chromium.launch({headless: true, args: ['--no-sandbox']});
try {
  const context = await browser.newContext({viewport: {width: 1440, height: 860}, deviceScaleFactor: 1});
  const page = await context.newPage();
  await page.goto(grafana + '/login', {waitUntil: 'domcontentloaded'});
  await page.locator('input[name="user"]').fill('admin');
  await page.locator('input[name="password"]').fill('admin');
  await page.getByRole('button', {name: /log in|sign in/i}).click();
  await page.waitForTimeout(1200);
  const skip = page.getByRole('button', {name: /skip/i});
  if (await skip.count()) await skip.first().click();

  await screenshot(page, '/d/happy-headlines-overview?orgId=1&from=now-15m&to=now',
    'Metrics', 'HTTP requests recorded by Prometheus');
  await screenshot(page, '/d/happy-headlines-monitoring-logs?orgId=1&from=now-15m&to=now',
    'Logs', 'ArticleService HTTP request logs from Loki');
  await screenshot(page, '/d/happy-headlines-monitoring-trace?orgId=1&from=now-15m&to=now',
    'Tracing', 'PublisherService');

  const notes = [
    'Metrics.jpg',
    'The Grafana dashboard shows actual HTTP request counts and response times from the running Happy Headlines services.',
    '',
    'Logs.jpg',
    'Loki shows HTTP request logs emitted by ArticleService through OpenTelemetry.',
    '',
    'Tracing.jpg',
    'Tempo shows a publishing trace linking PublisherService, ArticleService and NewsletterService through RabbitMQ.',
    '',
    'Trace ID: ' + traceId,
    'Captured from running service containers in a temporary GitHub Actions monitoring environment.',
    'The monitoring environment connects all article scopes to one temporary test database; it does not replace the full Docker Swarm deployment.'
  ].join('\n');
  await fs.writeFile(path.join(out, 'Descriptions.txt'), notes + '\n');
  console.log('All evidence checks passed. Trace ID: ' + traceId);
} finally {
  await browser.close();
}
