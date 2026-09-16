import { useEffect } from 'react'
import { isRouteErrorResponse, useRouteError } from 'react-router'
import { AuthCard } from '@/components/AuthCard'
import { Button, buttonVariants } from '@/components/ui/button'

/**
 * หน้า error กลางของทุก route แทนหน้า error ภาษาอังกฤษของ react-router
 * เจอบ่อยตอนเปิดหน้าเว็บค้างไว้ระหว่างมีเวอร์ชันใหม่ หรือกดย้อนกลับหลังล็อกอิน Google · โหลดหน้าใหม่ก็หาย
 */
export function RouteError() {
  const error = useRouteError()
  const notFound = isRouteErrorResponse(error) && error.status === 404

  // เก็บของจริงไว้ใน console ให้ไล่ต่อได้ ผู้ใช้เห็นแค่ข้อความไทย
  useEffect(() => {
    console.error('[route] หน้าเว็บทำงานผิดพลาด', error)
  }, [error])

  return (
    <AuthCard
      title={notFound ? 'ไม่พบหน้านี้' : 'หน้านี้มีปัญหา'}
      description={
        notFound
          ? 'ลิงก์อาจพิมพ์ผิด หรือหน้านี้ถูกย้ายไปแล้ว'
          : 'ลองโหลดหน้าใหม่อีกครั้ง ถ้ายังขึ้นแบบนี้อยู่ให้แจ้งผู้ดูแลระบบ'
      }
    >
      <div className="grid gap-2">
        <Button onClick={() => window.location.reload()}>โหลดหน้าใหม่</Button>
        <a href="/" className={buttonVariants({ variant: 'outline' })}>
          กลับหน้าแรก
        </a>
      </div>
    </AuthCard>
  )
}
