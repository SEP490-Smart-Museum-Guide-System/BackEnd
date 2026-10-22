// shortcut: Windows TLS compatibility for local development; deploy BE with working TLS or self-host OSRM.
import { createServer } from 'node:http';
import { pathToFileURL } from 'node:url';

const upstream = 'https://routing.openstreetmap.de/routed-car/';
export function createBridge(fetchRoute = fetch) {
  let nextRequest = 0;
  return createServer(async (req, res) => {
    const url = new URL(req.url, 'http://localhost');
    if (req.method === 'GET' && url.pathname === '/health') {
      res.writeHead(200).end('SMGS OSRM local bridge');
      return;
    }
    const match = /^\/route\/v1\/driving\/(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?);(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)$/.exec(url.pathname);
    if (req.method !== 'GET' || !match || match.slice(1).some((v, i) => !Number.isFinite(Number(v)) || Math.abs(Number(v)) > (i % 2 ? 90 : 180))) {
      res.writeHead(400).end();
      return;
    }
    if (Date.now() < nextRequest) {
      res.writeHead(429, { 'Retry-After': '1' }).end();
      return;
    }
    nextRequest = Date.now() + 1000;
    try {
      // Fixed destination, path and query: this is not a general HTTP proxy. Never log GPS URLs.
      const result = await fetchRoute(upstream + url.pathname.slice(1) + '?overview=full&geometries=geojson&steps=false', {
        signal: AbortSignal.timeout(8000), redirect: 'error', headers: { 'User-Agent': 'SMGS/1.0' },
      });
      const chunks = [];
      let size = 0;
      if (result.body) for await (const chunk of result.body) {
        size += chunk.length;
        if (size > 8 * 1024 * 1024) throw new Error('Response too large');
        chunks.push(chunk);
      }
      res.writeHead(result.status, { 'Content-Type': 'application/json' }).end(Buffer.concat(chunks));
    } catch (error) {
      res.writeHead(error.name === 'TimeoutError' ? 504 : 502).end();
    }
  });
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  createBridge().listen(5168, '127.0.0.1', () => console.log('SMGS OSRM bridge ready on 127.0.0.1:5168'));
}
