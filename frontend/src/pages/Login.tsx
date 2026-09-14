import { Navigate, useSearchParams } from 'react-router'
import { AuthCard } from '@/components/AuthCard'
import { CodeForm } from '@/components/CodeForm'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { buttonVariants } from '@/components/ui/button'
import { homeOf, useMe } from '@/lib/auth'

export default function Login() {
  const { data: me } = useMe()
  const [params] = useSearchParams()

  if (me) return <Navigate to={homeOf(me.role)} replace />

  return (
    <AuthCard title="Krugift คะแนนคณิต" description="เข้าสู่ระบบเพื่อดูหรือจัดการคะแนน">
      {params.get('error') && (
        <Alert variant="destructive">
          <AlertDescription>เข้าสู่ระบบด้วย Google ไม่สำเร็จ กรุณาลองใหม่</AlertDescription>
        </Alert>
      )}

      {/* ต้องเป็นลิงก์ธรรมดา เพราะ OAuth ต้อง redirect ทั้งหน้า */}
      <a href="/api/auth/google" className={buttonVariants({ variant: 'outline' })}>
        เข้าสู่ระบบด้วย Google
      </a>

      <div className="flex items-center gap-3 text-xs text-muted-foreground">
        <span className="h-px flex-1 bg-border" />
        นักเรียนที่ไม่มี Google ใช้รหัสนักเรียน
        <span className="h-px flex-1 bg-border" />
      </div>

      <CodeForm endpoint="/auth/code-login" submitLabel="เข้าสู่ระบบ" />
    </AuthCard>
  )
}
