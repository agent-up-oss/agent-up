import http from 'node:http';

const escapeHtml = (value) => String(value)
  .replaceAll('&', '&amp;')
  .replaceAll('<', '&lt;')
  .replaceAll('>', '&gt;')
  .replaceAll('"', '&quot;')
  .replaceAll("'", '&#39;');

const renderCard = (item) => {
  if (Array.isArray(item)) {
    return `<tr>${item.map((cell) => `<td>${escapeHtml(cell)}</td>`).join('')}</tr>`;
  }

  return `
    <article class="card">
      <strong>${escapeHtml(item.title ?? item.name)}</strong>
      <span>${escapeHtml(item.detail ?? item.status ?? item.value ?? '')}</span>
    </article>`;
};

const renderTable = (route) => {
  const headings = route.columns ?? [];
  const rows = route.rows ?? [];
  return `
    <div class="table-wrap">
      <table>
        <thead><tr>${headings.map((heading) => `<th>${escapeHtml(heading)}</th>`).join('')}</tr></thead>
        <tbody>${rows.map(renderCard).join('')}</tbody>
      </table>
    </div>`;
};

const renderBody = (app, route, path) => {
  if (route.json) {
    return `<pre class="json">${escapeHtml(JSON.stringify(route.json, null, 2))}</pre>`;
  }

  const cards = route.cards?.length ? `<section class="grid">${route.cards.map(renderCard).join('')}</section>` : '';
  const table = route.rows?.length ? renderTable(route) : '';
  const actions = route.actions?.length
    ? `<div class="actions">${route.actions.map((action) => `<button type="button">${escapeHtml(action)}</button>`).join('')}</div>`
    : '';

  return `
    <section class="hero">
      <p class="eyebrow">${escapeHtml(app.workspace)}</p>
      <h1>${escapeHtml(route.heading)}</h1>
      <p>${escapeHtml(route.body)}</p>
      ${actions}
    </section>
    ${cards}
    ${table}
    <footer>
      <span>${escapeHtml(app.name)}</span>
      <span>${escapeHtml(path)}</span>
      <span>audit-ready demo surface</span>
    </footer>`;
};

const renderPage = (app, route, path) => `
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>${escapeHtml(app.name)} - ${escapeHtml(route.label)}</title>
    <style>
      :root {
        color-scheme: light;
        font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
        color: #172026;
        background: #f5f7fb;
      }
      * { box-sizing: border-box; }
      body { margin: 0; min-height: 100vh; }
      .shell { min-height: 100vh; display: flex; flex-direction: column; }
      nav {
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 14px 20px;
        background: #111827;
        color: white;
      }
      nav strong { margin-right: auto; font-size: 15px; }
      nav a {
        color: #cbd5e1;
        text-decoration: none;
        padding: 7px 10px;
        border-radius: 6px;
        font-size: 14px;
      }
      nav a.active { color: #052e2b; background: #5eead4; }
      main { width: min(1080px, calc(100vw - 32px)); margin: 0 auto; padding: 28px 0 34px; }
      .hero {
        background: white;
        border: 1px solid #d9e2ec;
        border-radius: 8px;
        padding: 24px;
        box-shadow: 0 12px 30px rgba(15, 23, 42, .08);
      }
      .eyebrow {
        margin: 0 0 8px;
        color: #0f766e;
        text-transform: uppercase;
        font-size: 12px;
        font-weight: 800;
        letter-spacing: .08em;
      }
      h1 { margin: 0; font-size: clamp(30px, 4vw, 48px); line-height: 1.02; letter-spacing: 0; }
      p { max-width: 760px; color: #475569; line-height: 1.6; }
      .actions { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 18px; }
      button {
        border: 0;
        border-radius: 6px;
        background: #0f766e;
        color: white;
        padding: 10px 14px;
        font-weight: 700;
      }
      .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 12px; margin-top: 18px; }
      .card {
        min-height: 108px;
        border-radius: 8px;
        border: 1px solid #d9e2ec;
        background: #ffffff;
        padding: 18px;
      }
      .card strong { display: block; margin-bottom: 8px; color: #111827; }
      .card span { color: #64748b; }
      .table-wrap { margin-top: 18px; overflow-x: auto; background: white; border: 1px solid #d9e2ec; border-radius: 8px; }
      table { width: 100%; border-collapse: collapse; }
      th, td { padding: 12px 14px; border-bottom: 1px solid #e5eaf0; text-align: left; white-space: nowrap; }
      th { color: #334155; background: #f8fafc; font-size: 13px; }
      .json { background: #0f172a; color: #d1fae5; padding: 18px; border-radius: 8px; overflow-x: auto; }
      footer { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 18px; color: #64748b; font-size: 13px; }
      footer span { background: #e2e8f0; border-radius: 999px; padding: 5px 10px; }
    </style>
  </head>
  <body>
    <div class="shell">
      <nav>
        <strong>${escapeHtml(app.name)}</strong>
        ${app.routes.map((item) =>
          `<a class="${item.path === path ? 'active' : ''}" href="${escapeHtml(item.path)}">${escapeHtml(item.label)}</a>`).join('')}
      </nav>
      <main>${renderBody(app, route, path)}</main>
    </div>
  </body>
</html>`;

export function startDemoApp(app) {
  const port = Number.parseInt(process.env[app.portVariable] ?? `${app.defaultPort}`, 10);
  const routes = new Map(app.routes.map((route) => [route.path, route]));
  const server = http.createServer((request, response) => {
    const url = new URL(request.url ?? '/', `http://localhost:${port}`);
    const route = routes.get(url.pathname);
    if (!route) {
      response.writeHead(404, { 'content-type': 'text/html; charset=utf-8' });
      response.end(renderPage(app, {
        label: 'Not found',
        heading: 'Route not found',
        body: `The demo app has no route for ${url.pathname}. Use the navigation links to continue.`,
        actions: ['Return to a known route']
      }, url.pathname));
      return;
    }

    if (route.json && url.pathname.startsWith('/api/')) {
      response.writeHead(200, { 'content-type': 'application/json; charset=utf-8' });
      response.end(JSON.stringify(route.json));
      return;
    }

    response.writeHead(200, { 'content-type': 'text/html; charset=utf-8' });
    response.end(renderPage(app, route, url.pathname));
  });

  server.listen(port, '127.0.0.1', () => {
    console.log(`${app.name} ready on http://127.0.0.1:${port}`);
  });
}
