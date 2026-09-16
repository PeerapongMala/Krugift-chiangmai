/**
 * ตรวจ input ฝั่งเว็บ — คืน null = ผ่าน, คืน string = ข้อความไทยที่โชว์ได้เลย
 *
 * ตั้งใจให้ "หน้าตาเหมือน" Common/Validate.cs ฝั่ง backend เป๊ะ ๆ
 * ฝั่งนี้มีไว้ให้ผู้ใช้รู้ผลทันทีโดยไม่ต้องรอ server — **ไม่ใช่** ตัวกันจริง
 * ตัวกันจริงคือฝั่ง backend เสมอ ห้ามตัดออกเด็ดขาด
 */

/** ต้องตรงกับ Common/Limits.cs */
export const LIMITS = {
  name: 200,
  message: 2000,
  studentCode: 20,
  scoreCeiling: 9999.99,
} as const

const trimmed = (value: string | null | undefined) => (value ?? '').trim()

/** DB เก็บ decimal(6,2) · ปัดด้วย 100 แล้วเทียบ กันเศษทศนิยมของ float แบบ 0.1 + 0.2 */
const hasAtMost2Decimals = (value: number) => Math.abs(Math.round(value * 100) - value * 100) < 1e-6

export const validate = {
  name(value: string | null | undefined, what: string): string | null {
    const v = trimmed(value)
    if (!v) return `กรุณากรอก${what}`
    if (v.length > LIMITS.name) return `${what}ยาวเกิน ${LIMITS.name} ตัวอักษร`
    return null
  },

  email(value: string | null | undefined): string | null {
    const v = trimmed(value)
    if (!v) return 'กรุณากรอกอีเมล'
    if (v.length > LIMITS.name) return `อีเมลยาวเกิน ${LIMITS.name} ตัวอักษร`
    // เช็คหยาบ ๆ พอให้รู้ตัวเร็ว ตัวตัดสินจริงคือ MailAddress ฝั่ง backend
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v)) return 'รูปแบบอีเมลไม่ถูกต้อง'
    return null
  },

  studentCode(value: string | null | undefined): string | null {
    const v = trimmed(value)
    if (!v) return 'กรุณากรอกรหัสนักเรียน'
    if (v.length > LIMITS.studentCode) return `รหัสนักเรียนยาวเกิน ${LIMITS.studentCode} ตัวอักษร`
    if (!/^[A-Za-z0-9]+$/.test(v)) return 'รหัสนักเรียนใช้ได้เฉพาะตัวเลขและตัวอักษร'
    return null
  },

  message(value: string | null | undefined): string | null {
    const v = trimmed(value)
    if (!v) return 'กรุณากรอกข้อความ'
    if (v.length > LIMITS.message) return `ข้อความยาวเกิน ${LIMITS.message} ตัวอักษร`
    return null
  },

  /** คะแนนเต็มของรายการประเมิน */
  maxScore(value: number | null | undefined): string | null {
    if (value == null || Number.isNaN(value)) return 'กรุณากรอกคะแนนเต็ม'
    if (value <= 0) return 'คะแนนเต็มต้องมากกว่า 0'
    if (value > LIMITS.scoreCeiling) return `คะแนนเต็มต้องไม่เกิน ${LIMITS.scoreCeiling}`
    if (!hasAtMost2Decimals(value)) return 'คะแนนเต็มมีทศนิยมได้ไม่เกิน 2 ตำแหน่ง'
    return null
  },

  /** คะแนนที่กรอก · null = ยังไม่ให้คะแนน ถือว่าผ่าน */
  score(value: number | null | undefined, maxScore: number): string | null {
    if (value == null) return null
    if (Number.isNaN(value)) return 'คะแนนต้องเป็นตัวเลข'
    if (value < 0) return 'คะแนนติดลบไม่ได้'
    if (value > maxScore) return `คะแนนเกินคะแนนเต็ม (${maxScore})`
    if (!hasAtMost2Decimals(value)) return 'คะแนนมีทศนิยมได้ไม่เกิน 2 ตำแหน่ง'
    return null
  },
}

/** คืนข้อความแรกที่ไม่ผ่าน หรือ null ถ้าผ่านหมด */
export const firstError = (...checks: (string | null)[]): string | null => checks.find(Boolean) ?? null
