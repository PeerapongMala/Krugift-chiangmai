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
    <nav aria-label="มุมมองของห้องเรียน" className="mb-4 flex gap-1 overflow-x-auto border-b">
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
