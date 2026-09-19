import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

/** รายการแบบมีเส้นคั่น ใช้แทนการ์ดต่อแถว อ่านไล่ลงมาเร็วกว่าและไม่แน่นบนมือถือ */
export function Rows({ children }: { children: ReactNode }) {
  return <ul className="divide-y overflow-hidden rounded-xl border bg-card">{children}</ul>
}

/** หนึ่งแถว · ปุ่มในแถวให้ห่อด้วย RowActions เพื่อให้ชิดขวาและตกบรรทัดใหม่ได้เองบนจอแคบ */
export function Row({ children, className }: { children: ReactNode; className?: string }) {
  return <li className={cn('flex flex-wrap items-center gap-x-3 gap-y-2 px-3 py-2.5', className)}>{children}</li>
}

export function RowActions({ children }: { children: ReactNode }) {
  return <div className="ml-auto flex shrink-0 flex-wrap items-center gap-2">{children}</div>
}
