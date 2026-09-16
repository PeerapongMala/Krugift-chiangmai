import { FileDown, FileUp } from 'lucide-react'
import { Link } from 'react-router'
import { buttonVariants } from '@/components/ui/button'
import { routes } from '@/lib/keys'

/** นำเข้า / ส่งออก Excel ของห้อง · ส่งออก = ไฟล์ตัวอย่างที่มีข้อมูลปัจจุบันของห้องครบ แก้แล้วนำเข้ากลับได้ */
export function ExcelButtons({ classroomId, kind }: { classroomId: number; kind: 'students' | 'scores' }) {
  return (
    <>
      <Link to={routes.classroomImport(classroomId, kind)} className={buttonVariants({ variant: 'outline' })}>
        <FileUp aria-hidden="true" />
        นำเข้า
      </Link>
      <a
        href={`/api/classrooms/${classroomId}/import/${kind}/template`}
        download
        className={buttonVariants({ variant: 'outline' })}
      >
        <FileDown aria-hidden="true" />
        ส่งออก
      </a>
    </>
  )
}
