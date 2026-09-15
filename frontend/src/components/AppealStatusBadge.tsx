import { cn } from '@/lib/utils'

export type AppealStatus = 'Open' | 'Answered' | 'Closed'

const LABEL: Record<AppealStatus, string> = {
  Open: 'รอครูตอบ',
  Answered: 'ครูตอบแล้ว',
  Closed: 'เสร็จสิ้น',
}

/** ป้ายสถานะคำถามเรื่องคะแนน ใช้ทั้งหน้ารายการและหน้า thread */
export function AppealStatusBadge({ status }: { status: AppealStatus }) {
  return (
    <span
      className={cn(
        'shrink-0 rounded-full px-2.5 py-0.5 text-xs',
        status === 'Open' && 'bg-accent text-accent-foreground',
        status === 'Answered' && 'bg-primary text-primary-foreground',
        status === 'Closed' && 'bg-muted text-muted-foreground',
      )}
    >
      {LABEL[status]}
    </span>
  )
}
