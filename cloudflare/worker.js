const PLATFORMS = ['instagram', 'tiktok', 'snapchat'];
const TERMINAL_PLATFORM_STATUSES = new Set(['published', 'failed', 'needs_review', 'cancelled']);
const CLAIM_MINUTES = 15;
const RETENTION_DAYS = 30;

export default {
  async fetch(request, env) {
    try {
      const url = new URL(request.url);
      if (request.method === 'OPTIONS') return cors(new Response(null, { status: 204 }));
      if (url.pathname === '/' || url.pathname === '/health') {
        return json({ ok: true, message: 'Dawish Cloudflare API جاهز' });
      }

      const token = authToken(request);
      const isAdmin = token && token === env.ADMIN_TOKEN;
      const isShop = token && token === env.SHOP_TOKEN;
      if (!isAdmin && !isShop) return json({ error: 'unauthorized' }, 401);

      if (url.pathname === '/v1/admin/posts' && request.method === 'POST') {
        if (!isAdmin) return json({ error: 'admin token required' }, 403);
        return createPost(request, env);
      }
      if (url.pathname === '/v1/admin/posts' && request.method === 'GET') {
        if (!isAdmin) return json({ error: 'admin token required' }, 403);
        return listPosts(env);
      }
      if (url.pathname === '/v1/admin/cleanup' && request.method === 'POST') {
        if (!isAdmin) return json({ error: 'admin token required' }, 403);
        return json(await cleanupRetention(env));
      }

      const adminPostMatch = url.pathname.match(/^\/v1\/admin\/posts\/([^/]+)\/(cancel|reschedule|events)$/);
      if (adminPostMatch) {
        if (!isAdmin) return json({ error: 'admin token required' }, 403);
        const postId = decodeURIComponent(adminPostMatch[1]);
        const action = adminPostMatch[2];
        if (action === 'cancel' && request.method === 'POST') return cancelPost(env, postId);
        if (action === 'reschedule' && request.method === 'POST') return reschedulePost(request, env, postId);
        if (action === 'events' && request.method === 'GET') return postEvents(env, postId);
      }

      if (url.pathname === '/v1/shop/due' && request.method === 'GET') return duePosts(env);
      if (url.pathname === '/v1/shop/claim' && request.method === 'POST') return claimPost(request, env);
      if (url.pathname === '/v1/shop/heartbeat' && request.method === 'POST') return heartbeat(request, env);

      const mediaPrefix = '/v1/media/';
      if (url.pathname.startsWith(mediaPrefix) && request.method === 'GET') {
        return getMedia(env, decodeURIComponent(url.pathname.slice(mediaPrefix.length)));
      }

      const resultMatch = url.pathname.match(/^\/v1\/shop\/posts\/([^/]+)\/(?:platform-result|result)$/);
      if (resultMatch && request.method === 'POST') {
        return postResult(request, env, decodeURIComponent(resultMatch[1]));
      }

      return json({ error: 'not found' }, 404);
    } catch (error) {
      return json({ error: String(error?.message || error) }, 500);
    }
  },

  async scheduled(_controller, env, ctx) {
    ctx.waitUntil(cleanupRetention(env));
  }
};

