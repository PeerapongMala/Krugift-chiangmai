export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

type ApiInit = RequestInit & { json?: unknown }

/** เรียก /api/... ถ้า error จะ throw ApiError พร้อมข้อความ (detail) จาก ProblemDetails ของ server */
export async function api<T = void>(path: string, init: ApiInit = {}): Promise<T> {
  const { json, headers, ...rest } = init
  const res = await fetch(`/api${path}`, {
    ...rest,
    headers: json === undefined ? headers : { 'Content-Type': 'application/json', ...headers },
    body: json === undefined ? rest.body : JSON.stringify(json),
  })

  if (!res.ok) {
    const problem = await res.json().catch(() => null)
    const fallback = res.status === 429 ? 'ลองบ่อยเกินไป กรุณารอสักครู่' : 'เกิดข้อผิดพลาด กรุณาลองใหม่'
    throw new ApiError(res.status, problem?.detail ?? fallback)
  }

  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}
