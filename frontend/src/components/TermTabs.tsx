import { Link } from 'react-router'
import { routes } from '@/lib/keys'
import { cn } from '@/lib/utils'

type Tab = 'classrooms' | 'items'

const TABS: { key: Tab; label: string; to: (termId: number) => string }[] = [
  { key: 'classrooms', label: 'ห้องเรียน', to: routes.term },
  { key: 'items', label: 'รายการคะแนน', to: routes.termItems },
]

/**
 * แท็บของภาคเรียนหนึ่งภาค — ห้องเรียน / รายการคะแนน เป็นมุมมองของภาคเรียนเดียวกัน
 * แยกจากปุ่มการกระทำ (เพิ่มห้องเรียน นำเข้าไฟล์ ปัดคะแนน) ที่อยู่ใน TabToolbar ใต้แท็บ
 * โครงเดียวกับ ClassroomTabs เพื่อให้การสลับมุมมองทั้งแอปหน้าตาเหมือนกัน
 */
export function TermTabs({ termId, active }: { termId: number; active: Tab }) {
  return (
    <nav aria-label="มุมมองของภาคเรียน" className="mb-4 flex flex-wrap gap-1 border-b">
      {TABS.map((tab) => (
        <Link
          key={tab.key}
          to={tab.to(termId)}
          aria-current={tab.key === active ? 'page' : undefined}
          className={cn(
            '-mb-px shrink-0 border-b-2 px-3 py-2.5 text-sm',
            tab.key === active
              ? 'border-primary font-medium text-foreground'
              : 'border-transparent text-muted-foreground hover:text-foreground',
          )}
        >
          {tab.label}
        </Link>
      ))}
    </nav>
  )
}
