import type { ReactNode } from 'react'

export type ScoreRow = { id?: number; name: string; maxScore: number; value: number | null }

/** ปัดทศนิยม 2 ตำแหน่ง กันผลรวมแบบ 26.500000000000004 */
const round2 = (n: number) => Math.round(n * 100) / 100

/**
 * รายการคะแนนของนักเรียนหนึ่งคนในห้องหนึ่ง · ได้/เต็ม ทีละรายการ + รวม
 * ใช้ทั้งหน้าดูคะแนนด่วน (ไม่ล็อกอิน) และหน้าคะแนนของฉัน (ล็อกอินแล้ว)
 */
export function ScoreList({
  items,
  action,
}: {
  items: ScoreRow[]
  /** ปุ่มต่อท้ายแต่ละรายการ เช่น "สอบถาม" บนหน้าคะแนนของฉัน */
  action?: (item: ScoreRow) => ReactNode
}) {
  if (items.length === 0) {
    return <p className="text-center text-sm text-muted-foreground">ครูยังไม่ได้เพิ่มรายการคะแนน</p>
  }

  // นับเฉพาะรายการที่มีคะแนนแล้ว รายการที่ครูยังไม่ได้สอบจะได้ไม่ถูกนับเป็นคะแนนที่หายไป
  const graded = items.filter((i) => i.value !== null)
  const total = round2(graded.reduce((sum, i) => sum + (i.value ?? 0), 0))
  const full = round2(graded.reduce((sum, i) => sum + Number(i.maxScore), 0))

  return (
    <ul className="grid gap-2">
      {items.map((item, index) => (
        <li key={`${index}-${item.name}`} className="flex items-center justify-between gap-3 rounded-lg border px-3 py-2">
          <span className="min-w-0 truncate text-sm">{item.name}</span>
          <span className="shrink-0 text-sm tabular-nums">
            {/* ยังไม่กรอก แสดงขีด ไม่ใช่ 0 เด็กจะได้ไม่ตกใจว่าได้ศูนย์ */}
            <b>{item.value ?? '—'}</b>
            <span className="text-muted-foreground"> / {item.maxScore}</span>
          </span>
          {action?.(item)}
        </li>
      ))}
      <li className="flex items-center justify-between gap-3 rounded-lg bg-accent px-3 py-2 font-medium">
        <span>รวม</span>
        <span className="tabular-nums">
          {total} / {full}
        </span>
      </li>
    </ul>
  )
}
