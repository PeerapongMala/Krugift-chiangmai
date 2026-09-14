import { cn } from '@/lib/utils'

/**
 * เครดิตภาพมาสคอต — license ฟรีของ Freepik บังคับให้แสดงเครดิตพร้อมลิงก์กลับ
 * ทำให้เล็กและจางได้ แต่ห้ามซ่อนจนอ่านไม่ออก (ผิดทั้ง license และ accessibility)
 * 12px คือขนาดต่ำสุดที่ยังอ่านออกจริง
 * ถ้าอัปเป็น Freepik Premium หรือเปลี่ยนไปใช้ภาพ CC0 เอาไฟล์นี้ออกได้เลย
 */
export function Credit({ className }: { className?: string }) {
  return (
    <p className={cn('text-xs text-muted-foreground/70', className)}>
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
