import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createBridge } from './osrm-local-bridge.mjs';

test('fixed upstream, coordinate validation, quota and real response pass-through', async () => {
  let called = 0;
  const server = createBridge(async url => {
    called++;
    assert.equal(url, 'https://routing.openstreetmap.de/routed-car/route/v1/driving/106.7,10.7;106.8,10.8?overview=full&geometries=geojson&steps=false');
    return new Response('{"code":"Ok"}', { status: 200 });
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const base = `http://127.0.0.1:${server.address().port}`;
  try {
    assert.equal((await fetch(base + '/route/v1/driving/999,10;106,10')).status, 400);
    assert.equal((await fetch(base + '/other')).status, 400);
    const response = await fetch(base + '/route/v1/driving/106.7,10.7;106.8,10.8?url=https://invalid');
    assert.equal(response.status, 200);
    assert.equal(await response.text(), '{"code":"Ok"}');
    assert.equal((await fetch(base + '/route/v1/driving/106.7,10.7;106.8,10.8')).status, 429);
    assert.equal(called, 1);
  } finally { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
});

test('network failure is returned without leaking URL', async () => {
  const server = createBridge(async () => { throw new Error('private location'); });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  try {
    const response = await fetch(`http://127.0.0.1:${server.address().port}/route/v1/driving/106,10;107,11`);
    assert.equal(response.status, 502);
    assert.equal(await response.text(), '');
  } finally { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
});
