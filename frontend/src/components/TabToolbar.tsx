import type { ReactNode } from 'react'

/**
 * ปุ่มของแท็บที่เปิดอยู่ วางใต้ ClassroomTabs ไม่ใช่ใน PageHeader
 * เพราะเป็นการกระทำของแท็บนั้น ไม่ใช่ของทั้งห้อง
 */
export function TabToolbar({ children }: { children: ReactNode }) {
  return <div className="mb-4 flex flex-wrap justify-end gap-2">{children}</div>
}
