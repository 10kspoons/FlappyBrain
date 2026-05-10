import Database from 'better-sqlite3'
import path from 'node:path'
import fs from 'node:fs'

export interface Score {
  id: number
  player_name: string
  score: number
  sections_completed: number
  played_at: string
}

export interface CurrentPlayer {
  name: string | null
  scanned_at: string | null
}

let db: Database.Database | null = null

function getDataDir(): string {
  return process.env.DATA_DIR ?? '/data'
}

export function getDb(): Database.Database {
  if (db) return db

  const dataDir = getDataDir()
  if (!fs.existsSync(dataDir)) {
    fs.mkdirSync(dataDir, { recursive: true })
  }

  const dbPath = path.join(dataDir, 'flappybrain.db')
  db = new Database(dbPath)
  db.pragma('journal_mode = WAL')

  db.exec(`
    CREATE TABLE IF NOT EXISTS scores (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      player_name TEXT NOT NULL,
      score INTEGER NOT NULL DEFAULT 0,
      sections_completed INTEGER NOT NULL DEFAULT 0,
      played_at TEXT NOT NULL DEFAULT (datetime('now'))
    );

    CREATE TABLE IF NOT EXISTS current_player (
      id INTEGER PRIMARY KEY CHECK (id = 1),
      name TEXT,
      scanned_at TEXT
    );
  `)

  const seedRow = db.prepare('SELECT id FROM current_player WHERE id = 1').get()
  if (!seedRow) {
    db.prepare('INSERT INTO current_player (id, name, scanned_at) VALUES (1, NULL, NULL)').run()
  }

  return db
}

export function getTopScores(limit = 10): Score[] {
  const rows = getDb()
    .prepare('SELECT * FROM scores ORDER BY score DESC, played_at DESC LIMIT ?')
    .all(limit) as Score[]
  return rows
}

export function getMostRecentScore(): Score | null {
  const row = getDb()
    .prepare('SELECT * FROM scores ORDER BY played_at DESC, id DESC LIMIT 1')
    .get() as Score | undefined
  return row ?? null
}

export function insertScore(playerName: string, score: number, sectionsCompleted: number): Score {
  const stmt = getDb().prepare(
    'INSERT INTO scores (player_name, score, sections_completed) VALUES (?, ?, ?)'
  )
  const result = stmt.run(playerName, score, sectionsCompleted)
  const inserted = getDb()
    .prepare('SELECT * FROM scores WHERE id = ?')
    .get(result.lastInsertRowid) as Score
  return inserted
}

export function getCurrentPlayer(): CurrentPlayer {
  const row = getDb()
    .prepare('SELECT name, scanned_at FROM current_player WHERE id = 1')
    .get() as { name: string | null; scanned_at: string | null } | undefined
  if (!row) return { name: null, scanned_at: null }
  return { name: row.name, scanned_at: row.scanned_at }
}

export function setCurrentPlayer(name: string | null): CurrentPlayer {
  const scannedAt = name ? new Date().toISOString() : null
  getDb()
    .prepare(
      `INSERT INTO current_player (id, name, scanned_at) VALUES (1, ?, ?)
       ON CONFLICT(id) DO UPDATE SET name = excluded.name, scanned_at = excluded.scanned_at`
    )
    .run(name, scannedAt)
  return { name, scanned_at: scannedAt }
}
