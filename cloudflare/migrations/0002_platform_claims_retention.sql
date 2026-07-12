PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS post_platforms (
  id TEXT PRIMARY KEY,
  post_id TEXT NOT NULL,
  platform TEXT NOT NULL CHECK (platform IN ('instagram', 'tiktok', 'snapchat')),
  status TEXT NOT NULL DEFAULT 'scheduled',
  attempt_count INTEGER NOT NULL DEFAULT 0,
  last_error TEXT NOT NULL DEFAULT '',
  published_at TEXT,
  updated_at TEXT NOT NULL,
  FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE CASCADE,
  UNIQUE (post_id, platform)
);

CREATE TABLE IF NOT EXISTS post_claims (
  post_id TEXT PRIMARY KEY,
  device_id TEXT NOT NULL,
  claimed_at TEXT NOT NULL,
  expires_at TEXT NOT NULL,
  FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS retention_jobs (
  post_id TEXT PRIMARY KEY,
  media_key TEXT NOT NULL,
  delete_after TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'pending',
  attempts INTEGER NOT NULL DEFAULT 0,
  last_error TEXT NOT NULL DEFAULT '',
  updated_at TEXT NOT NULL,
  FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_post_platforms_due ON post_platforms(status, post_id);
CREATE INDEX IF NOT EXISTS idx_post_platforms_post ON post_platforms(post_id, platform);
CREATE INDEX IF NOT EXISTS idx_post_claims_expiry ON post_claims(expires_at);
CREATE INDEX IF NOT EXISTS idx_retention_jobs_due ON retention_jobs(status, delete_after);
CREATE INDEX IF NOT EXISTS idx_post_events_post_created ON post_events(post_id, created_at);
CREATE INDEX IF NOT EXISTS idx_devices_last_seen ON devices(last_seen_at);

-- Backfill platform rows for posts created before this migration.
INSERT OR IGNORE INTO post_platforms (id, post_id, platform, status, attempt_count, last_error, published_at, updated_at)
SELECT id || ':instagram', id, 'instagram',
  CASE WHEN status = 'published' THEN 'published' ELSE 'scheduled' END,
  0, '', CASE WHEN status = 'published' THEN updated_at ELSE NULL END, updated_at
FROM posts WHERE instagram_enabled = 1;

INSERT OR IGNORE INTO post_platforms (id, post_id, platform, status, attempt_count, last_error, published_at, updated_at)
SELECT id || ':tiktok', id, 'tiktok',
  CASE WHEN status = 'published' THEN 'published' ELSE 'scheduled' END,
  0, '', CASE WHEN status = 'published' THEN updated_at ELSE NULL END, updated_at
FROM posts WHERE tiktok_enabled = 1;

INSERT OR IGNORE INTO post_platforms (id, post_id, platform, status, attempt_count, last_error, published_at, updated_at)
SELECT id || ':snapchat', id, 'snapchat',
  CASE WHEN status = 'published' THEN 'published' ELSE 'scheduled' END,
  0, '', CASE WHEN status = 'published' THEN updated_at ELSE NULL END, updated_at
FROM posts WHERE snapchat_enabled = 1;
