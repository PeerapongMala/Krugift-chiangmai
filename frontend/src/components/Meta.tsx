import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

/**
 * บรรทัดข้อมูลประกอบใต้ชื่อ เช่น "นักเรียน 45 คน" "19 รายการ"
 * แต่ละอย่างเป็น span ของตัวเอง เว้นระยะด้วย gap ไม่ใช้ตัวคั่นที่ผู้ใช้ต้องตีความเอง
 */
export function Meta({ children, className }: { children: ReactNode; className?: string }) {
  return <p className={cn('flex flex-wrap gap-x-3 text-xs text-muted-foreground', className)}>{children}</p>
}
