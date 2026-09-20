import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { qk } from '@/lib/keys'

type Term = { id: number; name: string }

/**
 * ชื่อภาคเรียนไว้ใช้เป็นหัวข้อหน้า ครูจะได้รู้ว่ากำลังอยู่ภาคเรียนไหน
 * ใช้ cache ก้อนเดียวกับหน้ารายการภาคเรียน (qk.terms) ส่วนใหญ่จึงไม่ยิง request เพิ่ม
 * คืนสตริงว่างระหว่างโหลด ให้หน้าที่เรียกตัดสินใจเองว่าจะแสดงอะไรแทน
 */
export function useTermName(termId: number) {
  const terms = useQuery({
    queryKey: qk.terms,
    queryFn: () => api<Term[]>('/terms'),
    enabled: Number.isInteger(termId),
  })

  return terms.data?.find((t) => t.id === termId)?.name ?? ''
}
