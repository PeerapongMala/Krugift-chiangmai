import { FileDown } from 'lucide-react'
import { buttonVariants } from '@/components/ui/button'

/**
 * ส่งออกข้อมูลของห้องเป็น Excel ไฟล์เดียว 2 ชีท (รายชื่อนักเรียน กับ ตารางคะแนน)
 * ขาออกอย่างเดียว การนำเข้ามีทางเดียวคือไฟล์ของครูทั้งภาคเรียนที่หน้าภาคเรียน
 */
export function ExportButton({ classroomId }: { classroomId: number }) {
  return (
    <a href={`/api/classrooms/${classroomId}/export`} download className={buttonVariants({ variant: 'outline' })}>
      <FileDown aria-hidden="true" />
      ส่งออก Excel
    </a>
  )
}