function authToken(request) {
  const header = request.headers.get('authorization') || '';
  return header.toLowerCase().startsWith('bearer ') ? header.slice(7).trim() : '';
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

function plusMinutes(iso, minutes) {
  return new Date(Date.parse(iso) + minutes * 60_000).toISOString();
}

export function retentionDeadline(fromIso, days = RETENTION_DAYS) {
  return new Date(Date.parse(fromIso) + days * 86_400_000).toISOString();
}

export function isValidFutureSchedule(value, nowMs = Date.now()) {
  const parsed = Date.parse(String(value || ''));
  return Number.isFinite(parsed) && parsed > nowMs;
}

export function parsePlatformStates(encoded) {
  if (!encoded) return {};
  return String(encoded).split(',').reduce((result, item) => {
    const separator = item.indexOf(':');
    if (separator > 0) result[item.slice(0, separator)] = item.slice(separator + 1);
    return result;
  }, {});
}

export function computeOverallStatus(statuses) {
  if (!statuses.length) return 'cancelled';
  if (statuses.every(status => status === 'published')) return 'published';
  if (statuses.every(status => status === 'cancelled')) return 'cancelled';
  if (statuses.some(status => status === 'needs_review')) return 'needs_review';
  if (statuses.some(status => status === 'failed')) return 'partial_failed';
  if (statuses.some(status => status === 'awaiting_confirmation')) return 'awaiting_confirmation';
  if (statuses.some(status => status === 'published')) return 'partially_published';
  return 'scheduled';
}

function enabledPlatforms(form) {
  return PLATFORMS.filter(platform => number(form.get(`${platform}_enabled`), 0) === 1);
}

function number(value, fallback) {
  if (value === null || value === undefined || value === '') return fallback;
  return String(value) === '1' || String(value).toLowerCase() === 'true' ? 1 : 0;
}

function rowToPost(row) {
  return {
    id: row.id,
    caption: row.caption,
    instagramEnabled: !!row.instagram_enabled,
    tiktokEnabled: !!row.tiktok_enabled,
    snapchatEnabled: !!row.snapchat_enabled,
    instagramLocation: row.instagram_location || '',
    tiktokLocation: row.tiktok_location || '',
    snapchatLocation: row.snapchat_location || '',
    scheduledAt: row.scheduled_at,
    status: row.status,
    platformStates: parsePlatformStates(row.platform_states),
    tiktokMode: row.tiktok_mode,
    snapchatMode: row.snapchat_mode,
    mediaKey: row.media_key,
    createdAt: row.created_at,
    updatedAt: row.updated_at
  };
}

async function createPost(request, env) {
  const form = await request.formData();
  const image = form.get('image');
  const caption = String(form.get('caption') || '').trim();
  if (!caption) return json({ error: 'caption is required' }, 400);
  if (!image || typeof image === 'string') return json({ error: 'image is required' }, 400);

  const medicalHits = findMedicalClaims(caption);
  if (medicalHits.length) return json({ error: 'medical_claim_blocked', hits: medicalHits }, 400);

  const platforms = enabledPlatforms(form);
  if (!platforms.length) return json({ error: 'at least one platform is required' }, 400);

  const id = crypto.randomUUID();
  const fileName = image.name || 'image.jpg';
  const extension = fileName.includes('.') ? fileName.split('.').pop().toLowerCase() : 'jpg';
  const safeExtension = ['jpg', 'jpeg', 'png', 'webp'].includes(extension) ? extension : 'jpg';
  const mediaKey = `posts/${id}/original.${safeExtension}`;
  const created = nowIso();

  await env.MEDIA.put(mediaKey, image.stream(), { httpMetadata: { contentType: image.type || 'image/jpeg' } });
  try {
    const statements = [
      env.DB.prepare(`INSERT INTO posts (
        id, caption, instagram_enabled, tiktok_enabled, snapchat_enabled,
        instagram_location, tiktok_location, snapchat_location, scheduled_at,
        status, tiktok_mode, snapchat_mode, media_key, created_at, updated_at
      ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`).bind(
        id, caption,
        platforms.includes('instagram') ? 1 : 0,
        platforms.includes('tiktok') ? 1 : 0,
        platforms.includes('snapchat') ? 1 : 0,
        String(form.get('instagram_location') || ''),
        String(form.get('tiktok_location') || ''),
        String(form.get('snapchat_location') || ''),
        String(form.get('scheduled_at') || created),
        'scheduled', 'image_only', 'image_only', mediaKey, created, created
      ),
      ...platforms.map(platform => env.DB.prepare(`INSERT INTO post_platforms
        (id, post_id, platform, status, attempt_count, last_error, published_at, updated_at)
        VALUES (?, ?, ?, 'scheduled', 0, '', NULL, ?)`).bind(`${id}:${platform}`, id, platform, created)),
      eventStatement(env, id, 'admin', 'created', `created for ${platforms.join(', ')}`, created)
    ];
    await env.DB.batch(statements);
  } catch (error) {
    await env.MEDIA.delete(mediaKey);
    throw error;
  }

  return json({ id, mediaKey, message: 'post scheduled', platformStates: Object.fromEntries(platforms.map(x => [x, 'scheduled'])) });
}

async function listPosts(env) {
  const { results } = await env.DB.prepare(`SELECT p.*,
    GROUP_CONCAT(pp.platform || ':' || pp.status) AS platform_states
    FROM posts p LEFT JOIN post_platforms pp ON pp.post_id = p.id
    GROUP BY p.id ORDER BY p.scheduled_at DESC LIMIT 200`).all();
  return json(results.map(rowToPost));
}

async function duePosts(env) {
  const now = nowIso();
  const { results } = await env.DB.prepare(`SELECT p.*,
    GROUP_CONCAT(pp.platform || ':' || pp.status) AS platform_states
    FROM posts p
    JOIN post_platforms pp ON pp.post_id = p.id
    LEFT JOIN post_claims pc ON pc.post_id = p.id
    WHERE p.scheduled_at <= ?
      AND p.status NOT IN ('published', 'cancelled')
      AND pp.status IN ('scheduled', 'retry')
      AND (pc.post_id IS NULL OR pc.expires_at <= ?)
    GROUP BY p.id ORDER BY p.scheduled_at ASC LIMIT 20`).bind(now, now).all();
  return json(results.map(rowToPost));
}

async function claimPost(request, env) {
  const body = await request.json().catch(() => ({}));
  const postId = String(body.postId || '').trim();
  const deviceId = String(body.deviceId || '').trim();
  if (!postId || !deviceId) return json({ error: 'postId and deviceId are required' }, 400);

  const claimedAt = nowIso();
  const expiresAt = plusMinutes(claimedAt, CLAIM_MINUTES);
  const result = await env.DB.prepare(`INSERT INTO post_claims (post_id, device_id, claimed_at, expires_at)
    SELECT ?, ?, ?, ? WHERE EXISTS (
      SELECT 1 FROM posts p
      WHERE p.id = ? AND p.scheduled_at <= ? AND p.status NOT IN ('published', 'cancelled')
        AND EXISTS (SELECT 1 FROM post_platforms pp WHERE pp.post_id = p.id AND pp.status IN ('scheduled', 'retry'))
    )
    ON CONFLICT(post_id) DO UPDATE SET
      device_id = excluded.device_id,
      claimed_at = excluded.claimed_at,
      expires_at = excluded.expires_at
    WHERE post_claims.expires_at <= ? OR post_claims.device_id = excluded.device_id`)
    .bind(postId, deviceId, claimedAt, expiresAt, postId, claimedAt, claimedAt).run();

  if (!result.meta?.changes) return json({ error: 'already_claimed' }, 409);
  await logEvent(env, postId, deviceId, 'claimed', `claim valid until ${expiresAt}`);
  return json({ ok: true, postId, deviceId, claimedAt, expiresAt });
}

async function postResult(request, env, postId) {
  const body = await request.json().catch(() => ({}));
  const platform = String(body.platform || '').toLowerCase();
  const status = String(body.status || '').toLowerCase();
  const message = String(body.message || '');
  const deviceId = String(body.deviceId || 'shop');
  const allowedStatuses = new Set(['scheduled', 'retry', 'awaiting_confirmation', 'published', 'failed', 'needs_review']);
  if (platform !== 'all' && !PLATFORMS.includes(platform)) return json({ error: 'invalid platform' }, 400);
  if (!allowedStatuses.has(status)) return json({ error: 'invalid status' }, 400);

  const claim = await env.DB.prepare(`SELECT device_id, expires_at FROM post_claims WHERE post_id = ?`).bind(postId).first();
  if (!claim || claim.device_id !== deviceId || claim.expires_at <= nowIso()) {
    return json({ error: 'claim_required' }, 409);
  }

  const updated = nowIso();
  const targetSql = platform === 'all' ? '' : ' AND platform = ?';
  const statement = env.DB.prepare(`UPDATE post_platforms SET
      status = ?, attempt_count = attempt_count + 1,
      last_error = ?, published_at = ?, updated_at = ?
    WHERE post_id = ?${targetSql}`);
  const bound = platform === 'all'
    ? statement.bind(status, status === 'failed' ? message : '', status === 'published' ? updated : null, updated, postId)
    : statement.bind(status, status === 'failed' ? message : '', status === 'published' ? updated : null, updated, postId, platform);
  const result = await bound.run();
  if (!result.meta?.changes) return json({ error: 'platform not enabled for post' }, 404);

  await env.DB.prepare(`INSERT INTO publish_results
    (id, post_id, platform, status, message, screenshot_key, created_at)
    VALUES (?, ?, ?, ?, ?, '', ?)`).bind(crypto.randomUUID(), postId, platform, status, message, updated).run();
  await logEvent(env, postId, deviceId, `platform_${status}`, `${platform}: ${message}`);

  const overallStatus = await refreshOverallStatus(env, postId, updated);
  if (['published', 'partial_failed', 'needs_review', 'cancelled'].includes(overallStatus)) {
    await env.DB.prepare(`DELETE FROM post_claims WHERE post_id = ?`).bind(postId).run();
  }
  return json({ ok: true, postId, platform, status, overallStatus });
}

async function refreshOverallStatus(env, postId, updated) {
  const { results } = await env.DB.prepare(`SELECT status FROM post_platforms WHERE post_id = ?`).bind(postId).all();
  const overall = computeOverallStatus(results.map(row => row.status));
  await env.DB.prepare(`UPDATE posts SET status = ?, updated_at = ? WHERE id = ?`).bind(overall, updated, postId).run();
  if (overall === 'published') await scheduleRetention(env, postId, updated);
  return overall;
}

async function cancelPost(env, postId) {
  const updated = nowIso();
  const post = await env.DB.prepare(`SELECT media_key FROM posts WHERE id = ?`).bind(postId).first();
  if (!post) return json({ error: 'post not found' }, 404);
  await env.DB.batch([
    env.DB.prepare(`UPDATE posts SET status = 'cancelled', updated_at = ? WHERE id = ?`).bind(updated, postId),
    env.DB.prepare(`UPDATE post_platforms SET status = 'cancelled', updated_at = ? WHERE post_id = ? AND status != 'published'`).bind(updated, postId),
    env.DB.prepare(`DELETE FROM post_claims WHERE post_id = ?`).bind(postId),
    retentionStatement(env, postId, post.media_key, updated),
    eventStatement(env, postId, 'admin', 'cancelled', 'cancelled by admin', updated)
  ]);
  return json({ ok: true, postId, status: 'cancelled', mediaDeleteAfter: retentionDeadline(updated) });
}

async function reschedulePost(request, env, postId) {
  const body = await request.json().catch(() => ({}));
  const scheduledAt = String(body.scheduledAt || '');
  if (!isValidFutureSchedule(scheduledAt)) return json({ error: 'scheduledAt must be a valid future date' }, 400);
  const updated = nowIso();
  const post = await env.DB.prepare(`SELECT id, status FROM posts WHERE id = ?`).bind(postId).first();
  if (!post) return json({ error: 'post not found' }, 404);
  if (post.status === 'published') return json({ error: 'published post cannot be rescheduled' }, 409);

  await env.DB.batch([
    env.DB.prepare(`UPDATE posts SET scheduled_at = ?, status = 'scheduled', updated_at = ? WHERE id = ?`).bind(new Date(scheduledAt).toISOString(), updated, postId),
    env.DB.prepare(`UPDATE post_platforms SET status = 'scheduled', last_error = '', updated_at = ?
      WHERE post_id = ? AND status != 'published'`).bind(updated, postId),
    env.DB.prepare(`DELETE FROM post_claims WHERE post_id = ?`).bind(postId),
    env.DB.prepare(`DELETE FROM retention_jobs WHERE post_id = ? AND status = 'pending'`).bind(postId),
    eventStatement(env, postId, 'admin', 'rescheduled', `new schedule: ${new Date(scheduledAt).toISOString()}`, updated)
  ]);
  return json({ ok: true, postId, status: 'scheduled', scheduledAt: new Date(scheduledAt).toISOString() });
}

async function postEvents(env, postId) {
  const { results } = await env.DB.prepare(`SELECT id, post_id, device_id, event_type, message, created_at
    FROM post_events WHERE post_id = ? ORDER BY created_at DESC LIMIT 200`).bind(postId).all();
  return json(results);
}

async function scheduleRetention(env, postId, completedAt) {
  const post = await env.DB.prepare(`SELECT media_key FROM posts WHERE id = ?`).bind(postId).first();
  if (post?.media_key) await retentionStatement(env, postId, post.media_key, completedAt).run();
}

function retentionStatement(env, postId, mediaKey, fromIso) {
  return env.DB.prepare(`INSERT INTO retention_jobs
    (post_id, media_key, delete_after, status, attempts, last_error, updated_at)
    VALUES (?, ?, ?, 'pending', 0, '', ?)
    ON CONFLICT(post_id) DO UPDATE SET
      media_key = excluded.media_key,
      delete_after = excluded.delete_after,
      status = 'pending', last_error = '', updated_at = excluded.updated_at`)
    .bind(postId, mediaKey, retentionDeadline(fromIso), fromIso);
}

async function cleanupRetention(env) {
  const started = nowIso();
  const { results } = await env.DB.prepare(`SELECT post_id, media_key FROM retention_jobs
    WHERE status = 'pending' AND delete_after <= ? ORDER BY delete_after LIMIT 25`).bind(started).all();
  let deleted = 0;
  let failed = 0;
  for (const job of results) {
    try {
      await env.MEDIA.delete(job.media_key);
      await env.DB.batch([
        env.DB.prepare(`UPDATE retention_jobs SET status = 'deleted', attempts = attempts + 1, updated_at = ? WHERE post_id = ?`).bind(nowIso(), job.post_id),
        eventStatement(env, job.post_id, 'system', 'media_deleted', job.media_key, nowIso())
      ]);
      deleted++;
    } catch (error) {
      await env.DB.prepare(`UPDATE retention_jobs SET attempts = attempts + 1, last_error = ?, updated_at = ? WHERE post_id = ?`)
        .bind(String(error?.message || error), nowIso(), job.post_id).run();
      failed++;
    }
  }
  return { ok: failed === 0, scanned: results.length, deleted, failed };
}

async function heartbeat(request, env) {
  const body = await request.json().catch(() => ({}));
  const device = String(body.device || 'shop-pc');
  const role = String(body.mode || 'shop');
  const timestamp = nowIso();
  await env.DB.prepare(`INSERT INTO devices (id, name, role, last_seen_at, status)
    VALUES (?, ?, ?, ?, 'online')
    ON CONFLICT(id) DO UPDATE SET name=excluded.name, role=excluded.role,
    last_seen_at=excluded.last_seen_at, status=excluded.status`)
    .bind(device, device, role, timestamp).run();
  return json({ ok: true, lastSeenAt: timestamp });
}

async function getMedia(env, key) {
  const object = await env.MEDIA.get(key);
  if (!object) return json({ error: 'media not found' }, 404);
  return cors(new Response(object.body, { headers: { 'content-type': object.httpMetadata?.contentType || 'application/octet-stream' } }));
}

function eventStatement(env, postId, deviceId, eventType, message, createdAt) {
  return env.DB.prepare(`INSERT INTO post_events (id, post_id, device_id, event_type, message, created_at)
    VALUES (?, ?, ?, ?, ?, ?)`).bind(crypto.randomUUID(), postId, deviceId, eventType, message, createdAt);
}

async function logEvent(env, postId, deviceId, eventType, message) {
  await eventStatement(env, postId, deviceId, eventType, message, nowIso()).run();
}

function normalizeArabic(text) {
  return String(text || '').replace(/[أإآ]/g, 'ا').replace(/ى/g, 'ي').trim();
}

function findMedicalClaims(text) {
  const blocked = [
    'يعالج', 'يشفي', 'علاج', 'دواء', 'جرعة', 'يناسب مرضى', 'ضغط', 'سكري', 'سكر',
    'مناعة', 'يقوي المناعة', 'هضم', 'قولون', 'التهاب', 'كحة', 'بلغم', 'معدة',
    'ينحف', 'تخسيس', 'ألم', 'مسكن', 'للمفاصل', 'للصدر', 'للربو', 'للحساسية',
    'للجيوب', 'للصداع', 'treat', 'cure', 'heal', 'medicine', 'dose', 'diabetes',
    'immunity', 'digestion', 'inflammation', 'pain', 'medical'
  ];
  const normalized = normalizeArabic(text).toLowerCase();
  return [...new Set(blocked.filter(word => {
    const term = normalizeArabic(word).toLowerCase();
    const pattern = term.includes(' ')
      ? new RegExp(escapeRegExp(term), 'i')
      : new RegExp(`(^|[^\\p{L}\\p{N}])${escapeRegExp(term)}($|[^\\p{L}\\p{N}])`, 'iu');
    return pattern.test(normalized);
  }))];
}

function escapeRegExp(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
