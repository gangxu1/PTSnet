const BASE = '/api/db'

async function post(path, body) {
  const r = await fetch(BASE + path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })
  return r.json()
}

async function get(path, params = {}) {
  const qs = new URLSearchParams(params).toString()
  const r = await fetch(BASE + path + (qs ? '?' + qs : ''), { cache: 'no-store' })
  return r.json()
}

export async function testConnection(cfg) {
  return post('/test', cfg)
}

export async function connectDB(cfg) {
  return post('/connect', cfg)
}

export async function getTables() {
  return get('/tables')
}

export async function getPK(table) {
  return get('/pk', { table })
}

export async function queryTable(params) {
  return get('/query', params)
}

export async function saveCell(body) {
  return post('/save', body)
}

export async function deleteRow(body) {
  return post('/delete', body)
}

export async function insertRow(formData) {
  const r = await fetch(BASE + '/insert', { method: 'POST', body: formData })
  return r.json()
}

export async function updateRow(formData) {
  const r = await fetch(BASE + '/update', { method: 'POST', body: formData })
  return r.json()
}

export async function getFileList(table, pkCol, pkVal) {
  return get('/filelist', { table, pkCol, pkVal })
}

export function fileDownloadUrl(table, pkCol, pkVal, filename) {
  const p = new URLSearchParams({ table, pkCol, pkVal, filename })
  return BASE + '/filedownload?' + p.toString()
}

export async function getDistinct(table, col) {
  return get('/distinct', { table, col, _t: Date.now() })
}
