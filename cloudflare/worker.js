export default {
  async fetch(request, env) {
    try {
      const url = new URL(request.url);
      if (request.method === 'OPTIONS') return cors(new Response(null, { status: 204 }));
      if (url.pathname === '/' || url.pathname === '/health') return json({ ok: true, message: 'Dawish Cloudflare API جاهز' });

      const token = authToken(request);
      const isAdmin = token && token === env.ADMIN_TOKEN;
      const isShop = token && token === env.SHOP_TOKEN;
      if (!isAdmin && !isShop) return json({ error: 'unauthorized' }, 401);

      if (url.pathname === '/v1/admin/posts' && request.method === 'POST') {
        if (!isAdmin) return json({ error: 'admin token required' }, 403);
        return await createPost(request, env);
      }

      if (url.pathname === '/v1/admin/posts' && request.method === 'GET') {
        if (!isAdmin) return json({ error: 'admin token required' }, 403);
        return await listPosts(env);
      }

      if (url.pathname === '/v1/shop/due' && request.method === 'GET') {
        if (!isShop && !isAdmin) return json({ error: 'shop token required' }, 403);
        return await duePosts(env);
      }

      if (url.pathname === '/v1/shop/heartbeat' && request.method === 'POST') {
        if (!isShop && !isAdmin) return json({ error: 'shop token required' }, 403);
        return await heartbeat(request, env);
      }

      const mediaPrefix = '/v1/media/';
      if (url.pathname.startsWith(mediaPrefix) && request.method === 'GET') {
        const key = decodeURIComponent(url.pathname.slice(mediaPrefix.length));
        return await getMedia(env, key);
      }

      const resultMatch = url.pathname.match(/^\/v1\/shop\/posts\/([^/]+)\/result$/);
      if (resultMatch && request.method === 'POST') {
        if (!isShop && !isAdmin) return json({ error: 'shop token required' }, 403);
        return await postResult(request, env, resultMatch[1]);
      }

      return json({ error: 'not found' }, 404);
    } catch (err) {
      return json({ error: String(err?.message || err) }, 500);
    }
  }
};

function authToken(request) {
  const h = request.headers.get('authorization') || '';
  if (!h.toLowerCase().startsWith('bearer ')) return '';
  return h.slice(7).trim();
}

function json(data, status = 200) {
  return cors(new Response(JSON.stringify(data), {
    status,
    headers: { 'content-type': 'application/json; charset=utf-8' }
  }));
}

function cors(response) {
  const headers = new Headers(response.headers);
  headers.set('access-control-allow-origin', '*');
  headers.set('access-control-allow-methods', 'GET,POST,OPTIONS');
  headers.set('access-control-allow-headers', 'Authorization,Content-Type');
  return new Response(response.body, { status: response.status, statusText: response.statusText, headers });
}

function nowIso() {
  return new Date().toISOString();
}

function normalizeArabic(text) {
  return String(text || '')
    .replace(/[أإآ]/g, 'ا')
    .replace(/ى/g, 'ي')
    .trim();
}

function findMedicalClaims(text) {
  const blocked = [
    'يعالج', 'يشفي', 'علاج', 'دواء', 'جرعة', 'يناسب مرضى', 'ضغط', 'سكري', 'سكر',
    'مناعة', 'يقوي المناعة', 'هضم', 'قولون', 'التهاب', 'كحة', 'بلغم', 'معدة',
    'ينحف', 'تخسيس', 'ألم', 'الم', 'مسكن', 'للمفاصل', 'للصدر', 'للربو', 'للحساسية',
    'للجيوب', 'للصداع', 'treat', 'cure', 'heal', 'medicine', 'dose', 'diabetes',
    'immunity', 'digestion', 'inflammation', 'pain', 'medical'
  ];
  const normalized = normalizeArabic(text).toLowerCase();
  const hits = [];
  for (const word of blocked) {
    const term = normalizeArabic(word).toLowerCase();
    const pattern = term.includes(' ')
      ? new RegExp(escapeRegExp(term), 'i')
      : new RegExp(`(^|[^\p{L}\p{N}])${escapeRegExp(term)}($|[^\p{L}\p{N}])`, 'iu');
    if (pattern.test(normalized)) hits.push(word);
  }
  // لا نمنع عبارات تسويقية عامة مثل "مناسب للمناسبات"؛ المنع يعتمد على كلمات طبية محددة.
  return [...new Set(hits)];
}

