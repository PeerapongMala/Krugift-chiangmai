/** ข้อความไทยตามรหัสสถานะ ใช้เมื่อ server ไม่ได้ส่ง detail มาเอง */
const BY_STATUS: Record<number, string> = {
  400: 'ข้อมูลที่ส่งไปไม่ถูกต้อง',
  401: 'หมดเวลาการเข้าสู่ระบบ กรุณาเข้าสู่ระบบใหม่',
  403: 'ไม่มีสิทธิ์ทำรายการนี้',
  404: 'ไม่พบข้อมูลที่ต้องการ',
  409: 'ข้อมูลซ้ำหรือขัดแย้งกับที่มีอยู่',
  413: 'ไฟล์ใหญ่เกินไป',
  429: 'ลองบ่อยเกินไป กรุณารอสักครู่แล้วลองใหม่',
}

export class ApiError extends Error {
  status: number
  /** ข้อความดิบจาก ProblemDetails ถ้ามี — ไว้ดูตอน debug */
  detail?: string

  constructor(status: number, message: string, detail?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.detail = detail
  }
}

/** เชื่อมต่อ server ไม่ได้ (เน็ตหลุด / server ไม่ได้รัน) — คนละเรื่องกับ server ตอบ error */
export class NetworkError extends Error {
  reason: unknown

  constructor(reason: unknown) {
    super('เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ตรวจสอบอินเทอร์เน็ตแล้วลองใหม่')
    this.name = 'NetworkError'
    this.reason = reason
  }
}

type ApiInit = RequestInit & { json?: unknown }

/** รวมข้อความจาก ValidationProblemDetails ที่ ASP.NET ส่งมาตอน model binding ไม่ผ่าน */
function fromValidationErrors(errors: unknown): string | undefined {
  if (!errors || typeof errors !== 'object') return undefined
  const messages = Object.values(errors as Record<string, string[]>).flat().filter(Boolean)
  return messages.length > 0 ? messages.join(' · ') : undefined
}

/**
 * เรียก /api/...
 * ถ้า server ตอบ error จะ throw ApiError พร้อมข้อความไทยที่เอาไปโชว์ผู้ใช้ได้เลย
 * ถ้าต่อ server ไม่ติดจะ throw NetworkError
 * ทุกกรณีจะ log ของดิบลง console ไว้ให้ไล่บั๊กต่อได้
 */
export async function api<T = void>(path: string, init: ApiInit = {}): Promise<T> {
  const { json, headers, ...rest } = init
  const method = rest.method ?? 'GET'

  let res: Response
  try {
    res = await fetch(`/api${path}`, {
      ...rest,
      headers: json === undefined ? headers : { 'Content-Type': 'application/json', ...headers },
      body: json === undefined ? rest.body : JSON.stringify(json),
    })
  } catch (cause) {
    console.error(`[api] ${method} ${path} — ต่อ server ไม่ติด`, cause)
    throw new NetworkError(cause)
  }

  if (!res.ok) {
    const raw = await res.text()
    let problem: Record<string, unknown> | null = null
    try {
      problem = raw ? JSON.parse(raw) : null
    } catch {
      // server ตอบไม่ใช่ JSON (เช่น หน้า error ของ proxy) เก็บ text ดิบไว้ดูใน console
    }

    const detail =
      (typeof problem?.detail === 'string' ? problem.detail : undefined) ??
      fromValidationErrors(problem?.errors) ??
      (typeof problem?.title === 'string' ? problem.title : undefined)

    // ใส่รหัสสถานะต่อท้ายเมื่อไม่มีข้อความเฉพาะ ผู้ใช้จะได้แจ้งเรากลับมาได้ว่าเจอรหัสอะไร
    const message =
      detail ??
      `${BY_STATUS[res.status] ?? (res.status >= 500 ? 'เซิร์ฟเวอร์มีปัญหา กรุณาลองใหม่ภายหลัง' : 'เกิดข้อผิดพลาด')} (รหัส ${res.status})`

    // 401 เป็นเรื่องปกติของคนที่ยังไม่ล็อกอิน (useMe เช็คทุกหน้า) ไม่ต้องรก console
    if (res.status !== 401) console.error(`[api] ${method} ${path} → ${res.status}`, problem ?? raw)
    throw new ApiError(res.status, message, detail)
  }

  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

/** แปลง error อะไรก็ได้ให้เป็นข้อความไทยที่โชว์ผู้ใช้ได้ */
export function errorMessage(err: unknown): string {
  if (err instanceof ApiError || err instanceof NetworkError) return err.message
  console.error('[api] error ที่ไม่รู้จัก', err)
  return err instanceof Error && err.message
    ? `เกิดข้อผิดพลาด: ${err.message}`
    : 'เกิดข้อผิดพลาดที่ไม่รู้สาเหตุ กรุณาลองใหม่'
}
