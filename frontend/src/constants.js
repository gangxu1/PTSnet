export const BLOB_COLS = new Set(['FILECONTENT'])
export const HIDDEN_COLS = new Set([])
export const READONLY_COLS = new Set(['BATCH', 'CREATETIME', 'MODIFYDATE', 'TABLEID'])
export const REQUIRED_COLS = new Set(['BATCH'])
export const NEW_HIDDEN_COLS = new Set(['ADDA'])
export const MODAL_HIDDEN_COLS = new Set(['ADDC', 'ADDE', 'ADDF', 'FORWADER', 'TABLEID', 'CREATETIME', 'MODIFYDATE'])
export const COL_AUTOCOMPLETE = new Set(['CONTACT', 'LOCALCONTACT', 'WBS', 'COST'])

export const COL_LABELS = {
  BATCH: 'Batch',
  FAMILY: 'FwdName',
  FORWADER: 'ADDG',
  ADDA: 'Status'
}

export const COL_OPTIONS = {
  ADDA: ['', 'Open', 'Close', 'Others']
}

export const COL_FILTER_DEFAULTS = {
  ADDA: ['', 'Open']
}

export const COL_DEFAULTS = {
  MOD: 'Air NDF',
  FAMILY: 'Fedx',
  ADDA: ''
}

export const COL_ORDER = [
  'ADDA', 'BATCH', 'CONTACT', 'LOCALCONTACT', 'FAMILY', 'MOD', 'MODESIGN',
  'PONUMBER', 'COMMENTS', 'WBS', 'COST', 'INVOICENUM', 'INVOICEPRICE',
  'ERNUM', 'TABLEID', 'CREATETIME', 'MODIFYDATE', 'ADDB', 'ADDC', 'ADDE', 'ADDF', 'FORWADER'
]

export const IMAGE_EXTS = new Set(['png', 'jpg', 'jpeg', 'gif', 'bmp', 'webp'])
export const COL_VIS_KEY = 'colVisibility_PTS_TRA'

export const PAGE_TITLE = 'AMS Equipment Import Tracking, L2.6W5.0H2.8m'

export const ABOUT_TEXT = `1. 汇联易 添加COST WBS  Xiaoting Wu
2. 汇联易  其他问题 DanYi Fan
3. 供应链联系 SCM-EXT, CC   AU_SH_FA, RPA-CC`

export const FILE_ICONS = {
  pdf: '📄', doc: '📝', docx: '📝',
  xls: '📊', xlsx: '📊', csv: '📊',
  png: '🖼', jpg: '🖼', jpeg: '🖼', gif: '🖼', bmp: '🖼', webp: '🖼',
  zip: '🗜', rar: '🗜', '7z': '🗜'
}

export function colLabel(col) {
  return COL_LABELS[col] || col
}

export function today() {
  const d = new Date()
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

export function formatBytes(b) {
  if (b < 1024) return b + ' B'
  if (b < 1024 * 1024) return (b / 1024).toFixed(1) + ' KB'
  return (b / 1024 / 1024).toFixed(1) + ' MB'
}

export function fileIcon(name) {
  const ext = name.split('.').pop()?.toLowerCase() || ''
  return FILE_ICONS[ext] || '📎'
}

export function isImage(name) {
  const ext = name.split('.').pop()?.toLowerCase() || ''
  return IMAGE_EXTS.has(ext)
}

export function truncate30(val) {
  if (val == null) return ''
  const s = String(val)
  return s.length > 30 ? s.slice(0, 30) : s
}