function escapeRegExp(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function rowToPost(r) {
  return {
    id: r.id,
    caption: r.caption,
    instagramEnabled: !!r.instagram_enabled,
    tiktokEnabled: !!r.tiktok_enabled,
    snapchatEnabled: !!r.snapchat_enabled,
    instagramLocation: r.instagram_location || '',
    tiktokLocation: r.tiktok_location || '',
    snapchatLocation: r.snapchat_location || '',
    scheduledAt: r.scheduled_at,
    status: r.status,
    tiktokMode: r.tiktok_mode,
    snapchatMode: r.snapchat_mode,
    mediaKey: r.media_key,
    createdAt: r.created_at,
    updatedAt: r.updated_at
  };
}

async function createPost(request, env) {
  const form = await request.formData();
  const image = form.get('image');
  const caption = String(form.get('caption') || '').trim();

  if (!caption) return json({ error: 'caption is required' }, 400);
  const medicalHits = findMedicalClaims(caption);
  if (medicalHits.length) return json({ error: 'medical_claim_blocked', hits: medicalHits }, 400);
  if (!image || typeof image === 'string') return json({ error: 'image is required' }, 400);

  const id = crypto.randomUUID();
  const fileName = image.name || 'image.jpg';
  const ext = fileName.includes('.') ? fileName.split('.').pop().toLowerCase() : 'jpg';
  const safeExt = ['jpg', 'jpeg', 'png', 'webp'].includes(ext) ? ext : 'jpg';
  const mediaKey = `posts/${id}/image.${safeExt}`;
  const created = nowIso();
  await env.MEDIA.put(mediaKey, image.stream(), { httpMetadata: { contentType: image.type || 'image/jpeg' } });

  await env.DB.prepare(`INSERT INTO posts (
    id, caption, instagram_enabled, tiktok_enabled, snapchat_enabled,
    instagram_location, tiktok_location, snapchat_location, scheduled_at,
    status, tiktok_mode, snapchat_mode, media_key, created_at, updated_at
  ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`)
    .bind(
      id,
      caption,
      number(form.get('instagram_enabled'), 1),
      number(form.get('tiktok_enabled'), 1),
      number(form.get('snapchat_enabled'), 0),
      String(form.get('instagram_location') || ''),
      String(form.get('tiktok_location') || ''),
      String(form.get('snapchat_location') || ''),
      String(form.get('scheduled_at') || created),
      'scheduled',
      'image_only',
      'image_only',
      mediaKey,
      created,
      created
    ).run();

  await env.DB.prepare(`INSERT INTO post_events (id, post_id, device_id, event_type, message, created_at) VALUES (?, ?, ?, ?, ?, ?)`)
    .bind(crypto.randomUUID(), id, 'admin', 'created', 'created by manager app', created).run();

  return json({ id, mediaKey, message: 'post scheduled' });
}

function number(value, fallback) {
  if (value === null || value === undefined || value === '') return fallback;
  return String(value) === '1' || String(value).toLowerCase() === 'true' ? 1 : 0;
}

async function listPosts(env) {
  const { results } = await env.DB.prepare(`SELECT * FROM posts ORDER BY scheduled_at DESC LIMIT 200`).all();
  return json(results.map(rowToPost));
}

async function duePosts(env) {
  const now = nowIso();
  const { results } = await env.DB.prepare(`SELECT * FROM posts WHERE status = 'scheduled' AND scheduled_at <= ? ORDER BY scheduled_at ASC LIMIT 20`).bind(now).all();
  return json(results.map(rowToPost));
}

async function heartbeat(request, env) {
  const body = await request.json().catch(() => ({}));
  const device = String(body.device || 'shop-pc');
  const role = String(body.mode || 'shop');
  const ts = nowIso();
  await env.DB.prepare(`INSERT INTO devices (id, name, role, last_seen_at, status) VALUES (?, ?, ?, ?, ?) ON CONFLICT(id) DO UPDATE SET name=excluded.name, role=excluded.role, last_seen_at=excluded.last_seen_at, status=excluded.status`)
    .bind(device, device, role, ts, 'online').run();
  return json({ ok: true, lastSeenAt: ts });
}

async function getMedia(env, key) {
  const object = await env.MEDIA.get(key);
  if (!object) return json({ error: 'media not found' }, 404);
  return cors(new Response(object.body, { headers: { 'content-type': object.httpMetadata?.contentType || 'application/octet-stream' } }));
}

async function postResult(request, env, postId) {
  const body = await request.json().catch(() => ({}));
  const status = String(body.status || 'assistant_opened');
  const message = String(body.message || '');
  const platform = String(body.platform || 'all');
  const ts = nowIso();
  await env.DB.prepare(`INSERT INTO publish_results (id, post_id, platform, status, message, screenshot_key, created_at) VALUES (?, ?, ?, ?, ?, ?, ?)`)
    .bind(crypto.randomUUID(), postId, platform, status, message, '', ts).run();
  await env.DB.prepare(`UPDATE posts SET status = ?, updated_at = ? WHERE id = ?`).bind(status, ts, postId).run();
  await env.DB.prepare(`INSERT INTO post_events (id, post_id, device_id, event_type, message, created_at) VALUES (?, ?, ?, ?, ?, ?)`)
    .bind(crypto.randomUUID(), postId, 'shop', status, message, ts).run();
  return json({ ok: true });
}
