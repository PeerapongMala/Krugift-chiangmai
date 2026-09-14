import type { FormEvent, ReactNode } from 'react'
import { FormError } from '@/components/FormError'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'

export type ModalProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description?: ReactNode
  children?: ReactNode
  /** ใส่เมื่อ modal นี้เป็นฟอร์ม Modal จะห่อ form และสร้างปุ่มบันทึก/ยกเลิกให้เอง */
  onSubmit?: (form: FormData) => void | Promise<void>
  submitLabel?: string
  cancelLabel?: string
  /** ปุ่มยืนยันเป็นสีแดง ใช้กับการลบ */
  destructive?: boolean
  busy?: boolean
  error?: string
}

/**
 * modal กลางของแอป ครอบ shadcn Dialog อีกชั้นเพื่อไม่ให้แต่ละหน้าต้องประกอบ
 * Header/Footer/ปุ่ม/กล่อง error เองซ้ำ ๆ และคุมพฤติกรรมบนจอ 400px ไว้ที่เดียว
 */
export function Modal({
  open,
  onOpenChange,
  title,
  description,
  children,
  onSubmit,
  submitLabel = 'บันทึก',
  cancelLabel = 'ยกเลิก',
  destructive = false,
  busy = false,
  error = '',
}: ModalProps) {
  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    void onSubmit?.(new FormData(e.currentTarget))
  }

  const body = (
    <>
      {children}
      <FormError message={error} />
    </>
  )

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {/* จอมือถือ 400px: จำกัดความสูงแล้วให้เลื่อนในตัว modal เอง ไม่ให้เนื้อหาล้นออกนอกจอ */}
      <DialogContent className="max-h-[85svh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description && <DialogDescription>{description}</DialogDescription>}
        </DialogHeader>

        {onSubmit ? (
          <form onSubmit={handleSubmit} className="grid gap-4">
            {body}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
                {cancelLabel}
              </Button>
              <Button type="submit" variant={destructive ? 'destructive' : 'default'} disabled={busy}>
                {busy ? 'กำลังบันทึก...' : submitLabel}
              </Button>
            </DialogFooter>
          </form>
        ) : (
          <div className="grid gap-4">{body}</div>
        )}
      </DialogContent>
    </Dialog>
  )
}
