import { Link } from 'react-router'
import { routes } from '@/lib/keys'
import { cn } from '@/lib/utils'

type Tab = 'students' | 'items' | 'scores'

const TABS: { key: Tab; label: string; to: (classroomId: number) => string }[] = [
  { key: 'students', label: 'นักเรียน', to: routes.classroom },
  { key: 'items', label: 'รายการคะแนน', to: routes.classroomItems },
  { key: 'scores', label: 'ตารางคะแนน', to: routes.classroomScores },
]

/**
 * แท็บของห้องเรียนหนึ่งห้อง — นักเรียน / รายการ / ตารางคะแนน เป็นมุมมองของห้องเดียวกัน
 * ไม่ใช่เมนูหลักของแอป จึงอยู่ใต้หัวข้อหน้า และบอกชัดว่าตอนนี้อยู่แท็บไหน
 */
export function ClassroomTabs({ classroomId, active }: { classroomId: number; active: Tab }) {
  return (
    // ไม่ใส่ overflow-x-auto: แท็บ 3 อันพอดีจอ 400px อยู่แล้ว และ overflow จะทำให้ -mb-px ของแท็บ
    // (ให้เส้นใต้ทับขอบล่างพอดี) ล้นแนวตั้ง 1px จน scrollbar โผล่ขึ้นมา
    <nav aria-label="มุมมองของห้องเรียน" className="mb-4 flex flex-wrap gap-1 border-b">
      {TABS.map((tab) => (
        <Link
          key={tab.key}
          to={tab.to(classroomId)}
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
