import { useCallback, useState, type ReactNode } from 'react'
import { Modal } from '@/components/Modal'

type ConfirmOptions = {
  title: string
  description?: ReactNode
  confirmLabel?: string
  cancelLabel?: string
  /** ปุ่มยืนยันเป็นสีแดง ใช้กับการลบ */
  destructive?: boolean
}

/**
 * กล่องยืนยันแบบ await ได้ ทำให้เขียนไล่ตามลำดับความคิดได้เลย
 *
 *     const { confirm, dialog } = useConfirm()
 *     const ok = await confirm({ title: 'ลบห้องเรียนนี้?', destructive: true })
 *     if (!ok) return
 *
 * ต้องเรนเดอร์ dialog ไว้ในหน้าด้วย ไม่งั้นกล่องจะไม่โผล่
 */
export function useConfirm() {
  const [pending, setPending] = useState<(ConfirmOptions & { resolve: (ok: boolean) => void }) | null>(null)

  const confirm = useCallback(
    (options: ConfirmOptions) => new Promise<boolean>((resolve) => setPending({ ...options, resolve })),
    [],
  )

  function settle(ok: boolean) {
    pending?.resolve(ok)
    setPending(null)
  }

  const dialog = pending && (
    <Modal
      open
      onOpenChange={(next) => {
        if (!next) settle(false)
      }}
      title={pending.title}
      description={pending.description}
      onSubmit={() => settle(true)}
      submitLabel={pending.confirmLabel ?? 'ยืนยัน'}
      cancelLabel={pending.cancelLabel ?? 'ยกเลิก'}
      destructive={pending.destructive}
    />
  )

  return { confirm, dialog }
}
