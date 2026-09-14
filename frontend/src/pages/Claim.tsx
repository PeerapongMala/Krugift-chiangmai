import { Navigate } from 'react-router'
import { AuthCard } from '@/components/AuthCard'
import { CodeForm } from '@/components/CodeForm'
import { buttonVariants } from '@/components/ui/button'
import { homeOf, useMe } from '@/lib/auth'

export default function Claim() {
  const { data: me, isPending } = useMe()

  if (isPending) return null
  if (!me) return <Navigate to="/login" replace />
  if (me.role !== 'pending') return <Navigate to={homeOf(me.role)} replace />

  return (
    <AuthCard
      title="ยืนยันตัวตนครั้งแรก"
      description={
        <>
          บัญชี <b>{me.name}</b> ยังไม่ได้ผูกกับนักเรียน กรอกรหัสนักเรียนและรหัสส่วนตัวที่ได้จากครู (ทำครั้งเดียว)
        </>
      }
    >
      <CodeForm endpoint="/auth/claim" submitLabel="ยืนยันและผูกบัญชี" />
      <p className="text-xs text-muted-foreground">
        ถ้าคุณเป็นครูแต่มาอยู่หน้านี้ แปลว่าอีเมลนี้ยังไม่อยู่ในรายชื่อครู ให้แจ้งผู้ดูแลระบบ
      </p>
      {/* พาไปเลือกบัญชีใหม่เลย · server ล้าง cookie เดิมและบังคับ Google ถามบัญชีให้อยู่แล้ว */}
      <a href="/api/auth/google" className={buttonVariants({ variant: 'ghost' })}>
        ใช้บัญชี Google อื่น
      </a>
    </AuthCard>
  )
}
