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
          <AlertDescription>
            เข้าสู่ระบบด้วย Google ไม่สำเร็จ กรุณากดปุ่มด้านล่างเพื่อเริ่มใหม่ (อย่ากดย้อนกลับของเบราว์เซอร์)
          </AlertDescription>
        </Alert>
      )}

      {/* ต้องเป็นลิงก์ธรรมดา เพราะ OAuth ต้อง redirect ทั้งหน้า */}
      <a href="/api/auth/google" className={buttonVariants({ variant: 'outline' })}>
        <GoogleLogo />
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

/**
 * โลโก้ G ของ Google — สีต้องเป็น 4 สีมาตรฐานเท่านั้น
 * branding guidelines ห้ามเปลี่ยนสีหรือทำ monochrome ให้เข้าธีม จึง hardcode ไว้ ไม่ใช้ token ของเรา
 * ใช้ที่เดียวเลยอยู่ในไฟล์นี้ ตามกฎโปรเจกต์ (แยกไฟล์เมื่อใช้ >= 2 ที่)
 */
function GoogleLogo() {
  return (
    <svg viewBox="0 0 24 24" className="size-4 shrink-0" aria-hidden="true" focusable="false">
      <path
        fill="#4285F4"
        d="M23.52 12.27c0-.79-.07-1.54-.2-2.27H12v4.51h6.47a5.54 5.54 0 0 1-2.4 3.63v3h3.88c2.27-2.09 3.57-5.17 3.57-8.87Z"
      />
      <path
        fill="#34A853"
        d="M12 24c3.24 0 5.96-1.08 7.95-2.91l-3.88-3.01c-1.08.72-2.45 1.16-4.07 1.16-3.13 0-5.78-2.11-6.73-4.96H1.26v3.09A12 12 0 0 0 12 24Z"
      />
      <path fill="#FBBC05" d="M5.27 14.28a7.2 7.2 0 0 1 0-4.56V6.63H1.26a12 12 0 0 0 0 10.74l4.01-3.09Z" />
      <path
        fill="#EA4335"
        d="M12 4.75c1.77 0 3.35.61 4.6 1.8l3.44-3.44C17.95 1.19 15.24 0 12 0 7.31 0 3.26 2.69 1.26 6.63l4.01 3.09C6.22 6.86 8.87 4.75 12 4.75Z"
      />
    </svg>
  )
}
