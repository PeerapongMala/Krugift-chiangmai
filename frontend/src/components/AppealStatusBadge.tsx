import { cn } from '@/lib/utils'

export type AppealStatus = 'Open' | 'Answered' | 'Closed'
export type AppealViewer = 'teacher' | 'student'

/** ครูเห็นจากมุมตัวเอง (รอตอบ / ตอบแล้ว) · นักเรียนเห็นว่ากำลังรอใคร (รอครูตอบ / ครูตอบแล้ว) */
const LABEL: Record<AppealViewer, Record<AppealStatus, string>> = {
  teacher: { Open: 'รอตอบ', Answered: 'ตอบแล้ว', Closed: 'เสร็จสิ้น' },
  student: { Open: 'รอครูตอบ', Answered: 'ครูตอบแล้ว', Closed: 'เสร็จสิ้น' },
}

/** ป้ายสถานะคำถามเรื่องคะแนน ใช้ทั้งหน้ารายการและหน้า thread */
export function AppealStatusBadge({ status, viewer }: { status: AppealStatus; viewer: AppealViewer }) {
  return (
    <span
      className={cn(
        'shrink-0 rounded-full px-2.5 py-0.5 text-xs',
        status === 'Open' && 'bg-accent text-accent-foreground',
        status === 'Answered' && 'bg-primary text-primary-foreground',
        status === 'Closed' && 'bg-success text-success-foreground',
      )}
    >
      {LABEL[viewer][status]}
    </span>
  )
}
