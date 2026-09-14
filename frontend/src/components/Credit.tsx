import { cn } from '@/lib/utils'

/**
 * เครดิตภาพมาสคอต — license ฟรีของ Freepik บังคับให้ใส่เครดิตพร้อมลิงก์กลับ
 * ถ้าเปลี่ยนไปใช้ภาพจากที่อื่นหรืออัปเป็น Freepik Premium เอาออกได้
 */
export function Credit({ className }: { className?: string }) {
  return (
    <p className={cn('text-center text-xs text-muted-foreground', className)}>
      ภาพคาปิบาร่าโดย{' '}
      <a
        href="https://www.freepik.com"
        target="_blank"
        rel="noopener noreferrer"
        className="underline underline-offset-2 hover:text-foreground"
      >
        Freepik
      </a>
    </p>
  )
}
