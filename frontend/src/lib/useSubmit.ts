import { useState } from 'react'
import { errorMessage } from '@/lib/api'

/**
 * จัดการสถานะ busy/error ของการส่งฟอร์ม
 * แยกมาเพราะทุกฟอร์มในแอปต้องทำเหมือนกันหมด: ล็อกปุ่ม ล้าง error ยิง API แล้วแปลง error เป็นข้อความไทย
 */
export function useSubmit() {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  /** คืน true ถ้าสำเร็จ เอาไปตัดสินใจต่อได้ เช่น ปิด modal หรือ navigate */
  async function run(action: () => Promise<void>): Promise<boolean> {
    setBusy(true)
    setError('')
    try {
      await action()
      return true
    } catch (err) {
      setError(errorMessage(err))
      return false
    } finally {
      setBusy(false)
    }
  }

  /** ตรวจ input ก่อนยิง API · ถ้าไม่ผ่านจะโชว์ error แล้วคืน false */
  function check(message: string | null): boolean {
    if (message) {
      setError(message)
      return false
    }
    setError('')
    return true
  }

  return { busy, error, setError, run, check }
}
